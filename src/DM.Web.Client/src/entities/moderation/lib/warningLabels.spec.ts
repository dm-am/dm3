// A decision is signed "what от whom, when", the first part a noun phrase that reads whole.
import { describe, it, expect } from "vitest";
import { BanType } from "../api/banApi";
import {
  banScopeLabel,
  banTermLabel,
  pointsChangeLabel,
  warningKindLabel,
} from "./warningLabels";

const started = "2026-09-15T03:17:00Z";
const plusDays = (days: number) =>
  new Date(Date.parse(started) + days * 86_400_000).toISOString();

describe("the kind of a warning", () => {
  it.each([
    [1, "Предупреждение (1 балл)"],
    [2, "Предупреждение (2 балла)"],
    [6, "Предупреждение (6 баллов)"],
  ])("puts %i points in brackets beside the word", (points, expected) => {
    expect(warningKindLabel(points)).toBe(expected);
  });

  it("says nothing of points for a verbal one", () => {
    expect(warningKindLabel(0)).toBe("Устное предупреждение");
  });
});

// The change is named by the points it left; a change to nothing counts its nothing.
describe("the kind of a change of the points", () => {
  it.each([
    [1, "Изменение баллов (1 балл)"],
    [2, "Изменение баллов (2 балла)"],
    [5, "Изменение баллов (5 баллов)"],
    [0, "Изменение баллов (0 баллов)"],
  ])("puts the %i points it left in brackets", (points, expected) => {
    expect(pointsChangeLabel(points)).toBe(expected);
  });
});

// A ban without an end is "бессрочный", not a date a hundred years ahead.
describe("the scope of a ban", () => {
  it.each([
    ["DemocraticBan", "демократический бан"],
    ["FullBan", "полный бан"],
  ] as const)("names a %s by what it forbids", (accessPolicy, expected) => {
    expect(
      banScopeLabel({
        type: BanType.Temporary,
        accessPolicy,
        startedUtc: started,
        expiresUtc: plusDays(7),
      }),
    ).toBe(expected);
  });

  it.each([
    ["DemocraticBan", "бессрочный демократический бан"],
    ["FullBan", "бессрочный полный бан"],
  ] as const)(
    "names a permanent %s so, in a word",
    (accessPolicy, expected) => {
      expect(
        banScopeLabel({
          type: BanType.Permanent,
          accessPolicy,
          startedUtc: started,
          expiresUtc: plusDays(36500),
        }),
      ).toBe(expected);
    },
  );

  it("names a ban without an end as permanent", () => {
    expect(
      banScopeLabel({
        type: BanType.Temporary,
        accessPolicy: "DemocraticBan",
        startedUtc: started,
      }),
    ).toBe("бессрочный демократический бан");
  });

  /** There is no voluntary ban on the site: a row with the flag reads as any. */
  it("writes a voluntary ban with the ordinary words", () => {
    expect(
      banScopeLabel({
        type: BanType.Voluntary,
        accessPolicy: "DemocraticBan",
        startedUtc: started,
        expiresUtc: plusDays(3),
      }),
    ).toBe("демократический бан");
  });
});

/** The term in the table of violators: whole days, or "Бессрочный". */
describe("the term of a ban", () => {
  it.each([
    [1, "1 день"],
    [2, "2 дня"],
    [7, "7 дней"],
  ])("counts a ban of %i days", (days, expected) => {
    expect(
      banTermLabel({
        type: BanType.Temporary,
        startedUtc: started,
        expiresUtc: plusDays(days),
      }),
    ).toBe(expected);
  });

  it("calls a ban without an end permanent", () => {
    expect(banTermLabel({ type: BanType.Temporary, startedUtc: started })).toBe(
      "Бессрочный",
    );
  });
});
