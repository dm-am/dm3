/**
 * @vitest-environment node
 */

/**
 * One rule over every composer on the site: the editor's clear() may not run
 * before the request it belongs to has been answered.
 *
 * clear() does two things — it empties the editor and it deletes the saved
 * draft (localStorage, `bbcode_draft_*`). Called before the request, it hands
 * a failed send the power to destroy what was written: the field is empty
 * because the page emptied it, and the copy that would have survived a closed
 * tab is gone with it. Five composers did exactly that, and in the messenger,
 * the one that also never put the text back, a refused send left nothing at all.
 *
 * Emptying the bound model before the request stays allowed and is the point:
 * that is what makes sending feel instant, and the page hands the text back on
 * failure. The draft is the copy it cannot hand back.
 *
 * The check reads the AST rather than the text: `.clear()` is a common enough
 * name, and only a call on an editor handle (`<ref>.value?.clear()`) placed
 * before the first await of its own function is a violation.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This file sits at the root of the client sources it walks. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const collect = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) collect(full, out);
    else if (full.endsWith(".vue")) out.push(full);
  }
  return out;
};

const isFunctionLike = (node: ts.Node): boolean =>
  ts.isFunctionDeclaration(node) ||
  ts.isFunctionExpression(node) ||
  ts.isArrowFunction(node) ||
  ts.isMethodDeclaration(node);

/** `<ref>.value?.clear()` — the editor handle's own reset, draft included. */
const isEditorClear = (node: ts.Node): boolean =>
  ts.isCallExpression(node) &&
  /\.value\??\.clear$/.test(node.expression.getText());

/** Every editor clear() in the file, and the offending ones among them. */
function inspectFile(file: string): { clears: number; offenders: string[] } {
  const raw = readFileSync(file, "utf8");
  if (!raw.includes(".clear()")) return { clears: 0, offenders: [] };

  const { descriptor } = parseSfc(raw, { filename: file });
  const script = descriptor.scriptSetup ?? descriptor.script;
  if (!script) return { clears: 0, offenders: [] };

  const source = ts.createSourceFile(
    file,
    script.content,
    ts.ScriptTarget.Latest,
    true,
  );
  const where = relative(CLIENT_SRC, file).split("\\").join("/");
  const offenders: string[] = [];
  let clears = 0;

  const inspect = (fn: ts.Node): void => {
    const awaits: number[] = [];
    const found: number[] = [];
    const walk = (node: ts.Node): void => {
      // A nested function is its own unit: its awaits are not this one's.
      if (node !== fn && isFunctionLike(node)) return inspect(node);
      if (ts.isAwaitExpression(node)) awaits.push(node.pos);
      if (isEditorClear(node)) found.push(node.pos);
      ts.forEachChild(node, walk);
    };
    ts.forEachChild(fn, walk);
    clears += found.length;
    if (!awaits.length || !found.length) return;

    const firstAwait = Math.min(...awaits);
    for (const clear of found.filter((pos) => pos < firstAwait)) {
      const line =
        source.getLineAndCharacterOfPosition(clear).line +
        script.loc.start.line +
        1;
      offenders.push(`${where}:${line}`);
    }
  };

  const top = (node: ts.Node): void => {
    if (isFunctionLike(node)) inspect(node);
    else ts.forEachChild(node, top);
  };
  ts.forEachChild(source, top);
  return { clears, offenders };
}

describe("composers keep the draft until the send lands", () => {
  const files = collect(CLIENT_SRC);
  const inspected = files.map(inspectFile);

  it("clears no editor before the first await of its handler", () => {
    // A walk that finds nothing would pass silently.
    expect(files.length).toBeGreaterThan(50);
    expect(inspected.flatMap((result) => result.offenders)).toEqual([]);
  });

  it("still recognises the call it is written about", () => {
    // The rule is spelled as a shape (`<ref>.value?.clear()`). Rename the
    // exposed method or the ref and the check above would keep passing over
    // nothing, so it also asserts the shape is still in the tree.
    const clears = inspected.reduce((sum, result) => sum + result.clears, 0);
    expect(clears).toBeGreaterThanOrEqual(6);
  });
});
