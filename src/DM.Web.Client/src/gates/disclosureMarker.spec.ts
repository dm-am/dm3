/**
 * @vitest-environment node
 */

/**
 * The triangle in front of a row that opens is a drawing, not a word: interface
 * text is selectable by rule (textSelection.spec.ts), and a pseudo-element is
 * the one place a selection cannot reach. `.expand-marker` in Reset.sass draws
 * it. The check reads sources, the character and its escapes (`\25B6`,
 * `\u25B6`) decoded into one thing. Guillemets (U+2039, U+203A) are not
 * markers: the period pickers step with them and their line copies with them.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const SOURCES = [".vue", ".ts", ".sass", ".scss", ".css"];

/**
 * U+25B2..U+25C4 is the triangle block whole — every size, both fills, all four
 * directions — plus the two round chevrons. A disclosure marker is one of
 * these, and a character outside the set is not this test's business.
 */
const MARKER = /[\u25B2-\u25C4\u2303\u2304]/;

/** A code point spelled out: the CSS form `\25B6`, the JS form `\u25B6`. */
const ESCAPE = /\\u?\{?([0-9a-fA-F]{2,6})\}?/g;

/** The one file allowed to spell the marker, and what for. */
const ALLOWED: Record<string, string> = {
  "assets/styles/Reset.sass":
    "draws it in .expand-marker::before, where no selection reaches it",
};

/** A line with its escapes resolved; a run that is not a code point stays. */
const decode = (line: string): string =>
  line.replace(ESCAPE, (whole: string, hex: string): string => {
    const code = Number.parseInt(hex, 16);
    return code <= 0x10ffff ? String.fromCodePoint(code) : whole;
  });

const isScanned = (name: string): boolean =>
  SOURCES.some((ext) => name.endsWith(ext)) && !name.endsWith(".spec.ts");

describe("the disclosure marker is drawn, not written", () => {
  const files = collect(CLIENT_SRC, isScanned);

  it("reads the sources it is supposed to read", () => {
    // A walk that found nothing would pass every assertion below.
    expect(files.length).toBeGreaterThan(400);
  });

  it("leaves no marker in the markup of a shipped file", () => {
    const offenders: string[] = [];
    for (const file of files) {
      const path = asPath(file);
      if (path in ALLOWED) continue;
      readFileSync(file, "utf8")
        .split("\n")
        .forEach((line, index) => {
          if (MARKER.test(decode(line))) {
            offenders.push(`${path}:${index + 1}: ${line.trim()}`);
          }
        });
    }
    expect(offenders).toEqual([]);
  });
});
