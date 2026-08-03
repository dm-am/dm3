/**
 * @vitest-environment node
 */

/**
 * The green "online" dot and the server's idea of who is online are one
 * product rule kept in two places, because a browser cannot read the server's
 * copy of it.
 *
 * ActivityPolicy.OnlinePeriod orders user lists, feeds the "active" filters
 * and the popularity score. ONLINE_THRESHOLD_MINUTES decides the dot in the
 * comment header, in the topic card, on the profile page and in the chat.
 * Raising one of them alone is the exact failure the policy class was written
 * to prevent: the server sorts by ten minutes while four screens go grey after
 * five, and nothing in either file says so.
 *
 * So the copy stays and the agreement is asserted rather than promised.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { ONLINE_THRESHOLD_MINUTES } from "./user";

const HERE = dirname(fileURLToPath(import.meta.url));
// constants -> lib -> shared -> src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(HERE, "..", "..", "..", "..", "..", "..");
const POLICY = join(
  REPO_ROOT,
  "src",
  "DM.Domain.Core",
  "Configuration",
  "ActivityPolicy.cs",
);

const DECLARED = /OnlinePeriod\s*=\s*TimeSpan\.FromMinutes\((\d+)\)/;

describe("the online threshold", () => {
  it("is the same number of minutes on both sides", () => {
    const declared = DECLARED.exec(readFileSync(POLICY, "utf8"));

    // A policy that stopped declaring the period in minutes would leave this
    // test asserting nothing.
    expect(
      declared,
      "ActivityPolicy declares OnlinePeriod as a number of minutes",
    ).not.toBeNull();

    expect(Number(declared![1])).toBe(ONLINE_THRESHOLD_MINUTES);
  });
});
