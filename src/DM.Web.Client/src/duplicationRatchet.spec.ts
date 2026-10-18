/**
 * @vitest-environment node
 */

/**
 * Repeated code may not grow. Each of the five groups (client code, client
 * specs, e2e, server code, server tests) has a ceiling of redundant lines,
 * counted by shared/lib/testing/duplicates.ts the way the clean-up baseline
 * counted them, and a group past its ceiling fails.
 *
 * A copy is what the next reader has to find twice and the next fix has to make
 * twice; the ones that drifted apart are where this codebase's bugs have come
 * from. Below the ceiling passes, so that a wave which removes copies does not
 * have to edit this file in the middle of its work: the ceilings are lowered to
 * the measured numbers at the end of each step, and never raised. Raising one
 * widens what is allowed, which takes the owner's word.
 */
import { describe, it, expect } from "vitest";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

import {
  GROUPS,
  measureDuplicates,
  type Group,
} from "@/shared/lib/testing/duplicates";

/** The repository root: this spec sits in src/DM.Web.Client/src. */
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");

const CEILING: Record<Group, number> = {
  client: 4457,
  spec: 4238,
  e2e: 419,
  server: 3211,
  srvtest: 3534,
};

describe("duplication ratchet", () => {
  it("keeps the redundant lines of every group at or under its ceiling", () => {
    const measured = measureDuplicates(ROOT);
    const over = GROUPS.filter((group) => measured[group] > CEILING[group]).map(
      (group) =>
        `${group}: ${measured[group]} redundant lines, ceiling ${CEILING[group]}: remove the new copy rather than raise the ceiling`,
    );
    expect(over).toEqual([]);
  }, 120_000);
});
