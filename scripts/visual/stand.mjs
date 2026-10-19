// The running stand as a run finds it, only ever read: whether the API
// answers and is seeded, whether its rate limiting is off, what data it holds
// (the fingerprint a base records), and when its periodic jobs have passed.

import { spawnSync } from "node:child_process";
import { fail } from "./refusal.mjs";

export const API = process.env.VITE_API_URL || "http://localhost:5000";
export const API_CONTAINER = "dm-api";

// The API's periodic jobs run on its own clock, which the seed does not pin,
// and the first pass of each comes at a fixed delay after the host starts:
// most at once, the inactivity warnings a minute in, the pending reminders
// five (src/DM.Web.API/HostedServices). Each first pass writes (an inactive
// game gets its warning), and a run that straddled one would see its first
// states before the write and its last ones after. Waiting them out leaves
// hours with no pass that changes anything, which is longer than a run.
const QUIET_AFTER_START_MS = 6 * 60_000;

export async function awaitFirstPasses(rebuilt) {
  const inspect = spawnSync(
    "docker",
    ["inspect", API_CONTAINER, "--format", "{{.State.StartedAt}}"],
    { encoding: "utf8" },
  );
  const started = Date.parse(inspect.stdout?.trim() ?? "");
  if (Number.isNaN(started)) {
    // A stand this tool built runs in docker; the running one may not.
    if (rebuilt) fail(`the API container ${API_CONTAINER} is not running`);
    console.warn(
      `visual: cannot tell when the API started (no ${API_CONTAINER} container to ask), so its periodic jobs are not waited out`,
    );
    return;
  }
  const left = started + QUIET_AFTER_START_MS - Date.now();
  if (left <= 0) return;
  console.log(
    `visual: waiting ${Math.ceil(left / 1000)}s for the first passes of the API's periodic jobs`,
  );
  await new Promise((done) => setTimeout(done, left));
}

// The data a base was measured on, read through the API: the totals the site
// counts and when the seed's oldest account registered. The seeder lays every
// date out from the moment it runs (or from the pinned epoch), so a reseed at
// another moment moves that date even where every total comes out the same,
// and a game, a post or an account written on the stand moves a total. What a
// run leaves behind itself (sessions, last activity, what it read) moves
// neither, and neither does "online", which is the server's clock.
const SEED_ANCHOR = "SolohinLex";
const TOTALS = [
  "users",
  "characters",
  "games",
  "gamePosts",
  "blogs",
  "publications",
];

async function getJson(path) {
  try {
    const response = await fetch(`${API}${path}`, {
      headers: { Accept: "application/json" },
      signal: AbortSignal.timeout(10_000),
    });
    return {
      status: response.status,
      body: response.ok ? await response.json() : undefined,
    };
  } catch (error) {
    return { status: 0, problem: error.cause?.code ?? error.message };
  }
}

const answerOf = (reply) => reply.status || reply.problem;

export async function dataFingerprint() {
  const stats = await getJson("/v1/stats");
  if (stats.status !== 200)
    throw new Error(`GET ${API}/v1/stats answered ${answerOf(stats)}`);
  const anchor = await getJson(`/v1/users/${SEED_ANCHOR}`);
  if (anchor.status === 404) {
    throw new Error(
      `the stand is not seeded: there is no ${SEED_ANCHOR} ("powershell -File scripts/dm.ps1 seed" seeds it)`,
    );
  }
  if (anchor.status !== 200) {
    throw new Error(
      `GET ${API}/v1/users/${SEED_ANCHOR} answered ${answerOf(anchor)}`,
    );
  }
  const live = stats.body?.resource ?? stats.body;
  const account = anchor.body?.resource ?? anchor.body;
  return {
    api: API,
    totals: Object.fromEntries(
      TOTALS.map((name) => [name, live?.totals?.[name]?.value ?? null]),
    ),
    seedAnchor: {
      account: SEED_ANCHOR,
      registeredUtc: account?.registeredUtc ?? null,
    },
  };
}

const flatten = (value, path = "") =>
  value !== null && typeof value === "object"
    ? Object.entries(value).flatMap(([key, item]) =>
        flatten(item, path ? `${path}.${key}` : key),
      )
    : [[path, value]];

/** What differs between two fingerprints, a line a value. */
export function fingerprintChanges(before, after) {
  const was = new Map(flatten(before));
  const is = new Map(flatten(after));
  return [...new Set([...was.keys(), ...is.keys()])]
    .filter((key) => was.get(key) !== is.get(key))
    .map(
      (key) =>
        `${key}: ${was.get(key) ?? "(none)"} -> ${is.get(key) ?? "(none)"}`,
    );
}

// The API container as docker reports it: whether its rate limiting is off,
// and the commit "dm.ps1 e2e" (or a rebuilt stand) built it on. Only read;
// null when there is no docker or no such container running to ask, and then
// nothing is known about the limits.
function apiContainer() {
  const inspect = spawnSync(
    "docker",
    [
      "inspect",
      API_CONTAINER,
      "--format",
      "{{println .State.Running}}{{range .Config.Env}}{{println .}}{{end}}",
    ],
    { encoding: "utf8" },
  );
  if (inspect.status !== 0) return null;
  const [running, ...environment] = inspect.stdout.split(/\r?\n/);
  if (running.trim() !== "true") return null;
  const valueOf = (name) =>
    environment
      .find((line) => line.startsWith(`${name}=`))
      ?.slice(name.length + 1)
      .trim();
  return {
    unlimited: valueOf("DM_RateLimiting__Enabled")?.toLowerCase() === "false",
    builtFrom: valueOf("E2E_SOURCE_COMMIT") || null,
  };
}

const LIMITS_OFF =
  '"powershell -File scripts/dm.ps1 e2e" recreates only the API, with the limits off, and the data stays';

// What a run needs of the stand, checked before anything is built or taken:
// the API answers, it is seeded, and its rate limiting is off - a run is
// several hundred requests from one address, and under the limits the 429s
// read exactly like changed pages. Checks only: what is wrong is said, and
// starting, seeding or recreating anything is left to whoever owns the stand.
export async function checkStand() {
  const games = await getJson("/v1/games?page=1");
  if (games.status !== 200) {
    fail(
      `the API at ${API} does not answer: GET /v1/games?page=1 gave ${answerOf(games)}. ` +
        'Nothing here starts it, "powershell -File scripts/dm.ps1 start" does',
    );
  }
  const container = apiContainer();
  if (container === null) {
    console.warn(
      `visual: cannot tell whether the API's rate limiting is off (no ${API_CONTAINER} container to read it from). ` +
        `Under the limits the frames drift, and a 429 voids the run; ${LIMITS_OFF}`,
    );
  } else if (!container.unlimited) {
    fail(
      `${API_CONTAINER} runs with rate limiting on, and the run would fail on 429s; ${LIMITS_OFF}`,
    );
  }
  try {
    return {
      fingerprint: await dataFingerprint(),
      apiBuiltFrom: container?.builtFrom ?? null,
    };
  } catch (error) {
    fail(error.message);
  }
}
