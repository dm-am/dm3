// Whether a run measured one stand and one tree from start to end: the stand
// is shared and the working tree too, and a run that another process changed
// under it is void, which it says rather than report the change as pages.

import { spawnSync } from "node:child_process";
import { readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import {
  API_CONTAINER,
  dataFingerprint,
  fingerprintChanges,
} from "./stand.mjs";

// What identifies the stand a run measured: the database (a reset or a
// reseed creates it anew, with a new oid) and the API container. The stack is
// shared - e2e, scripts/verify-seed-reproducibility.sh and a developer's own
// reset all rebuild it - and a run that another process reset under it
// measured two fixtures. Its result is void, and it says so rather than
// reporting the reset as a page that changed. Read through docker, and only
// read; without docker both sides are empty and the fingerprint is what is
// left to tell.
export function standIdentity() {
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

export function sameStand(before) {
  const after = standIdentity();
  if (after === before) return true;
  console.error(
    `visual: the stand was rebuilt during the run (${before} -> ${after}); the result is void, run again`,
  );
  return false;
}

export async function sameData(before) {
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

export function sourcesUntouched(since, tree, directories) {
  const written = sourcesWrittenSince(since, tree, directories);
  if (written.length === 0) return true;
  console.error(
    "visual: sources were written during the run, so what it measured may have been built " +
      "from a tree that no longer exists; the result is void, run again:",
  );
  for (const path of written.slice(0, 10)) console.error(`  ${path}`);
  return false;
}
