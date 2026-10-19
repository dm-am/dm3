/**
 * @vitest-environment node
 */

/**
 * `Api` never throws: shared/api/client.ts resolves with `{ data: null, error }`,
 * so `await api.deleteComment(id)` on a line of its own means "asked", not
 * "deleted", and the code after it runs on a 403 as on a 204. This checks the
 * mechanical half of the rule: the answer to a comment or message edit or
 * delete is never discarded. Method names rather than modules: the same call is
 * made on an api object inside a store and on the store inside a page.
 */
import { describe, it, expect, vi } from "vitest";

// A whole-tree AST scan outruns the default when the suite saturates every core.
vi.setConfig({ testTimeout: 60_000 });
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, collect, asPath } from "@/shared/lib/testing/sourceTree";

const GUARDED = new Set([
  "updateComment",
  "deleteComment",
  "updateGameComment",
  "deleteGameComment",
  "updateBlogComment",
  "deleteBlogComment",
  "updateMessage",
  "deleteMessage",
]);

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

/**
 * `await x.deleteComment(...)` standing alone as a statement: the result has
 * nowhere to go. Destructuring it, returning it or assigning it all take the
 * call out of statement position, which is exactly the distinction wanted.
 */
function discardedIn(code: string, fileName: string): string[] {
  const source = ts.createSourceFile(
    fileName,
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const found: string[] = [];
  const visit = (node: ts.Node): void => {
    if (
      ts.isExpressionStatement(node) &&
      ts.isAwaitExpression(node.expression) &&
      ts.isCallExpression(node.expression.expression)
    ) {
      const callee = node.expression.expression.expression;
      if (
        ts.isPropertyAccessExpression(callee) &&
        GUARDED.has(callee.name.text)
      ) {
        found.push(node.getText().split("\n")[0].trim());
      }
    }
    ts.forEachChild(node, visit);
  };
  ts.forEachChild(source, visit);
  return found;
}

function offendersIn(file: string): string[] {
  const raw = readFileSync(file, "utf8");
  const where = asPath(file);
  const hit = (line: string) => `${where}: ${line}`;

  if (file.endsWith(".ts")) return discardedIn(raw, file).map(hit);

  const { descriptor } = parseSfc(raw, { filename: file });
  return [descriptor.script, descriptor.scriptSetup].flatMap((block) =>
    block ? discardedIn(block.content, file).map(hit) : [],
  );
}

describe("comment and message mutations", () => {
  it("never discard what the server answered", () => {
    const files = collect(CLIENT_SRC, isSource);
    // A walk that finds nothing would pass silently.
    expect(files.length).toBeGreaterThan(100);
    expect(files.flatMap(offendersIn)).toEqual([]);
  });
});
