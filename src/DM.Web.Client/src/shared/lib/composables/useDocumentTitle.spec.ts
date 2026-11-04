/**
 * @vitest-environment node
 */

/**
 * The tab title has one format and one place that builds it. A page composing
 * its own title is invisible to the route table, so the literals of every
 * useDocumentTitle(...) and joinTitleSegments(...) call are read off the
 * sources through the TypeScript AST.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import {
  TITLE_SEPARATOR,
  formatDocumentTitle,
  joinTitleSegments,
} from "./useDocumentTitle";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** The only two files allowed to assign `document.title`. */
const TITLE_WRITERS = [
  "app/providers/router.ts",
  "shared/lib/composables/useDocumentTitle.ts",
];

const EM_DASH = "\u2014";
const BRAND = formatDocumentTitle("");

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

const where = asPath;

/** Script sources of a file: the .ts itself, or the SFC script blocks. */
function scriptsOf(file: string, raw: string): string[] {
  if (file.endsWith(".ts")) return [raw];
  const { descriptor } = parseSfc(raw, { filename: file });
  return [descriptor.script?.content, descriptor.scriptSetup?.content].filter(
    (content): content is string => !!content,
  );
}

const parseTs = (code: string, fileName: string): ts.SourceFile =>
  ts.createSourceFile(fileName, code, ts.ScriptTarget.Latest, true);

/** Every call of `name(...)` in the source. */
function callsTo(source: ts.SourceFile, name: string): ts.CallExpression[] {
  const found: ts.CallExpression[] = [];
  const visit = (node: ts.Node): void => {
    if (
      ts.isCallExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === name
    ) {
      found.push(node);
    }
    ts.forEachChild(node, visit);
  };
  ts.forEachChild(source, visit);
  return found;
}

const isLiteral = (node: ts.Node): boolean =>
  ts.isStringLiteral(node) ||
  ts.isNoSubstitutionTemplateLiteral(node) ||
  ts.isTemplateExpression(node);

/** Text of every string and template literal under a node. */
function literalsUnder(node: ts.Node): string[] {
  const found: string[] = [];
  const visit = (current: ts.Node): void => {
    if (
      ts.isStringLiteral(current) ||
      ts.isNoSubstitutionTemplateLiteral(current) ||
      ts.isTemplateHead(current) ||
      ts.isTemplateMiddle(current) ||
      ts.isTemplateTail(current)
    ) {
      found.push(current.text);
    }
    ts.forEachChild(current, visit);
  };
  visit(node);
  return found;
}

describe("formatDocumentTitle", () => {
  it("puts the page first and the brand behind one separator", () => {
    expect(formatDocumentTitle("Игры")).toBe("Игры | Dungeon Master");
  });

  it("falls back to the brand alone on an empty title", () => {
    expect(formatDocumentTitle("   ")).toBe("Dungeon Master");
    expect(formatDocumentTitle(null)).toBe("Dungeon Master");
  });

  it("never emits an em dash", () => {
    expect(formatDocumentTitle("Игры")).not.toContain(EM_DASH);
    expect(TITLE_SEPARATOR).not.toContain(EM_DASH);
  });
});

describe("joinTitleSegments", () => {
  it("joins with the one separator", () => {
    expect(joinTitleSegments("Хроники", "Комнаты")).toBe("Хроники | Комнаты");
  });

  it("drops empty segments instead of leaving a dangling separator", () => {
    expect(joinTitleSegments("Хроники", null, undefined, "  ")).toBe("Хроники");
    expect(joinTitleSegments(null, "Комнаты")).toBe("Комнаты");
    expect(joinTitleSegments(null, undefined)).toBe("");
  });
});

describe("titles across the client sources", () => {
  const files = collect(CLIENT_SRC, isSource);

  it("keeps the forbidden signs out of every composed title", () => {
    const forbidden: Array<[string, string]> = [
      [EM_DASH, "em dash"],
      [";", "semicolon"],
      [TITLE_SEPARATOR, "a hand-glued separator"],
      [BRAND, "the brand"],
    ];
    const offenders: string[] = [];
    let calls = 0;

    for (const file of files) {
      const raw = readFileSync(file, "utf8");
      if (!raw.includes("useDocumentTitle(")) continue;
      for (const code of scriptsOf(file, raw)) {
        for (const call of callsTo(parseTs(code, file), "useDocumentTitle")) {
          calls += 1;
          for (const text of call.arguments.flatMap(literalsUnder)) {
            for (const [sign, what] of forbidden) {
              if (text.includes(sign)) {
                offenders.push(`${where(file)} "${text}": ${what}`);
              }
            }
          }
        }
      }
    }

    // A walk that finds nothing passes; the floor is low, since the count falls
    // as pages leave their title to the shell.
    expect(calls).toBeGreaterThan(5);
    expect(offenders).toEqual([]);
  });

  it("puts the distinguishing segment first in every composition", () => {
    const offenders: string[] = [];
    let calls = 0;

    for (const file of files) {
      const raw = readFileSync(file, "utf8");
      if (!raw.includes("joinTitleSegments(")) continue;
      for (const code of scriptsOf(file, raw)) {
        for (const call of callsTo(parseTs(code, file), "joinTitleSegments")) {
          calls += 1;
          const first = call.arguments[0];
          // A literal first segment puts a fixed section before the entity.
          if (first && isLiteral(first)) {
            offenders.push(`${where(file)}: ${first.getText()}`);
          }
        }
      }
    }

    expect(calls).toBeGreaterThan(5);
    expect(offenders).toEqual([]);
  });

  it("assigns document.title only where the format lives", () => {
    const writers = files
      .filter((file) =>
        /document\.title\s*=[^=]/.test(readFileSync(file, "utf8")),
      )
      .map(where)
      .sort();

    expect(writers).toEqual(TITLE_WRITERS);
  });
});
