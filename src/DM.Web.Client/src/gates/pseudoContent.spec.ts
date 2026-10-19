/**
 * @vitest-environment node
 */

/**
 * A line reads the same on the page and in the clipboard. A pseudo-element is
 * the one thing on screen a selection cannot reach, so a character that carries
 * meaning (a separator, a label, a bullet) is a text node, and only a drawing
 * lives in pseudo content; disclosureMarker.spec.ts reads the same border from
 * the other side. Text is whatever puts characters on the screen: a non-empty
 * quoted string, `attr()`, a counter, a quote keyword. Read off the sources,
 * since pseudo content leaves no node behind.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { parse as parseSfc } from "vue/compiler-sfc";
import { asPath, styleFiles } from "@/shared/lib/testing/sourceTree";

/** Pseudo content that stays, and what each one is for. */
const ALLOWED: Record<string, string> = {
  "assets/styles/Reset.sass .expand-marker &::before":
    "the disclosure triangle, a drawing that disclosureMarker.spec.ts requires to live here and not in the markup",
  "shared/ui/MonthYearPicker/MonthYearPicker.vue .myp-trigger-section::before":
    "an invisible copy of the widest label, holding the width; what a reader sees and copies is the text node painted over it",
  "shared/ui/BBCodeEditor/BBCodeEditor.vue .wysiwyg-content :deep(.tiptap) p.is-editor-empty:first-child::before":
    "the editor placeholder, here for the same reason as the disclosure triangle: it must not reach a copy. A text node would be a node of the document being edited, so it would be typed over, selected and saved as content; and by the time there is anything to copy the hint is already gone",
};

/** A declaration inside one of these is pseudo content. */
const PSEUDO = /::?(?:before|after)\b/;

/** A `content` declaration, in indented Sass or in plain CSS. */
const CONTENT = /(?:^|[\s;{])content\s*:/;

type Declared = { chain: string[]; value: string; line: number };
type Source = { lines: string[]; firstLine: number; indented: boolean };

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
  const files = styleFiles();

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
