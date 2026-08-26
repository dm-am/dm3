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
import { parsePageNumber } from "./utils";

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
