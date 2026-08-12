/**
 * @vitest-environment node
 */

/**
 * Interface text is selectable, and the rule names a single exception:
 * `user-select: none` belongs to the horizontal DashSeparator and nowhere
 * else. The "- - - - " line it draws is a rule, not words — a select-all
 * that swallowed it would paste a row of dashes into the middle of the
 * copied page.
 *
 * The sidebar's own "- " prefix is not an exception. It is decorative for
 * a screen reader (aria-hidden) but it is part of the line a reader
 * copies, so "- Игры" is the correct paste and "Игры" is not.
 *
 * The check reads sources rather than a rendered app: the declarations
 * live in scoped <style> blocks that never coexist in one DOM, and the
 * rule was broken in sixteen of them at once — each with a comment
 * asserting the opposite rule — for as long as it was kept by hand.
 *
 * Only declaration lines match, so prose that names the property inside a
 * comment is not a violation.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/** The one place allowed to opt out of selection, and what for. */
const ALLOWED: Record<string, string> = {
  "shared/ui/DashSeparator/DashSeparator.vue":
    "draws the horizontal dash rule, where the glyphs are a line and not text",
};

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

/** A declaration of the property, not a mention of it inside a comment. */
const DECLARATION = /^\s*user-select\s*:\s*none\s*;?\s*$/;

/**
 * A hand-typed run of dashes — the shape the rule takes when it is drawn
 * past the component instead of through it. Four repetitions are already a
 * rule and not prose.
 */
const HAND_DRAWN_RULE = /(?:- ){4}/;

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (STYLE_FILES.some((ext) => name.endsWith(ext))) {
      out.push(full);
    }
  }
  return out;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

const declaresIt = (source: string): boolean =>
  source.split("\n").some((line) => DECLARATION.test(line));

describe("text selection", () => {
  const files = collect(CLIENT_SRC);

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
    // The allowance above is what makes this check necessary: a copy typed
    // into a template renders the same line without the opt-out, and the
    // check for stray declarations cannot see a separator that never made
    // one. The component's own file is exempt for the same reason it holds
    // the allowance — there the line is the thing being drawn.
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
