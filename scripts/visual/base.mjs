// A base: the meta.json a snapshot writes beside what it took, and the checks
// that refuse a base this run cannot be compared with.

import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { join } from "node:path";
import { SEED_EPOCH, SEED_RANDOM } from "./rebuild.mjs";
import { fail } from "./refusal.mjs";

/** What a base was measured on: the stand as it runs, or one this tool rebuilt. */
export const RUNNING = "running";
export const REBUILT = "rebuilt";

export function writeMeta(directory, measurement, extra) {
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
export function readBase(directory, options) {
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

// A snapshot replaces the base whole: a state removed from the list must not
// linger in it and be compared forever.
export function clearBase(directory) {
  rmSync(directory, { recursive: true, force: true });
}
