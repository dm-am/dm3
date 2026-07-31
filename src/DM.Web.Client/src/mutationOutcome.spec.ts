/**
 * @vitest-environment node
 */

/**
 * `Api` never throws: shared/api/client.ts catches the failure and resolves
 * with `{ data: null, error }`. So `await api.deleteComment(id)` on a line of
 * its own does not mean "deleted" — it means "asked", and the code after it
 * runs on a 403 exactly as it runs on a 204.
 *
 * That is how four stores came to strike a comment through on a refusal, and
 * how two chat pages came to close an editor over text the server had
 * rejected: the reader was shown the outcome of a request that never
 * happened, sometimes with the refusal toast still on screen beside it.
 *
 * This checks the narrow, mechanical half of the rule: the answer to a
 * comment or message edit or delete is never discarded. Reading the answer is
 * not yet handling it — the store and component specs are what hold that — but
 * a discarded answer cannot be handled at all, and a discarded answer is the
 * shape the defect took in all fourteen places at once.
 *
 * Method names rather than modules: the same call is made on an api object
 * inside a store and on the store inside a page, and both halves were broken.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

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
  const where = relative(CLIENT_SRC, file).split("\\").join("/");
  const hit = (line: string) => `${where}: ${line}`;

  if (file.endsWith(".ts")) return discardedIn(raw, file).map(hit);

  const { descriptor } = parseSfc(raw, { filename: file });
  return [descriptor.script, descriptor.scriptSetup].flatMap((block) =>
    block ? discardedIn(block.content, file).map(hit) : [],
  );
}

describe("comment and message mutations", () => {
  it("never discard what the server answered", () => {
    const files = collectFiles(CLIENT_SRC);
    // A walk that finds nothing would pass silently.
    expect(files.length).toBeGreaterThan(100);
    expect(files.flatMap(offendersIn)).toEqual([]);
  });
});
