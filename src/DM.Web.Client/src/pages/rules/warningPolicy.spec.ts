/**
 * @vitest-environment node
 */

// The numbers the rules promise and the numbers the server bans by are one rule in two places.
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import {
  AUTO_BAN_HOURS,
  AUTO_BAN_POINTS,
  MAX_WARNING_POINTS,
  hours,
  points,
} from "./warningPolicy";

const HERE = dirname(fileURLToPath(import.meta.url));
// rules -> pages -> src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(HERE, "..", "..", "..", "..", "..");
const POLICY = readFileSync(
  join(
    REPO_ROOT,
    "src",
    "DM.Domain.Moderation",
    "Features",
    "Warnings",
    "WarningPolicy.cs",
  ),
  "utf8",
);

function declared(name: string): number {
  const match = new RegExp(`const int ${name} = (\\d+);`).exec(POLICY);
  // A policy that stopped declaring the number would leave the check asserting nothing.
  expect(match, `WarningPolicy declares ${name} as a number`).not.toBeNull();
  return Number(match![1]);
}

describe("the numbers of the rules", () => {
  it("are the server's", () => {
    expect(AUTO_BAN_POINTS).toBe(declared("AutoBanPointsThreshold"));
    expect(AUTO_BAN_HOURS).toBe(declared("AutoBanDurationHours"));
    expect(MAX_WARNING_POINTS).toBe(declared("MaxPointsPerWarning"));
  });

  it("agree with their nouns", () => {
    expect([1, 2, 6, 11, 21].map(points)).toEqual([
      "1 балл",
      "2 балла",
      "6 баллов",
      "11 баллов",
      "21 балл",
    ]);
    expect([1, 24, 48].map(hours)).toEqual(["1 час", "24 часа", "48 часов"]);
  });
});
