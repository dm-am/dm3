/**
 * @vitest-environment node
 */

/**
 * No cut: a name, a title or a caption that does not fit wraps, and nothing
 * ends in "…" for want of room. A text cut
 * by the edge of its box is caught on the page, by the e2e of nothing hidden or
 * cut.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** A source of the client the cut is looked for in: code and styles, no spec. */
const isCutSource = (name: string): boolean =>
  [".vue", ".sass", ".scss", ".css", ".ts"].some((ext) => name.endsWith(ext)) &&
  !name.endsWith(".spec.ts");

/** Ways a text is cut: a declaration, a style object, a call, a clamp. */
const CUT_WAYS: [string, RegExp][] = [
  ["text-overflow: ellipsis", /text-overflow\s*:\s*ellipsis/],
  ["textOverflow: ellipsis", /textOverflow\s*:\s*["'`]ellipsis/],
  ['setProperty("text-overflow"', /setProperty\(\s*["'`]text-overflow/],
  ["line-clamp", /line-clamp|lineClamp/],
];

/** The cuts a file declares, its comments left out. */
function cutsIn(file: string): string[] {
  const clean = readFileSync(file, "utf8")
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .map((one) => one.replace(/(^|[^:"'`])\/\/.*$/, "$1"))
    .join("\n");
  return CUT_WAYS.filter(([, way]) => way.test(clean)).map(([name]) => name);
}

describe("no text cut", () => {
  it("is cut under an ellipsis or clamped to lines nowhere", () => {
    // A block is the full version, whole: a word longer than the line breaks
    // by the rule of the site.
    const offenders: string[] = [];
    let seen = 0;
    for (const file of collect(CLIENT_SRC, isCutSource)) {
      seen += 1;
      for (const way of cutsIn(file))
        offenders.push(`${asPath(file)}: ${way} - a text wraps and is not cut`);
    }
    expect(offenders).toEqual([]);
    expect(seen).toBeGreaterThan(500);
  });
});
