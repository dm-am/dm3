/**
 * @vitest-environment node
 */

/**
 * A line reads the same on the page and in the clipboard. The one thing on
 * screen a selection cannot reach is a pseudo-element: `::before` and
 * `::after` content is not in the document, and `Range.toString()` never
 * emits it. So a character that carries meaning — a separator between two
 * values, a label, a bullet — is a text node, and only a drawing lives in
 * pseudo content.
 *
 * disclosureMarker.spec.ts is the same border read from the other side: the
 * triangle in front of a row that opens is a drawing, and it is required to
 * live in pseudo content precisely so a reader who copies the row does not
 * get it. Here the rule is read the other way, over every pseudo-element in
 * the client.
 *
 * Text is whatever puts characters on the screen: a non-empty quoted string,
 * `attr()`, a counter, a quote keyword. The empty string a rule draws its box
 * with, `none` and `url()` put no characters anywhere and are not a violation.
 *
 * The check reads sources rather than a rendered page for the same reason the
 * rule exists: pseudo content leaves no node behind, so what it does to a copy
 * cannot be seen in the DOM it produced.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

/**
 * Pseudo content that stays, and what each one is for. The bullets are the
 * one place the rule is still broken: they become the " | " the design
 * language spells a separator with, which is a visible edit the owner has not
 * ruled on yet, and the day he does their two entries go with them.
 */
const ALLOWED: Record<string, string> = {
  "assets/styles/Reset.sass .expand-marker &::before":
    "the disclosure triangle, a drawing that disclosureMarker.spec.ts requires to live here and not in the markup",
  "shared/ui/MonthYearPicker/MonthYearPicker.vue .myp-trigger-section::before":
    "an invisible copy of the widest label, holding the width; what a reader sees and copies is the text node painted over it",
  "pages/account/sections/AccountSecurityHistorySection.vue .event-device &::after":
    "a bullet between two values of the security line, awaiting its replacement by a separator a reader can copy",
  "pages/account/sections/AccountSecurityHistorySection.vue .event-ip &::after":
    "the same bullet, one value further along the same line",
};

/** A declaration inside one of these is pseudo content. */
const PSEUDO = /::?(?:before|after)\b/;

/** A `content` declaration, in indented Sass or in plain CSS. */
const CONTENT = /(?:^|[\s;{])content\s*:/;

type Declared = { chain: string[]; value: string; line: number };
type Source = { lines: string[]; firstLine: number; indented: boolean };

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

/** Style sources of a file: the file itself, or the blocks of an SFC. */
function styleSources(file: string): Source[] {
  const raw = readFileSync(file, "utf8");
  if (!file.endsWith(".vue")) {
    return [
      {
        lines: raw.split("\n"),
        firstLine: 1,
        indented: file.endsWith(".sass"),
      },
    ];
  }
  const { descriptor } = parseSfc(raw, { filename: file });
  return descriptor.styles.map((style) => ({
    lines: style.content.split("\n"),
    firstLine: style.loc.start.line,
    indented: style.lang === "sass",
  }));
}

/** Every `content` declaration of one indented-Sass source, with its nesting. */
function contentDeclarations(lines: string[], firstLine: number): Declared[] {
  const found: Declared[] = [];
  const stack: { indent: number; selector: string }[] = [];
  let group: string[] = [];
  let groupIndent = 0;

  lines.forEach((raw, index) => {
    const text = raw.trim();
    if (!text || /^(\/\/|\/\*|\*)/.test(text)) return;
    const indent = raw.length - raw.trimStart().length;
    while (stack.length && stack[stack.length - 1].indent >= indent)
      stack.pop();

    // `prop: value` always has whitespace after the colon in Sass, which is
    // what separates it from `&:hover`, `a:hover` and `:global(...)`.
    const declaration = /^([a-z-]+):[ \t]+(\S.*)$/.exec(text);
    if (declaration && !text.endsWith(",")) {
      if (declaration[1] === "content") {
        found.push({
          chain: stack.map((entry) => entry.selector),
          value: declaration[2].trim(),
          line: firstLine + index,
        });
      }
      return;
    }

    // A selector group spans lines: every line but the last ends in a comma.
    if (text.endsWith(",")) {
      if (!group.length) groupIndent = indent;
      group.push(text.slice(0, -1).trim());
      return;
    }
    const selector =
      group.length && groupIndent === indent
        ? [...group, text].join(", ")
        : text;
    group = [];
    stack.push({ indent, selector });
  });

  return found;
}

/** A value that puts characters on the screen. */
function writesText(value: string): boolean {
  const bare = value.replace(/\s*!important\s*$/, "").trim();
  if (/\b(?:attr|counter|counters)\s*\(/.test(bare)) return true;
  if (/(^|\s)(?:open-quote|close-quote)($|\s)/.test(bare)) return true;
  return (bare.match(/"[^"]*"|'[^']*'/g) ?? []).some((part) => part.length > 2);
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** Every pseudo-element in the tree that writes, keyed as ALLOWED keys it. */
function writingPseudos(files: string[]): Map<string, string> {
  const found = new Map<string, string>();
  for (const file of files) {
    const path = asPath(file);
    for (const { lines, firstLine, indented } of styleSources(file)) {
      if (!indented) continue;
      for (const { chain, value, line } of contentDeclarations(
        lines,
        firstLine,
      )) {
        if (!chain.some((selector) => PSEUDO.test(selector))) continue;
        if (!writesText(value)) continue;
        found.set(
          `${path} ${chain.join(" ")}`,
          `${path}:${line} content: ${value}`,
        );
      }
    }
  }
  return found;
}

describe("pseudo-element content", () => {
  const files = collect(CLIENT_SRC);

  it("reads the style sources it is supposed to read", () => {
    // A walk that found nothing would pass every assertion below.
    expect(files.length).toBeGreaterThan(200);

    // The reader below is written for indented Sass, which is every style
    // block in the client but one. A `content` declaration in the exception
    // would go past it unread, and the rule would hold only where it looked.
    const unread: string[] = [];
    for (const file of files) {
      for (const { lines, firstLine, indented } of styleSources(file)) {
        if (indented) continue;
        lines.forEach((line, index) => {
          if (CONTENT.test(line)) {
            unread.push(`${asPath(file)}:${firstLine + index}`);
          }
        });
      }
    }
    expect(unread).toEqual([]);
  });

  it("draws with a pseudo-element and never writes with one", () => {
    const offenders = [...writingPseudos(files)]
      .filter(([key]) => !(key in ALLOWED))
      .map(([, where]) => where);
    expect(offenders).toEqual([]);
  });

  it("keeps every allowance in use, so it cannot outlive its reason", () => {
    const writing = writingPseudos(files);
    for (const [key, reason] of Object.entries(ALLOWED)) {
      expect(writing.has(key), `${key}: ${reason}`).toBe(true);
    }
  });
});
