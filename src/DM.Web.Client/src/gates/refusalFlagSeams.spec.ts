/**
 * @vitest-environment node
 */

/**
 * A window that prints a refusal in itself sends its request with
 * `ownsRefusal` and `ownsRateLimit`, so the interceptor keeps quiet about a
 * 403, a 429 and a 5xx (UI_STANDARDS, "Всплывающие сообщения"). Between some
 * windows and the api stands a seam the page writes by hand (NotepadAdapter,
 * the api handed to createCommentSection), and an arrow that takes only the id
 * still type-checks against it. This checks that such an arrow takes the
 * second argument and hands it on.
 */
import { describe, it, expect, vi } from "vitest";
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, collect, asPath } from "@/shared/lib/testing/sourceTree";

// A whole-tree AST scan outruns the default when the suite saturates every core.
vi.setConfig({ testTimeout: 60_000 });

/** A seam typed by an annotation, and the members of it that carry the flags. */
const TYPED_SEAMS: Record<string, string[]> = {
  NotepadAdapter: ["remove"],
};

/** A seam handed to a factory, and the members of it that carry the flags. */
const FACTORY_SEAMS: Record<string, string[]> = {
  createCommentSection: ["deleteComment"],
};

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

/**
 * Whether the function takes a second parameter and passes it, by name, to a
 * call in its body. A body that names it anywhere else (a spread, a guard)
 * does not count: the flags have to reach a request.
 */
function forwardsFlags(fn: ts.Node): boolean {
  if (!ts.isArrowFunction(fn) && !ts.isFunctionExpression(fn)) return false;
  const second = fn.parameters[1];
  if (!second || !ts.isIdentifier(second.name)) return false;
  const name = second.name.text;

  let passed = false;
  const visit = (node: ts.Node): void => {
    if (
      ts.isCallExpression(node) &&
      node.arguments.some((arg) => ts.isIdentifier(arg) && arg.text === name)
    ) {
      passed = true;
    }
    ts.forEachChild(node, visit);
  };
  visit(fn.body);
  return passed;
}

interface Found {
  /** Every seam member met, so a scan that finds nothing is caught. */
  seen: number;
  /** The members that drop the flags. */
  dropped: string[];
}

function scan(code: string, fileName: string, where: string): Found {
  const source = ts.createSourceFile(
    fileName,
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const found: Found = { seen: 0, dropped: [] };

  const check = (literal: ts.Expression, members: string[]) => {
    if (!ts.isObjectLiteralExpression(literal)) return;
    for (const property of literal.properties) {
      if (!property.name || !ts.isIdentifier(property.name)) continue;
      if (!members.includes(property.name.text)) continue;
      found.seen++;
      const value = ts.isPropertyAssignment(property)
        ? property.initializer
        : property;
      if (!forwardsFlags(value)) {
        found.dropped.push(
          `${where}: ${property.getText().split("\n")[0].trim()}`,
        );
      }
    }
  };

  const visit = (node: ts.Node): void => {
    if (
      ts.isVariableDeclaration(node) &&
      node.type &&
      ts.isTypeReferenceNode(node.type) &&
      ts.isIdentifier(node.type.typeName) &&
      node.initializer
    ) {
      const members = TYPED_SEAMS[node.type.typeName.text];
      if (members) check(node.initializer, members);
    }
    if (
      ts.isCallExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.arguments[0]
    ) {
      const members = FACTORY_SEAMS[node.expression.text];
      if (members) check(node.arguments[0], members);
    }
    ts.forEachChild(node, visit);
  };
  ts.forEachChild(source, visit);
  return found;
}

function scanFile(file: string): Found {
  const raw = readFileSync(file, "utf8");
  const where = asPath(file);
  if (file.endsWith(".ts")) return scan(raw, file, where);

  const { descriptor } = parseSfc(raw, { filename: file });
  return [descriptor.script, descriptor.scriptSetup]
    .filter((block) => block !== null)
    .map((block) => scan(block.content, file, where))
    .reduce(
      (all, one) => ({
        seen: all.seen + one.seen,
        dropped: [...all.dropped, ...one.dropped],
      }),
      { seen: 0, dropped: [] as string[] },
    );
}

describe("the seams between a window and the api", () => {
  it("hand the window's refusal flags on to the request", () => {
    const files = collect(CLIENT_SRC, isSource);
    const results = files.map(scanFile);

    // A scan that meets no seam would pass silently.
    expect(results.reduce((sum, r) => sum + r.seen, 0)).toBeGreaterThanOrEqual(
      5,
    );
    expect(results.flatMap((r) => r.dropped)).toEqual([]);
  });
});
