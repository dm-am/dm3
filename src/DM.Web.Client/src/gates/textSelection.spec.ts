/**
 * @vitest-environment node
 */

/**
 * Interface text is selectable, and `user-select: none` belongs to the
 * horizontal DashSeparator alone: the "- - - - " line it draws is a rule, not
 * words. The sidebar's own "- " prefix is not an exception: it is part of the
 * line a reader copies. Read off the sources, declaration lines only, so prose
 * that names the property in a comment is not a violation.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  CLIENT_SRC,
  asPath,
  styleFiles,
} from "@/shared/lib/testing/sourceTree";

/** The one place allowed to opt out of selection, and what for. */
const ALLOWED: Record<string, string> = {
  "shared/ui/DashSeparator/DashSeparator.vue":
    "draws the horizontal dash rule, where the glyphs are a line and not text",
  "widgets/sidebar/SiteAddresses.vue":
    "the RU mark is an icon spelled in letters: its twin is an <svg>, which " +
    "no selection can take, and letters that could made the two rows of one " +
    "list select and copy differently (owner, 2026-08-18)",
};

/** A declaration of the property, not a mention of it inside a comment. */
const DECLARATION = /^\s*user-select\s*:\s*none\s*;?\s*$/;

/**
 * A hand-typed run of dashes — the shape the rule takes when it is drawn
 * past the component instead of through it. Four repetitions are already a
 * rule and not prose.
 */
const HAND_DRAWN_RULE = /(?:- ){4}/;

const declaresIt = (source: string): boolean =>
  source.split("\n").some((line) => DECLARATION.test(line));

describe("text selection", () => {
  const files = styleFiles();

  it("reads the style sources it is supposed to read", () => {
    // A walk that found nothing would pass every assertion below.
    expect(files.length).toBeGreaterThan(200);
  });

  it("leaves every string of interface text selectable but the dash rule", () => {
    const offenders: string[] = [];
    for (const file of files) {
      const path = asPath(file);
      if (path in ALLOWED) continue;
      readFileSync(file, "utf8")
        .split("\n")
        .forEach((line, index) => {
          if (DECLARATION.test(line)) offenders.push(`${path}:${index + 1}`);
        });
    }
    expect(offenders).toEqual([]);
  });

  it("draws the dash rule through the component and never by hand", () => {
    // A copy of the separator typed into a template renders the same line
    // without the opt-out, which the check for stray declarations cannot see.
    const offenders: string[] = [];
    for (const file of files) {
      const path = asPath(file);
      if (path in ALLOWED) continue;
      readFileSync(file, "utf8")
        .split("\n")
        .forEach((line, index) => {
          if (HAND_DRAWN_RULE.test(line))
            offenders.push(`${path}:${index + 1}`);
        });
    }
    expect(offenders).toEqual([]);
  });

  it("keeps the allowance in use, so it cannot outlive its reason", () => {
    for (const [path, reason] of Object.entries(ALLOWED)) {
      const source = readFileSync(join(CLIENT_SRC, path), "utf8");
      expect(declaresIt(source), `${path}: ${reason}`).toBe(true);
    }
  });
});
