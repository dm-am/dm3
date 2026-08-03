/**
 * @vitest-environment node
 */

/**
 * The tab title has one format and one place that builds it — and it had
 * neither. The formatter glued the brand with an em dash, which is out of
 * interface copy; five profile subpages glued a second segment with a second
 * em dash ("Полученные оценки постов — TestUser — Dungeon Master"), so the
 * name that tells two tabs apart stood in the middle of 58 characters and was
 * the first thing a narrow tab cut off.
 *
 * The unit part pins the format. The scan part is what keeps it: a page that
 * composes its own title is invisible to the route table, so the rule is read
 * off the sources — every `useDocumentTitle(...)` argument and every
 * `joinTitleSegments(...)` call under the client tree.
 *
 * Only literals are read, through the TypeScript AST (the idiom of
 * shared/lib/copy-rules.spec.ts): an expression is opaque and this check does
 * not judge what it cannot see.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import {
  TITLE_SEPARATOR,
  formatDocumentTitle,
  joinTitleSegments,
} from "./useDocumentTitle";

const HERE = dirname(fileURLToPath(import.meta.url));
// composables -> lib -> shared -> src
const CLIENT_SRC = resolve(HERE, "..", "..", "..");

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** The only two files allowed to assign `document.title`. */
const TITLE_WRITERS = [
  "app/providers/router.ts",
  "shared/lib/composables/useDocumentTitle.ts",
];

const EM_DASH = "\u2014";
const YO = "\u0451";
const BRAND = formatDocumentTitle("");

function collectFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collectFiles(full, out);
    } else if (
      (full.endsWith(".ts") || full.endsWith(".vue")) &&
      !full.endsWith(".spec.ts") &&
      !full.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

const where = (file: string) =>
  relative(CLIENT_SRC, file).split("\\").join("/");

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
  const files = collectFiles(CLIENT_SRC);

  it("keeps the forbidden signs out of every composed title", () => {
    const forbidden: Array<[string, string]> = [
      [EM_DASH, "em dash"],
      [";", "semicolon"],
      [YO, "the letter yo"],
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

    // A walk that finds nothing would pass silently.
    expect(calls).toBeGreaterThan(10);
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
          // A literal first segment means a fixed section name stands in front
          // of the entity — the shape that made five profile tabs read
          // "Полученные оценки постов" right up to the truncation.
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
