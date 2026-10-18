/**
 * The preview of a tag description draws the server's markup, character for
 * character: the client's half of the pairs of hintMarkup.cases.ts (the
 * server's is HintMarkupShould).
 */

import { describe, expect, it } from "vitest";
import { hintToHtml, isTipImageAddress, validateBBCode } from "./bbcode";
import { HINT_MARKUP_PAIRS } from "./hintMarkup.cases";

describe("the markup of a tag description, on the client", () => {
  it("is read, every pair of it", () => {
    expect(HINT_MARKUP_PAIRS.length).toBeGreaterThanOrEqual(10);
  });

  it.each(HINT_MARKUP_PAIRS.map((pair, index) => [index, ...pair] as const))(
    "pair %i: the preview draws what the server renders",
    (_index, input, html) => {
      expect(hintToHtml(input)).toBe(html);
    },
  );

  it("draws nothing for no text", () => {
    expect(hintToHtml("")).toBe("");
  });

  // The preview shows the moderator a hidden block as typed: the page erases
  // it for every reader, and the save refuses it with the reason the check
  // gives under the field.
  it("shows a hidden block as typed, and the check names it", () => {
    const input = "до [private]секрет[/private] после";
    expect(hintToHtml(input)).toBe(input);
    expect(validateBBCode(input, "hint").map((issue) => issue.reason)).toEqual([
      "private-outside-game",
    ]);
  });
});

describe("the address of a [tip] picture", () => {
  it.each([
    "https://example.com/a.gif",
    "http://example.com/a.gif",
    "/images/erp-tooltip.gif",
    " /images/a.gif ",
    "/a",
  ])("takes %j", (address) => {
    expect(isTipImageAddress(address)).toBe(true);
  });

  it.each([
    "images/a.gif",
    "/",
    "//example.com/a.gif",
    "/\\example.com/a.gif",
    "/images\\a.gif",
    "/images/a b.gif",
    "/images/a\u0001.gif",
    "javascript:alert(1)",
    "http://127.0.0.1/a.gif",
    "",
  ])("refuses %j", (address) => {
    expect(isTipImageAddress(address)).toBe(false);
  });
});
