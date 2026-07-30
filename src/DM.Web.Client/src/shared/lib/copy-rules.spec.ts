/**
 * @vitest-environment node
 */

/**
 * UI_STANDARDS forbids the middle dot as a separator in interface copy:
 * navigation strips use " | ", and a qualifier attached to a value goes in
 * parentheses — the dice chip in GameRoom reads "2d6 +1 (взрыв 2) (скрытый)",
 * both qualifiers in the same form. The rule was manual, and the one violation
 * it had was found by reading the code rather than by a check.
 *
 * This test is that check, and it is deliberately narrow:
 *   - only .vue and .ts under the client source tree — not docs, not styles;
 *   - in .ts files and in <script> blocks only string and template literals are
 *     read, through the TypeScript AST, so the character inside a code comment
 *     or an identifier is not a failure;
 *   - in <template> HTML comments are stripped first, for the same reason.
 *
 * A middle dot outside a literal is not copy and is not this test's business.
 * Inside one it is copy until proven otherwise, and the tree holds none.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";

/** U+00B7 MIDDLE DOT, spelled by code point so this file stays clean itself. */
const MIDDOT = "\u00B7";

const HERE = dirname(fileURLToPath(import.meta.url));
// lib -> shared -> src
const CLIENT_SRC = resolve(HERE, "..", "..");

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

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

/** Text of every string and template literal; comments and names excluded. */
function stringLiterals(code: string, fileName: string): string[] {
  const source = ts.createSourceFile(
    fileName,
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const found: string[] = [];
  const visit = (node: ts.Node): void => {
    if (
      ts.isStringLiteral(node) ||
      ts.isNoSubstitutionTemplateLiteral(node) ||
      ts.isTemplateHead(node) ||
      ts.isTemplateMiddle(node) ||
      ts.isTemplateTail(node)
    ) {
      found.push(node.text);
    }
    ts.forEachChild(node, visit);
  };
  ts.forEachChild(source, visit);
  return found;
}

function offendersIn(file: string): string[] {
  const raw = readFileSync(file, "utf8");
  // Cheap reject: almost no file contains the character at all.
  if (!raw.includes(MIDDOT)) return [];

  const where = relative(CLIENT_SRC, file).split("\\").join("/");
  const hits: string[] = [];

  if (file.endsWith(".ts")) {
    if (stringLiterals(raw, file).some((text) => text.includes(MIDDOT))) {
      hits.push(`${where} (string literal)`);
    }
    return hits;
  }

  const { descriptor } = parseSfc(raw, { filename: file });
  const template = (descriptor.template?.content ?? "").replace(
    /<!--[\s\S]*?-->/g,
    "",
  );
  if (template.includes(MIDDOT)) hits.push(`${where} (template text)`);

  for (const block of [descriptor.script, descriptor.scriptSetup]) {
    if (!block) continue;
    if (stringLiterals(block.content, file).some((t) => t.includes(MIDDOT))) {
      hits.push(`${where} (script literal)`);
    }
  }
  return hits;
}

describe("interface copy", () => {
  it("never uses the middle dot in a user-visible string", () => {
    const offenders = collectFiles(CLIENT_SRC).flatMap(offendersIn);
    expect(offenders).toEqual([]);
  });
});
