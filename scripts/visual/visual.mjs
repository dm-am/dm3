#!/usr/bin/env node
// The frame stand and the API snapshot: two instruments that say whether a
// change was invisible. By default both measure the stack that is already
// running, the one the site on :5173 talks to, and change nothing in it: no
// reset, no rebuild, no seed. A base is comparable only with a run on the same
// data, so a base records a fingerprint of the stand's data, and a diff on
// other data is refused instead of reported as a wall of differences.
//
// --rebuild-stand is the strict mode: the stack is reset, rebuilt from the
// tree and seeded at a pinned epoch and seed, so the same commit measured
// twice gives the same bytes. It destroys the data of the shared stack and
// takes the site down for 10-15 minutes, so it is never the default.
//
// Outside CI on purpose: pixels depend on the fonts and the rasteriser of the
// machine, so a base is only ever compared on the machine that took it.
//
//   node scripts/visual/visual.mjs stand [--rebuild-stand]
//   node scripts/visual/visual.mjs snap <label> [--root DIR] [--theme dark]
//   node scripts/visual/visual.mjs diff <label> [--root DIR] [--theme dark]
//   node scripts/visual/visual.mjs api-snap <label> [--root DIR]
//   node scripts/visual/visual.mjs api-diff <label> [--root DIR]
//
// "stand" checks the running stand and prints its fingerprint. Common
// options: --rev REV or --commit REV (build the client from that commit
// instead of the working tree; with --rebuild-stand, the stack as well),
// --rebuild-stand, --no-build (reuse the last client build), --grep RE (only
// the states whose name matches).
// README.md next to this file says when to use which.

import { spawn, spawnSync } from "node:child_process";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
  statSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { createServer } from "node:net";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { runApiSnapshot, VOID } from "./api.mjs";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..");
const CLIENT = join(REPO, "src", "DM.Web.Client");
const WORK = process.env.DM_VISUAL_WORK || join(tmpdir(), "dm3-visual");
const DIST = join(WORK, "dist");

// The same pair scripts/verify-seed-reproducibility.sh proves reproducible.
// An old epoch on purpose: every window the server measures against its own
// clock (online, "new", the current month) is long past for every seeded row,
// so the answers do not drift while a step is in progress. The epoch is also
// where the page's clock stands, on the running stand as well: what matters
// there is that the base and the diff read the same clock.
const SEED_EPOCH = process.env.SEED_EPOCH || "2026-06-15T12:00:00Z";
const SEED_RANDOM = process.env.SEED_RANDOM || "20260730";

// The one origin besides the owner's dev server that the API admits in
// Development (src/DM.Web.API/Startup.cs), and the one e2e uses. Not a port
// to slide from: on any other one the API refuses every request by origin.
const PREVIEW_PORT = 5174;
const API = process.env.VITE_API_URL || "http://localhost:5000";
const API_CONTAINER = "dm-api";

/** What a base was measured on: the stand as it runs, or one this tool rebuilt. */
const RUNNING = "running";
const REBUILT = "rebuilt";

function parseArguments(argv) {
  const [command, ...rest] = argv;
  const options = { command, label: undefined, rebuild: false, build: true };
  for (let i = 0; i < rest.length; i++) {
    const arg = rest[i];
    if (arg === "--root") options.root = rest[++i];
    else if (arg === "--theme") options.theme = rest[++i];
    else if (arg === "--grep") options.grep = rest[++i];
    else if (arg === "--rev" || arg === "--commit") options.rev = rest[++i];
    else if (arg === "--rebuild-stand") options.rebuild = true;
    // What every run does now; accepted so that an older command line runs.
    else if (arg === "--no-stand") continue;
    else if (arg === "--no-build") options.build = false;
    else if (!arg.startsWith("--") && !options.label) options.label = arg;
    else fail(`unknown argument ${arg}`);
  }
  return options;
}

/** A refusal: said at the end of main(), and the run ends with 2. */
class Refusal extends Error {}

function fail(message) {
  throw new Refusal(message);
}

function attempt(command, args, cwd) {
  console.log(`> ${command} ${args.join(" ")}`);
  return spawnSync(command, args, { cwd, stdio: "inherit" }).status;
}

function run(command, args, cwd) {
  const status = attempt(command, args, cwd);
  if (status !== 0) fail(`${command} ${args.join(" ")} exited with ${status}`);
}

/**
 * The tree a run builds from: the working tree, or a commit exported beside it.
 *
 * A commit is the natural base of a step (what was there before the step) and
 * the one tree nobody else edits while the run is going: the working tree is
 * shared, and another process that edits a source and puts it back mid-run
 * leaves images built from a tree that no longer exists. The export is
 * `git archive` of the commit, plus what a clone does not carry: the client's
 * packages, linked from the working tree, and for a stack built from it
 * (--rebuild-stand) the stack's secrets (docker/.env).
 */
function sourceTree(rev, buildsStack) {
  if (!rev) return { root: REPO, commit: commitOf(REPO) };
  const commit = spawnSync(
    "git",
    ["-C", REPO, "rev-parse", "--verify", `${rev}^{commit}`],
    { encoding: "utf8" },
  ).stdout.trim();
  if (!commit) fail(`no commit ${rev}`);
  const root = join(WORK, "tree", commit.slice(0, 12));
  const done = join(root, ".exported");
  if (!existsSync(done)) {
    rmSync(root, { recursive: true, force: true });
    mkdirSync(root, { recursive: true });
    const archive = spawnSync(
      "git",
      ["-C", REPO, "archive", "--format=tar", commit],
      {
        maxBuffer: 1 << 30,
      },
    );
    if (archive.status !== 0) fail(`git archive ${commit} failed`);
    // Unpacked in place rather than with -C: GNU tar reads "C:" in a Windows
    // path as the name of a remote host.
    const untar = spawnSync("tar", ["-x"], {
      cwd: root,
      input: archive.stdout,
    });
    if (untar.status !== 0) fail(`unpacking ${commit} failed: ${untar.stderr}`);
    writeFileSync(done, commit);
  }
  if (buildsStack)
    copyFileSync(join(REPO, "docker", ".env"), join(root, "docker", ".env"));
  const modules = join(root, "src", "DM.Web.Client", "node_modules");
  if (!existsSync(modules))
    symlinkSync(join(CLIENT, "node_modules"), modules, "junction");
  return { root, commit };
}

// The account the application connects as, which owns the database: created
// by anyone else, the migration dies on "permission denied for schema public".
function databaseUser(docker) {
  const env = readFileSync(join(docker, ".env"), "utf8");
  return /^DB_USER=(.+)$/m.exec(env)?.[1].trim() || "dm_app";
}

const REBUILD_GRACE_SECONDS = 10;

// Said before anything is touched, and so that nobody scrolls past it: the
// stack is shared with whoever works on the site on :5173, and the site stops
// under them while it is rebuilt.
async function warnRebuild() {
  const rule = "!".repeat(78);
  console.error(
    [
      rule,
      "!! --rebuild-stand DESTROYS THE DATA OF THE SHARED STAND.",
      "!! The database dm3 is dropped and seeded anew, the API and the workers",
      "!! are rebuilt from the tree and restarted: whatever is on the stand now",
      "!! is lost, and the site on :5173 does not work for 10-15 minutes.",
      `!! Starting in ${REBUILD_GRACE_SECONDS} seconds; Ctrl+C stops it.`,
      rule,
    ].join("\n"),
  );
  await new Promise((done) => setTimeout(done, REBUILD_GRACE_SECONDS * 1000));
}

// A fresh stack: the databases dropped, this tree's images, the seed pinned,
// and the API without rate limits - one snapshot is several hundred requests
// from one address, and a 429 reads exactly like a changed page. Only ever
// on --rebuild-stand, and the warning is part of it, whoever calls it.
async function rebuildStand({ root: tree, commit }) {
  await warnRebuild();
  const manage = (command) =>
    process.platform === "win32"
      ? attempt(
          "powershell",
          [
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            join(tree, "scripts", "dm.ps1"),
            command,
          ],
          tree,
        )
      : attempt("bash", [join(tree, "scripts", "dm.sh"), command], tree);

  // "start" again when the reset fails: on empty volumes object storage and
  // RabbitMQ can pass their healthchecks after the script has stopped waiting
  // (on a busy machine RabbitMQ boots for minutes), and the stack then comes up
  // on the next attempt with nothing lost - the databases are already empty.
  if (manage("reset") !== 0 && manage("start") !== 0 && manage("start") !== 0)
    fail("the stack did not start");
  // The seed goes into a database nothing else has written to. The reset
  // starts the API, and its periodic jobs write on their own schedule: the
  // digests of the closed periods at once (news topics, numbered in the order
  // they are created), an idle game's warning a minute in. A pass before the
  // seed numbered the seed's own topics after it, a pass in the middle of the
  // seed was read back by the seeder's next step, which then drew one
  // identifier more or less and moved every identifier after it - both by the
  // clock, so the same command laid out a different fixture now and then.
  // So the applications stop, the database is made anew as the migration
  // leaves it (as scripts/verify-seed-reproducibility.sh does), and the
  // applications start again only after the seed.
  const docker = join(tree, "docker");
  const applications = ["dmapi", "dm-notification-worker", "dm-mail-worker"];
  run("docker", ["compose", "stop", ...applications], docker);
  run(
    "docker",
    [
      "compose",
      "exec",
      "-T",
      "postgres",
      "psql",
      "-U",
      "postgres",
      "-d",
      "postgres",
      "-v",
      "ON_ERROR_STOP=1",
      "-c",
      "DROP DATABASE IF EXISTS dm3 WITH (FORCE)",
      "-c",
      `CREATE DATABASE dm3 OWNER "${databaseUser(docker)}"`,
    ],
    docker,
  );
  run(
    "docker",
    ["compose", "up", "-d", "--force-recreate", "--wait", "migration"],
    docker,
  );
  // One command, not "users" then "content": both draw from one generator.
  run(
    "docker",
    [
      "compose",
      "run",
      "--rm",
      "-T",
      "--build",
      "-e",
      `DM_SeedEpochUtc=${SEED_EPOCH}`,
      "-e",
      `DM_SeedRandomSeed=${SEED_RANDOM}`,
      "seeder",
      "all",
    ],
    join(tree, "docker"),
  );
  // Recreates the API after the seed, which also drops whatever it cached
  // from the empty database, with the switches of the stand in an overlay of
  // its own (written outside docker/, which is the set that gets deployed):
  //
  // - rate limiting off, as "dm.ps1 e2e" has it, with the commit the e2e
  //   stand check reads, so the stack stays fit for e2e after a run;
  // - the last activity of an account stamped at sign-in only. By default a
  //   request a minute after the previous one stamps it again, and lists
  //   ordered by activity (the readers of a game) then ordered the stand's
  //   own accounts by which of them happened to ask last, which is timing.
  //   Stamped once, they stand in the order the run signs them in.
  const overlay = join(WORK, "stand-overlay.yml");
  mkdirSync(WORK, { recursive: true });
  writeFileSync(
    overlay,
    [
      "services:",
      "  dmapi:",
      "    environment:",
      '      DM_RateLimiting__Enabled: "false"',
      '      DM_AuthenticationConfiguration__ActivityTrackingMinutes: "5256000"',
      `      E2E_SOURCE_COMMIT: "${commit}"`,
      "",
    ].join("\n"),
  );
  run(
    "docker",
    [
      "compose",
      "-f",
      "docker-compose.yml",
      "-f",
      overlay,
      "up",
      "-d",
      "--build",
      "--no-deps",
      "--wait",
      ...applications,
    ],
    join(tree, "docker"),
  );
}

// The API's periodic jobs run on its own clock, which the seed does not pin,
// and the first pass of each comes at a fixed delay after the host starts:
// most at once, the inactivity warnings a minute in, the pending reminders
// five (src/DM.Web.API/HostedServices). Each first pass writes (an inactive
// game gets its warning), and a run that straddled one would see its first
// states before the write and its last ones after. Waiting them out leaves
// hours with no pass that changes anything, which is longer than a run.
const QUIET_AFTER_START_MS = 6 * 60_000;

async function awaitFirstPasses(rebuilt) {
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

function portIsFree(port) {
  return new Promise((done) => {
    const probe = createServer();
    probe.once("error", () => done(false));
    probe.once("listening", () => probe.close(() => done(true)));
    probe.listen(port, "localhost");
  });
}

async function waitFor(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // not up yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  fail(`${url} did not answer in ${timeoutMs / 1000}s`);
}

// A build of this working tree into a directory of its own: dist/ belongs to
// whoever ran "npm run build" last, and the owner's dev server is not a build.
function buildClient(tree) {
  const client = join(tree, "src", "DM.Web.Client");
  run(
    process.execPath,
    [
      join(client, "node_modules", "vite", "bin", "vite.js"),
      "build",
      "--outDir",
      DIST,
      "--emptyOutDir",
      "--logLevel",
      "warn",
    ],
    client,
  );
}

async function startPreview() {
  // Refusing an occupied port rather than sliding to the next one: another
  // server there is a stranger's build, and on any other port the API rejects
  // every request by origin.
  if (!(await portIsFree(PREVIEW_PORT))) {
    fail(
      `port ${PREVIEW_PORT} is taken; stop whatever serves it (an e2e run?) and retry. ` +
        "The preview cannot move: the API lets a browser in from 5173 and 5174 only",
    );
  }
  const preview = spawn(
    process.execPath,
    [
      join(CLIENT, "node_modules", "vite", "bin", "vite.js"),
      "preview",
      "--outDir",
      DIST,
      "--port",
      String(PREVIEW_PORT),
      "--strictPort",
    ],
    { cwd: CLIENT, stdio: "ignore" },
  );
  try {
    await waitFor(`http://localhost:${PREVIEW_PORT}/`, 60_000);
  } catch (error) {
    // Left running, the server would keep this process from ever ending.
    preview.kill();
    throw error;
  }
  return preview;
}

function commitOf(tree) {
  const head = spawnSync("git", ["-C", tree, "rev-parse", "HEAD"], {
    encoding: "utf8",
  });
  const dirty = spawnSync("git", ["-C", tree, "status", "--porcelain"], {
    encoding: "utf8",
  });
  return `${head.stdout.trim()}${dirty.stdout.trim() ? "+dirty" : ""}`;
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

async function dataFingerprint() {
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
function fingerprintChanges(before, after) {
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
async function checkStand() {
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

function writeMeta(directory, measurement, extra) {
  mkdirSync(directory, { recursive: true });
  const rebuilt = measurement.stand === REBUILT;
  const meta = {
    takenAt: new Date().toISOString(),
    startedAt: new Date(measurement.started).toISOString(),
    stand: measurement.stand,
    clock: SEED_EPOCH,
    // The seed is known only of a stand this tool laid out itself.
    seedEpoch: rebuilt ? SEED_EPOCH : null,
    seedRandom: rebuilt ? SEED_RANDOM : null,
    fingerprint: measurement.fingerprint,
    apiBuiltFrom: measurement.apiBuiltFrom,
    commit: measurement.tree.commit,
    ...extra,
  };
  writeFileSync(
    join(directory, "meta.json"),
    `${JSON.stringify(meta, null, 2)}\n`,
  );
}

// A base taken in another way is not a base for this run: refused before the
// stand is touched (a rebuild costs a quarter of an hour), not reported as a
// wall of differences. Whether the stand holds the base's data is asked of
// the stand itself once it is ready, in begin().
function readBase(directory, options) {
  const file = join(directory, "meta.json");
  if (!existsSync(file))
    fail(`no base at ${directory}; take one with snap first`);
  const meta = JSON.parse(readFileSync(file, "utf8"));
  if (!meta.fingerprint) {
    fail(
      `the base at ${directory} records no fingerprint of the stand's data (an older visual.mjs took it), ` +
        "so nothing says the stand still holds that data; take it again",
    );
  }
  const stand = options.rebuild ? REBUILT : RUNNING;
  if (meta.stand !== stand) {
    fail(
      meta.stand === REBUILT
        ? "the base was taken on a rebuilt stand (--rebuild-stand) and this run measures the running one: pass the flag here too, or take the base again without it"
        : "the base was taken on the running stand, and a rebuilt one never holds the same data: take the base with --rebuild-stand too, or drop the flag here",
    );
  }
  if (meta.clock !== SEED_EPOCH) {
    fail(
      `the base's pages stood at ${meta.clock}, this run's at ${SEED_EPOCH} (SEED_EPOCH)`,
    );
  }
  if (stand === REBUILT && meta.seedRandom !== SEED_RANDOM) {
    fail(
      `the base was seeded with ${meta.seedRandom}, this run with ${SEED_RANDOM} (SEED_RANDOM)`,
    );
  }
  // Not refused: a part of a full base is a fair comparison for the states
  // that leave no mark (the guest's), and README says which those are.
  if ((meta.grep ?? null) !== (options.grep ?? null)) {
    console.warn(
      `visual: the base was taken with --grep ${meta.grep ?? "(none)"}, this run uses ${options.grep ?? "(none)"}`,
    );
  }
  return meta;
}

// What identifies the stand a run measured: the database (a reset or a
// reseed creates it anew, with a new oid) and the API container. The stack is
// shared - e2e, scripts/verify-seed-reproducibility.sh and a developer's own
// reset all rebuild it - and a run that another process reset under it
// measured two fixtures. Its result is void, and it says so rather than
// reporting the reset as a page that changed. Read through docker, and only
// read; without docker both sides are empty and the fingerprint is what is
// left to tell.
function standIdentity() {
  const database = spawnSync(
    "docker",
    [
      "exec",
      "dm-pg",
      "psql",
      "-U",
      "postgres",
      "-d",
      "postgres",
      "-tAc",
      "select oid from pg_database where datname = 'dm3'",
    ],
    { encoding: "utf8" },
  );
  const api = spawnSync(
    "docker",
    ["inspect", "--format", "{{.Id}}", API_CONTAINER],
    { encoding: "utf8" },
  );
  return `database ${database.stdout?.trim() ?? ""}, api ${api.stdout?.trim().slice(0, 12) ?? ""}`;
}

function sameStand(before) {
  const after = standIdentity();
  if (after === before) return true;
  console.error(
    `visual: the stand was rebuilt during the run (${before} -> ${after}); the result is void, run again`,
  );
  return false;
}

async function sameData(before) {
  let after;
  try {
    after = await dataFingerprint();
  } catch (error) {
    console.error(
      `visual: the stand stopped answering during the run (${error.message}); the result is void, run again`,
    );
    return false;
  }
  const changes = fingerprintChanges(before, after);
  if (changes.length === 0) return true;
  console.error(
    `visual: the stand's data changed during the run (${changes.join("; ")}); the result is void, run again`,
  );
  return false;
}

// The sources of what a run measures, written to while it was going: the
// whole of src/ for a rebuilt stand, the client alone on the running stand
// (whose server is whatever it runs), and nothing for the API snapshot there.
// The working tree is shared too: another process editing a file and putting
// it back (a mutation run proving that a test fails) leaves the tree as it
// was and the images built in between with its edit. The time of the last
// write is what such an edit leaves behind. Specs and the client's test
// helpers are not built into anything the stand serves.
const NOT_BUILT = new Set([
  "node_modules",
  "bin",
  "obj",
  "dist",
  "coverage",
  ".vite",
  "test-results",
  "playwright-report",
  "TestResults",
]);
const NOT_SERVED = /\.(spec|test|cases)\.ts$|[\\/]lib[\\/]testing[\\/]/;

function sourcesWrittenSince(since, tree, directories) {
  const written = [];
  const walk = (directory) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      if (NOT_BUILT.has(entry.name)) continue;
      const path = join(directory, entry.name);
      if (entry.isDirectory()) walk(path);
      else if (!NOT_SERVED.test(path) && statSync(path).mtimeMs >= since)
        written.push(path);
    }
  };
  for (const directory of directories) walk(join(tree, directory));
  return written;
}

function sourcesUntouched(since, tree, directories) {
  const written = sourcesWrittenSince(since, tree, directories);
  if (written.length === 0) return true;
  console.error(
    "visual: sources were written during the run, so what it measured may have been built " +
      "from a tree that no longer exists; the result is void, run again:",
  );
  for (const path of written.slice(0, 10)) console.error(`  ${path}`);
  return false;
}

// A snapshot replaces the base whole: a state removed from the list must not
// linger in it and be compared forever.
function clearBase(directory) {
  rmSync(directory, { recursive: true, force: true });
}

/**
 * What every run does before it measures: refuses a base it cannot be
 * compared with, rebuilds the stand only on --rebuild-stand, checks the stand
 * it is about to measure, and keeps what identifies that stand, so that the
 * end of the run can tell one stand from two.
 *
 * `built` are the directories of the tree that the run builds what it
 * measures from on the running stand.
 */
async function begin(options, update, base, built) {
  const meta = update ? null : readBase(base, options);
  const started = Date.now();
  const tree = sourceTree(options.rev, options.rebuild);
  if (options.rebuild) await rebuildStand(tree);
  else {
    console.log(
      `visual: measuring the running stand at ${API} as it is: nothing is reset, rebuilt or seeded`,
    );
  }
  const { fingerprint, apiBuiltFrom } = await checkStand();
  if (meta) {
    const changes = fingerprintChanges(meta.fingerprint, fingerprint);
    if (changes.length > 0) {
      fail(
        [
          "the stand does not hold the data the base was taken on:",
          ...changes.map((line) => `  ${line}`),
          "It was reseeded or written to since the base (or the server counts differently now): " +
            "take the base again, then compare.",
        ].join("\n"),
      );
    }
    if ((meta.apiBuiltFrom ?? null) !== apiBuiltFrom) {
      console.log(
        `visual: the base measured the API built on ${meta.apiBuiltFrom ?? "(unknown)"}, this run the one built on ${apiBuiltFrom ?? "(unknown)"}`,
      );
    }
  }
  if (update) clearBase(base);
  const identity = standIdentity();
  const watched = options.rebuild ? ["src"] : built;
  return {
    tree,
    started,
    meta,
    fingerprint,
    apiBuiltFrom,
    stand: options.rebuild ? REBUILT : RUNNING,
    intact: async () =>
      sameStand(identity) &&
      (await sameData(fingerprint)) &&
      sourcesUntouched(started, tree.root, watched),
  };
}

async function frames(options, update) {
  const theme = options.theme === "dark" ? "dark" : "light";
  const root = resolve(
    options.root || process.env.DM_VISUAL_ROOT || join(WORK, "frames"),
  );
  const base = join(root, options.label, theme);
  const report = join(root, `${options.label}.diff`, theme);
  const measurement = await begin(options, update, base, [
    join("src", "DM.Web.Client"),
  ]);
  if (options.build || !existsSync(DIST)) buildClient(measurement.tree.root);
  await awaitFirstPasses(options.rebuild);
  const preview = await startPreview();

  const args = [
    join(CLIENT, "node_modules", "@playwright", "test", "cli.js"),
    "test",
    "-c",
    join(HERE, "frames.config.ts"),
    `--update-snapshots=${update ? "all" : "none"}`,
  ];
  if (options.grep) args.push("--grep", options.grep);

  const status = await new Promise((done) => {
    const child = spawn(process.execPath, args, {
      cwd: CLIENT,
      stdio: "inherit",
      env: {
        ...process.env,
        // The config and the specs live outside the client package, so the
        // client's copy of @playwright/test is found through NODE_PATH: the
        // specs are compiled to CommonJS, which honours it.
        NODE_PATH: join(CLIENT, "node_modules"),
        VISUAL_BASE: base,
        VISUAL_REPORT: report,
        VISUAL_OUTPUT: join(WORK, "output"),
        VISUAL_THEME: theme,
        VISUAL_MODE: update ? "snap" : "diff",
        VISUAL_EPOCH: SEED_EPOCH,
        VISUAL_APP: `http://localhost:${PREVIEW_PORT}`,
        VISUAL_API: API,
        VISUAL_AUTH: join(WORK, "auth"),
      },
    });
    child.on("exit", (code) => done(code ?? 1));
  });
  preview.kill();
  if (!(await measurement.intact())) return VOID;

  if (update && status === 0)
    writeMeta(base, measurement, { theme, grep: options.grep ?? null });
  if (!update) console.log(`report: ${join(report, "report.txt")}`);
  return status;
}

async function api(options, update) {
  const root = resolve(
    options.root || process.env.DM_API_SNAPSHOT_ROOT || join(WORK, "api"),
  );
  const base = join(root, options.label);
  const report = join(root, `${options.label}.diff`);
  const measurement = await begin(options, update, base, []);
  await awaitFirstPasses(options.rebuild);
  const status = await runApiSnapshot({
    routes: join(measurement.tree.root, "artifacts", "openapi-routes.json"),
    api: API,
    base,
    report,
    update,
    grep: options.grep,
    baseStarted: measurement.meta
      ? Date.parse(measurement.meta.startedAt)
      : undefined,
  });
  if (!(await measurement.intact())) return VOID;
  if (update && status === 0)
    writeMeta(base, measurement, { grep: options.grep ?? null });
  return status;
}

async function main(argv) {
  const options = parseArguments(argv);
  const needsLabel = ["snap", "diff", "api-snap", "api-diff"];
  if (needsLabel.includes(options.command) && !options.label)
    fail(`${options.command} needs a label`);

  switch (options.command) {
    case "stand": {
      if (options.rebuild) await rebuildStand(sourceTree(options.rev, true));
      const { fingerprint, apiBuiltFrom } = await checkStand();
      console.log(JSON.stringify({ apiBuiltFrom, fingerprint }, null, 2));
      return 0;
    }
    case "snap":
      return frames(options, true);
    case "diff":
      return frames(options, false);
    case "api-snap":
      return api(options, true);
    case "api-diff":
      return api(options, false);
    default: {
      // The usage is the comment this file opens with.
      const lines = readFileSync(fileURLToPath(import.meta.url), "utf8").split(
        "\n",
      );
      const usage = [];
      for (const line of lines.slice(1)) {
        if (!line.startsWith("//")) break;
        usage.push(line.replace(/^\/\/ ?/, ""));
      }
      console.log(usage.join("\n"));
      return options.command ? 2 : 0;
    }
  }
}

// The exit code is the verdict, so it is set and the process left to end on
// its own: process.exit() with a fetch's connection still open aborts Node on
// Windows (a libuv assertion, exit 127), and so does an unhandled rejection.
try {
  process.exitCode = await main(process.argv.slice(2));
} catch (error) {
  if (error instanceof Refusal) {
    console.error(`visual: ${error.message}`);
    process.exitCode = 2;
  } else {
    console.error(error);
    process.exitCode = 1;
  }
}
