/**
 * @vitest-environment node
 */

/**
 * The triangle in front of a row that opens is a drawing, not a word. It used
 * to be a text node — an aria-hidden span holding `symbols.triangleRight` — in
 * five rows across three components, and interface text is selectable by rule
 * (textSelection.spec.ts), so a reader who selected such a row pasted the
 * marker along with it: "Атака на сайт" came out with a triangle in front of
 * it. A pseudo-element is the one place a selection cannot reach, and that is
 * where the glyph lives now. `.expand-marker` in Reset.sass draws it, and the
 * quarter turn it makes on `.expanded` is the former right-to-down swap.
 *
 * The check reads sources, because a marker comes back in two spellings and
 * neither is visible as a difference in a rendered page:
 *   - the character, written straight into a template;
 *   - an escape, which is how it was written before — the symbols table spelled
 *     its triangles "\u25BC" and the like, so a scan for the character alone
 *     would have walked past the very code this replaced.
 * Every line is decoded before it is read, so the two spellings are one thing.
 *
 * Guillemets (U+2039, U+203A) are deliberately not markers here: the period
 * pickers step with them and their line is meant to copy with them in it.
 *
 * `pages/dev` is out of scope, as in accessibleNames.spec.ts — the router
 * registers those mockup catalogs under `import.meta.env.DEV` only and rollup
 * drops them from the build.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const SOURCES = [".vue", ".ts", ".sass", ".scss", ".css"];

/** Mockup catalogs; the router registers them in development builds only. */
const NOT_SHIPPED = "pages/dev/";

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

const collect = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (
      SOURCES.some((ext) => name.endsWith(ext)) &&
      !name.endsWith(".spec.ts")
    ) {
      out.push(full);
    }
  }
  return out;
};

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

describe("the disclosure marker is drawn, not written", () => {
  const files = collect(CLIENT_SRC);

  it("reads the sources it is supposed to read", () => {
    // A walk that found nothing would pass every assertion below.
    expect(files.length).toBeGreaterThan(400);
  });

  it("leaves no marker in the markup of a shipped file", () => {
    const offenders: string[] = [];
    for (const file of files) {
      const path = asPath(file);
      if (path.startsWith(NOT_SHIPPED) || path in ALLOWED) continue;
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

  it("keeps drawing the marker it took out, and keeps turning it", () => {
    for (const [path, reason] of Object.entries(ALLOWED)) {
      const source = readFileSync(join(CLIENT_SRC, path), "utf8");
      expect(source, `${path}: ${reason}`).toMatch(
        /\.expand-marker\n {2}&::before\n {4}content: "\\25B6"/,
      );
      // The former right-to-down glyph swap, now one quarter turn.
      expect(source, `${path}: ${reason}`).toMatch(
        /&\.expanded::before\n {4}transform: rotate\(90deg\)/,
      );
    }
  });
});
