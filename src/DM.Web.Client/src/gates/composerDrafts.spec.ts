/**
 * @vitest-environment node
 */

/**
 * One rule over every composer on the site: the editor's clear() may not run
 * before the request it belongs to has been answered. clear() empties the
 * editor and deletes the saved draft (`bbcode_draft_*`), so called before the
 * request it hands a failed send the power to destroy what was written.
 * Emptying the bound model before the request stays allowed: the page hands
 * the text back on failure, and the draft is the copy it cannot hand back.
 *
 * The check reads the AST: only a call on an editor handle
 * (`<ref>.value?.clear()`) before the first await of its own function is a
 * violation. It walks .ts alongside both script blocks of a .vue, since a
 * composer may send through a shared composable. Spec files are skipped.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const isComposerSource = (name: string): boolean =>
  name.endsWith(".vue") || (name.endsWith(".ts") && !name.endsWith(".spec.ts"));

const isFunctionLike = (node: ts.Node): boolean =>
  ts.isFunctionDeclaration(node) ||
  ts.isFunctionExpression(node) ||
  ts.isArrowFunction(node) ||
  ts.isMethodDeclaration(node);

/** `<ref>.value?.clear()` — the editor handle's own reset, draft included. */
const isEditorClear = (node: ts.Node): boolean =>
  ts.isCallExpression(node) &&
  /\.value\??\.clear$/.test(node.expression.getText());

interface Inspected {
  clears: number;
  offenders: string[];
}

/** Every editor clear() in the file, and the offending ones among them. */
function inspectFile(file: string): Inspected {
  const raw = readFileSync(file, "utf8");
  if (!raw.includes(".clear()")) return { clears: 0, offenders: [] };
  if (!file.endsWith(".vue")) return inspectScript(file, raw, 0);

  // A .vue file's scripts are blocks inside it, and both are read: a component
  // may keep its composable in the plain `<script>` beside `<script setup>`.
  // Reported lines are offset by where each block starts.
  const { descriptor } = parseSfc(raw, { filename: file });
  return [descriptor.script, descriptor.scriptSetup]
    .flatMap((block) =>
      block ? [inspectScript(file, block.content, block.loc.start.line)] : [],
    )
    .reduce<Inspected>(
      (all, one) => ({
        clears: all.clears + one.clears,
        offenders: [...all.offenders, ...one.offenders],
      }),
      { clears: 0, offenders: [] },
    );
}

/** One script of the file, starting at `scriptStartLine` (0 for a .ts file). */
function inspectScript(
  file: string,
  content: string,
  scriptStartLine: number,
): Inspected {
  const source = ts.createSourceFile(
    file,
    content,
    ts.ScriptTarget.Latest,
    true,
  );
  const where = asPath(file);
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
        source.getLineAndCharacterOfPosition(clear).line + scriptStartLine + 1;
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
  const files = collect(CLIENT_SRC, isComposerSource);
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
