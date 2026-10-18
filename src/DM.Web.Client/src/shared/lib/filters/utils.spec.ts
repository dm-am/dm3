/**
 * @vitest-environment node
 */

/**
 * The twelve places that read `?number=` used to parse it themselves, in two
 * spellings that agreed on every case listed here. The cases are the ones the
 * spellings actually differed in wording about — an absent parameter, a
 * cleared one, a word, a zero, a repeated parameter — so a change to the one
 * remaining parser has to keep answering them the same way.
 */
import { describe, it, expect } from "vitest";
import {
  formatDateRangeForDisplay,
  formatNumberRangeForDisplay,
  parsePageNumber,
} from "./utils";

describe("parsePageNumber", () => {
  it("reads a page out of the query", () => {
    expect(parsePageNumber("3")).toBe(3);
    expect(parsePageNumber("12")).toBe(12);
  });

  it("answers undefined when there is no page in the parameter", () => {
    expect(parsePageNumber(undefined)).toBeUndefined();
    expect(parsePageNumber(null)).toBeUndefined();
    expect(parsePageNumber("")).toBeUndefined();
    expect(parsePageNumber("abc")).toBeUndefined();
  });

  it("refuses a page that does not exist", () => {
    expect(parsePageNumber("0")).toBeUndefined();
    expect(parsePageNumber("-2")).toBeUndefined();
  });

  it("takes the first value of a repeated parameter", () => {
    expect(parsePageNumber(["2", "5"])).toBe(2);
    expect(parsePageNumber([])).toBeUndefined();
  });
});

/**
 * The wording of a range in a filter bubble. Every list filter shows the same
 * two kinds of range, and each one used to spell its own: the pulse wrote
 * "от 5 до 20" where the rest of the site wrote "5 — 20", and "с ... по ..."
 * where the rest wrote a dash. The cases below are the three shapes a range
 * can arrive in, so a filter put back on its own wording fails here.
 */
describe("formatNumberRangeForDisplay", () => {
  it("puts a dash between the two ends", () => {
    expect(formatNumberRangeForDisplay(5, 20)).toBe("5 — 20");
    expect(formatNumberRangeForDisplay(-10, 0)).toBe("-10 — 0");
  });

  it("names the end that is set when the other one is open", () => {
    expect(formatNumberRangeForDisplay(3, null)).toBe("от 3");
    expect(formatNumberRangeForDisplay(null, 10)).toBe("до 10");
  });

  it("keeps a zero, which is a bound and not an absence", () => {
    expect(formatNumberRangeForDisplay(0, null)).toBe("от 0");
    expect(formatNumberRangeForDisplay(null, 0)).toBe("до 0");
  });

  it("says nothing when neither end is set", () => {
    expect(formatNumberRangeForDisplay(null, null)).toBe("");
    expect(formatNumberRangeForDisplay(undefined, undefined)).toBe("");
  });
});

describe("formatDateRangeForDisplay", () => {
  it("puts a dash between the two dates", () => {
    expect(
      formatDateRangeForDisplay({ from: "2026-09-01", to: "2026-09-20" }),
    ).toBe("01.09.2026 — 20.09.2026");
  });

  it("names the date that is set when the other one is open", () => {
    expect(formatDateRangeForDisplay({ from: "2026-09-01", to: null })).toBe(
      "с 01.09.2026",
    );
    expect(formatDateRangeForDisplay({ from: null, to: "2026-09-20" })).toBe(
      "до 20.09.2026",
    );
  });

  it("says nothing when neither date is set", () => {
    expect(formatDateRangeForDisplay({ from: null, to: null })).toBe("");
  });
});
