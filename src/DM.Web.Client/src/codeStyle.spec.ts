/**
 * @vitest-environment node
 */

/**
 * CODE_STYLE fixes one spelling for the ellipsis in interface text: three dots.
 * The single exception is the truncation marker — the sign that a long string
 * was cut (a search snippet, a preview) — which keeps U+2026, because "the text
 * breaks off here" and "a pause in speech" are not the same sign.
 *
 * Both spellings were in the tree at once, for the same state: one moderation
 * section rendered "Загрузка..." while the section next to it rendered the
 * U+2026 form. Beyond the mismatch on screen it cost search — a user who copies
 * the visible label and puts it in Ctrl+F finds nothing.
 *
 * The check is over sources rather than over a rendered app: the strings live
 * in templates, placeholder attributes and computed labels, and no runtime
 * surface reaches all of them at once.
 *
 * Comments are blanked out first, so an English aside that writes a range as
 * [a … b] or quotes a topic title is not a violation. Spec files are skipped
 * for the same reason: a test may quote the wrong form in order to assert on
 * it.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");

const ELLIPSIS = "\u2026";

/**
 * The exception, spelled out: files allowed to keep U+2026, and what for. A
 * name earns its place here only by appending the marker to text it cut.
 */
const TRUNCATION_MARKERS: Record<string, string> = {
  "src/DM.Infrastructure.Persistence/Repositories/Search/SearchSnippet.cs":
    "appends the marker to a snippet cut at the character budget",
};

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage", "bin", "obj"]);

/**
 * Blanks out comments, keeping every newline, so the line number in a failure
 * still points at the source line.
 *
 * A `//` opens a comment only outside a string literal and only when it does
 * not follow a colon — that leaves `https://` alone both in markup and inside
 * string constants.
 */
const stripComments = (source: string): string => {
  const blank = (match: string) => match.replace(/[^\n]/g, " ");
  return source
    .replace(/<!--[\s\S]*?-->/g, blank)
    .replace(/\/\*[\s\S]*?\*\//g, blank)
    .split("\n")
    .map((line) => {
      for (let i = 0; i + 1 < line.length; i++) {
        if (line[i] !== "/" || line[i + 1] !== "/") continue;
        if (i > 0 && line[i - 1] === ":") continue;
        const before = line.slice(0, i);
        const unclosed = (quote: string) =>
          (before.split(quote).length - 1) % 2 === 1;
        if (unclosed('"') || unclosed("'") || unclosed("`")) continue;
        return before;
      }
      return line;
    })
    .join("\n");
};

const collect = (dir: string, wanted: (name: string) => boolean): string[] => {
  const found: string[] = [];
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) found.push(...collect(full, wanted));
    else if (wanted(name)) found.push(full);
  }
  return found;
};

const violationsIn = (files: string[]): string[] => {
  const violations: string[] = [];
  for (const file of files) {
    const path = relative(REPO_ROOT, file).split("\\").join("/");
    if (path in TRUNCATION_MARKERS) continue;
    const source = readFileSync(file, "utf8");
    if (!source.includes(ELLIPSIS)) continue;
    stripComments(source)
      .split("\n")
      .forEach((line, index) => {
        if (line.includes(ELLIPSIS)) {
          violations.push(`${path}:${index + 1}: ${line.trim()}`);
        }
      });
  }
  return violations;
};

describe("ellipsis in interface text is three dots", () => {
  it("holds across the client sources", () => {
    const files = collect(
      join(CLIENT_ROOT, "src"),
      (name) =>
        (name.endsWith(".vue") || name.endsWith(".ts")) &&
        !name.endsWith(".spec.ts") &&
        !name.endsWith(".test.ts"),
    );
    // A walk that finds nothing would pass silently.
    expect(files.length).toBeGreaterThan(100);
    expect(violationsIn(files)).toEqual([]);
  });

  it("holds across the server sources, outside the truncation markers", () => {
    const files = collect(join(REPO_ROOT, "src"), (name) =>
      name.endsWith(".cs"),
    );
    expect(files.length).toBeGreaterThan(100);
    expect(violationsIn(files)).toEqual([]);
  });

  it("keeps every allowed truncation marker in use", () => {
    for (const [path, reason] of Object.entries(TRUNCATION_MARKERS)) {
      const source = stripComments(readFileSync(join(REPO_ROOT, path), "utf8"));
      expect(source, `${path}: ${reason}`).toContain(ELLIPSIS);
    }
  });
});
