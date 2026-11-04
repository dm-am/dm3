/**
 * @vitest-environment node
 */

// The online dot and ActivityPolicy.OnlinePeriod, the newbie hints and
// ProbationPolicy.NewbiePostThreshold: each one rule in two places.
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { NEWBIE_POST_THRESHOLD, ONLINE_THRESHOLD_MINUTES } from "./user";

const HERE = dirname(fileURLToPath(import.meta.url));
// constants -> lib -> shared -> src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(HERE, "..", "..", "..", "..", "..", "..");
const POLICY_DIR = join(REPO_ROOT, "src", "DM.Domain.Core", "Configuration");
const POLICY = join(POLICY_DIR, "ActivityPolicy.cs");
const PROBATION = join(POLICY_DIR, "ProbationPolicy.cs");

const DECLARED = /OnlinePeriod\s*=\s*TimeSpan\.FromMinutes\((\d+)\)/;

describe("the online threshold", () => {
  it("is the same number of minutes on both sides", () => {
    const declared = DECLARED.exec(readFileSync(POLICY, "utf8"));

    // A policy that stopped declaring minutes would leave this asserting nothing.
    expect(
      declared,
      "ActivityPolicy declares OnlinePeriod as a number of minutes",
    ).not.toBeNull();

    expect(Number(declared![1])).toBe(ONLINE_THRESHOLD_MINUTES);
  });
});

describe("the newbie threshold", () => {
  it("is the same number of game posts on both sides", () => {
    const declared = /NewbiePostThreshold\s*=\s*(\d+);/.exec(
      readFileSync(PROBATION, "utf8"),
    );

    expect(
      declared,
      "ProbationPolicy declares NewbiePostThreshold as a number",
    ).not.toBeNull();

    expect(Number(declared![1])).toBe(NEWBIE_POST_THRESHOLD);
  });
});
