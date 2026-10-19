/**
 * The cost of reading forms written by hand, each its own way to pay the
 * square (BBCODE_RENDERING.md, "Цена разбора"); generated texts are
 * bbcodeProperties.spec.ts's.
 */

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { validateBBCode } from "./bbcodeWarnings";
import { expectRoundTrip } from "@/shared/lib/testing/converterRoundTrip";

/**
 * The reader is linear in the text, as the server's (BbParserCostShould): four
 * times the text costs four times, where retrying from every position costs
 * sixteen.
 */
describe("the cost of reading", () => {
  const FORMS: Array<[string, (n: number) => string]> = [
    ["a name and spaces", (n) => "[img" + " ".repeat(n) + "x"],
    ["pairs of parameters", (n) => "[img" + ' a="1"'.repeat(n) + " x"],
    ["openings in a value", (n) => '[img alt="'.repeat(n) + "x"],
    ["quotes in a row", (n) => '[spoiler text="' + '"'.repeat(n) + " x"],
    ["hidden blocks never ended", (n) => '[private="a'.repeat(n)],
    ["tags never closed", (n) => "[b]".repeat(n)],
    ["closing tags with nothing open", (n) => "[/b]".repeat(n)],
    [
      "a verbatim block never closed",
      (n) => "[noparse]" + "[private ".repeat(n),
    ],
    // Every [noparse] looks ahead for its closing tag, past every "[/" there
    // is: a reader that looked again from each of them would pay the square.
    ["examples never closed", (n) => "[b][noparse][/b]".repeat(n)],
    [
      "closing tags inside a hidden block",
      (n) => "[b][private]" + "[/b]".repeat(n),
    ],
  ];

  /** Past this on the smaller text the reader is no linear one; the run stops at once. */
  const CEILING_MS = 1_000;

  /**
   * The shortest time a ratio is taken against: a run shorter is mostly the
   * noise of the machine, and a square reader is far past it anyway.
   */
  const FLOOR_MS = 5;

  const cost = (read: () => unknown): number => {
    let best = Infinity;
    for (let run = 0; run < 5; run++) {
      const started = performance.now();
      read();
      const took = performance.now() - started;
      best = Math.min(best, took);
      if (took > CEILING_MS) break;
    }
    return best;
  };

  const ratio = (
    read: (text: string) => unknown,
    small: string,
    large: string,
  ): number => {
    const few = cost(() => read(small));
    if (few > CEILING_MS) return Infinity;
    return cost(() => read(large)) / Math.max(few, FLOOR_MS);
  };

  it.each(FORMS)("grows with the text: %s", (_name, form) => {
    const small = form(10_000);
    const large = form(40_000);
    cost(() => bbcodeToHtml(small));

    // Four times the text; a square would make it sixteen times dearer.
    expect(ratio(bbcodeToHtml, small, large)).toBeLessThan(8);
    expect(ratio(validateBBCode, small, large)).toBeLessThan(8);
  });

  // Deeper than the server takes, the text is shown as typed (TreeMaxDepth).
  it("reads a tree deeper than the server takes as the text it is", () => {
    const deep = "[b]".repeat(600) + "x" + "[/b]".repeat(600);
    expect(bbcodeToHtml(deep)).toBe(`<p>${deep}</p>`);
    expect(validateBBCode(deep).map((issue) => issue.reason)).toEqual([
      "too-deep",
    ]);
    expectRoundTrip(deep);
  });
});
