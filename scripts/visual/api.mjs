// The API snapshot: every GET the API declares, asked by a guest, a member and
// a moderator, written down as normalised JSON. Two runs of one commit on a
// fresh pinned stand write the same files; a server change that is meant to be
// invisible leaves them the same as well.
//
// The routes come from artifacts/openapi-routes.json, which the integration
// suite writes and a test keeps in step with the controllers, so a GET added
// to the API is asked here without anyone touching this file. Its path
// parameters are not: a route api-fixture.mjs cannot fill is reported, not
// skipped.

import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, join } from "node:path";
import { PARAMETERS, QUERIES, discover } from "./api-fixture.mjs";
import {
  ACTIVITY_ORDERED,
  collapseRepeated,
  orderStandAccounts,
  REPEATED,
  REWRITES,
  VOLATILE,
} from "./api-volatile.mjs";
import { UNORDERED } from "./unordered.mjs";

const ORIGIN = "http://localhost:5174";
const PASSWORD = "Test123!";

/** The exit code of a run whose result is void: what it measured was not one stand. */
export const VOID = 3;

const LIMITED =
  'the API\'s rate limiting is on; "powershell -File scripts/dm.ps1 e2e" recreates only the API, with the limits off, and the data stays';

/** Who asks. The member and the moderator are seeded accounts (DataSeeder.Users.cs). */
const ROLES = [
  { name: "guest" },
  { name: "user", email: "user@test.local" },
  { name: "moderator", email: "mod@test.local" },
];

/**
 * A cookie jar of one session, enough for the API's own cookies. The 429s it
 * meets go into `refused`, which the sessions of a run share: a refusal is
 * the API's rate limiting and not its answer, and a snapshot holding one is
 * void.
 */
class Session {
  constructor(api, name, refused) {
    this.api = api;
    this.name = name;
    this.refused = refused;
    this.cookies = new Map();
  }

  async login(email) {
    const response = await this.request("POST", "/v1/account/login", {
      email,
      password: PASSWORD,
    });
    if (response.status !== 200) {
      throw new Error(
        `login of ${email} failed with ${response.status}: ${await response.text()}` +
          (response.status === 429 ? `; ${LIMITED}` : ""),
      );
    }
  }

  async request(method, path, body) {
    const headers = { Origin: ORIGIN, Accept: "application/json" };
    if (this.cookies.size > 0) {
      headers.Cookie = [...this.cookies]
        .map(([name, value]) => `${name}=${value}`)
        .join("; ");
    }
    if (body) headers["Content-Type"] = "application/json";
    const response = await fetch(`${this.api}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
      redirect: "manual",
    });
    if (response.status === 429) this.refused.push(`${method} ${path}`);
    for (const cookie of response.headers.getSetCookie()) {
      const [pair] = cookie.split(";");
      const at = pair.indexOf("=");
      this.cookies.set(pair.slice(0, at).trim(), pair.slice(at + 1).trim());
    }
    return response;
  }

  async json(path) {
    const response = await this.request("GET", path);
    if (!response.ok)
      throw new Error(`GET ${path} as ${this.name}: ${response.status}`);
    return response.json();
  }
}

/**
 * Replaces the values of the volatile fields, wherever in the tree they are,
 * and rewrites the volatile pieces of every string.
 */
function normalise(value, route, run) {
  if (typeof value === "string")
    return REWRITES.reduce(
      (text, rule) => text.replace(rule.pattern, rule.replacement),
      value,
    );
  if (Array.isArray(value))
    return value.map((item) => normalise(item, route, run));
  if (value && typeof value === "object") {
    const out = {};
    for (const [key, item] of Object.entries(value)) {
      const rule = VOLATILE.find(
        (v) =>
          (v.field === key || v.field === "*") &&
          (!v.routes || v.routes.test(route)) &&
          (!v.when || v.when(item, run)),
      );
      out[key] =
        rule && item !== null
          ? `<${rule.field === "*" ? "written by the run" : rule.field}>`
          : normalise(item, route, run);
      if (ACTIVITY_ORDERED.fields.has(key) && Array.isArray(out[key]))
        orderStandAccounts(out[key]);
    }
    return out;
  }
  return value;
}

async function answer(session, route, url, run) {
  const response = await session.request("GET", url);
  const type = response.headers.get("content-type") ?? "";
  const bytes = Buffer.from(await response.arrayBuffer());
  let body;
  if (type.includes("json")) {
    const text = bytes.toString("utf8");
    body = text ? normalise(JSON.parse(text), route, run) : "";
    const written = (moment) => moment === "<written by the run>";
    UNORDERED.find((rule) => rule.route === route)?.order(body, written);
    const repeated = REPEATED.find((rule) => rule.route === route);
    if (repeated) collapseRepeated(body, repeated, written);
  } else if (type.startsWith("text/")) {
    body = bytes.toString("utf8");
  } else {
    body = {
      bytes: bytes.length,
      sha256: createHash("sha256").update(bytes).digest("hex"),
    };
  }
  return {
    route,
    url,
    status: response.status,
    contentType: type.split(";")[0],
    body,
  };
}

/** The address of one route, with its path parameters and query filled. */
function addressOf(route, fixture) {
  const path = route.slice("GET ".length);
  const fill = PARAMETERS[path];
  const names = [...path.matchAll(/\{(\w+)\}/g)].map((m) => m[1]);
  if (names.length > 0 && !fill)
    return { missing: `no parameters for ${path} in api-fixture.mjs` };
  const values = fill ? fill(fixture) : {};
  let url = path;
  for (const name of names) {
    if (values[name] === undefined || values[name] === null)
      return { missing: `${path}: the fixture has no ${name}` };
    url = url.replace(`{${name}}`, encodeURIComponent(String(values[name])));
  }
  const query = QUERIES[path];
  if (query)
    url += `?${new URLSearchParams(typeof query === "function" ? query(fixture) : query)}`;
  return { url };
}

const fileOf = (route) =>
  `${route
    .replace(/^GET \/v1\//, "")
    .replace(/[{}]/g, "")
    .replace(/[^\w.-]+/g, "_")}.json`;

function compare(base, current, report) {
  const differences = [];
  // Ids that moved make every answer about them move too; said first, once.
  if (
    readFileSync(join(base, "fixture.json"), "utf8") !==
    readFileSync(join(current, "fixture.json"), "utf8")
  ) {
    differences.push(
      "DIFFERS  fixture.json (the looked-up ids are not the base's)",
    );
  }
  for (const role of ROLES.map((r) => r.name)) {
    const baseDir = join(base, role);
    const currentDir = join(current, role);
    const files = new Set([
      ...(existsSync(baseDir) ? readdirSync(baseDir) : []),
      ...(existsSync(currentDir) ? readdirSync(currentDir) : []),
    ]);
    for (const file of [...files].sort()) {
      const a = join(baseDir, file);
      const b = join(currentDir, file);
      if (!existsSync(a)) differences.push(`ADDED    ${role} ${file}`);
      else if (!existsSync(b)) differences.push(`REMOVED  ${role} ${file}`);
      else if (readFileSync(a, "utf8") !== readFileSync(b, "utf8")) {
        differences.push(`DIFFERS  ${role} ${file}`);
        const diff = spawnSync(
          "git",
          ["diff", "--no-index", "--no-color", a, b],
          { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
        );
        mkdirSync(join(report, role), { recursive: true });
        writeFileSync(join(report, role, `${file}.diff`), diff.stdout);
      }
    }
  }
  return differences;
}

export async function runApiSnapshot({
  routes: routesFile,
  api,
  base,
  report,
  update,
  grep,
  baseStarted,
}) {
  // When this run and the run that took the base started (api-volatile.mjs
  // reads both).
  const run = { started: Date.now(), baseStarted };
  const routes = JSON.parse(readFileSync(routesFile, "utf8"))
    .filter((route) => route.startsWith("GET "))
    .filter((route) => !grep || new RegExp(grep).test(route));

  const refused = [];
  const sessions = [];
  for (const role of ROLES) {
    const session = new Session(api, role.name, refused);
    if (role.email) await session.login(role.email);
    sessions.push(session);
  }
  // The administrator only looks ids up; its answers are not part of the snapshot.
  const admin = new Session(api, "admin", refused);
  await admin.login("admin@test.local");
  const fixture = await discover(sessions[1], admin);

  // A diff starts from an empty report: a difference of the previous run that
  // is gone now must not be read as one still there.
  const target = update ? base : join(report, "current");
  if (!update) rmSync(report, { recursive: true, force: true });
  const unfilled = [];
  for (const route of routes) {
    const { url, missing } = addressOf(route, fixture);
    if (missing) {
      unfilled.push(missing);
      continue;
    }
    for (const session of sessions) {
      const result = await answer(session, route, url, run);
      const file = join(target, session.name, fileOf(route));
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, `${JSON.stringify(result, null, 2)}\n`);
    }
  }
  writeFileSync(
    join(target, "fixture.json"),
    `${JSON.stringify(fixture, null, 2)}\n`,
  );

  for (const line of unfilled) console.log(`NOT ASKED ${line}`);
  console.log(
    `routes: ${routes.length}, asked: ${routes.length - unfilled.length}, roles: ${ROLES.length}`,
  );
  if (refused.length > 0) {
    console.error(
      `visual: the API answered 429 to ${refused.length} requests (the first: ${refused[0]}), ` +
        `so the snapshot holds refusals; the result is void: ${LIMITED}`,
    );
    return VOID;
  }
  if (unfilled.length > 0) return 1;
  if (update) return 0;

  const differences = compare(base, target, report);
  const lines = [`differences: ${differences.length}`, ...differences];
  mkdirSync(report, { recursive: true });
  writeFileSync(join(report, "report.txt"), `${lines.join("\n")}\n`);
  console.log(lines.join("\n"));
  console.log(`report: ${join(report, "report.txt")}`);
  return differences.length > 0 ? 1 : 0;
}
