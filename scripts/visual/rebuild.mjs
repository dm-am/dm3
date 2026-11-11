// The strict mode, --rebuild-stand: the shared stack reset, rebuilt from the
// tree and seeded at a pinned epoch and seed, so that the same commit measured
// twice gives the same bytes. It destroys the stand's data, so nothing here
// runs without the flag.

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { attempt, fail, run } from "./refusal.mjs";
import { WORK } from "./tree.mjs";

// The same pair scripts/verify-seed-reproducibility.sh proves reproducible.
// An old epoch on purpose: every window the server measures against its own
// clock (online, "new", the current month) is long past for every seeded row,
// so the answers do not drift while a step is in progress. The epoch is also
// where the page's clock stands, on the running stand as well: what matters
// there is that the base and the diff read the same clock.
export const SEED_EPOCH = process.env.SEED_EPOCH || "2026-06-15T12:00:00Z";
export const SEED_RANDOM = process.env.SEED_RANDOM || "20260730";

// The applications that write to the database: stopped while it is made anew
// and started again only after the seed.
const APPLICATIONS = ["dmapi", "dm-notification-worker", "dm-mail-worker"];

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
export async function rebuildStand({ root: tree, commit }) {
  await warnRebuild();
  resetStack(tree);
  freshDatabase(tree);
  seedPinned(tree);
  startApplications(tree, commit);
}

// "start" again when the reset fails: on empty volumes object storage can
// pass its healthcheck after the script has stopped waiting, and the stack
// then comes up on the next attempt with nothing lost - the databases are
// already empty.
function resetStack(tree) {
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

  if (manage("reset") !== 0 && manage("start") !== 0 && manage("start") !== 0)
    fail("the stack did not start");
}

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
function freshDatabase(tree) {
  const docker = join(tree, "docker");
  run("docker", ["compose", "stop", ...APPLICATIONS], docker);
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
      "-c",
      "REVOKE CONNECT ON DATABASE dm3 FROM PUBLIC",
    ],
    docker,
  );
  run(
    "docker",
    ["compose", "up", "-d", "--force-recreate", "--wait", "migration"],
    docker,
  );
  // "up --wait" returns once the container runs, and the migration is done
  // only when it has exited: "wait" blocks until then and fails on its code.
  run("docker", ["compose", "wait", "migration"], docker);
}

// One command, not "users" then "content": both draw from one generator.
function seedPinned(tree) {
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
}

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
function startApplications(tree, commit) {
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
      ...APPLICATIONS,
    ],
    join(tree, "docker"),
  );
}
