/**
 * @vitest-environment node
 */

/**
 * A draft key is built by shared/lib/utils/draftKey.ts, never spelled: keys
 * assembled from literals on each screen collide, and one game's comment box
 * opens with the text written for another. This check reads the template AST
 * of every .vue and looks at `draft-key` wherever it is bound: a literal is a
 * violation, and the only expressions accepted are a call to the builder and a
 * pass-through of the component's own declared prop.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** @vue/compiler-core NodeTypes — the members this walk reads. */
const ELEMENT = 1;
const SIMPLE_EXPRESSION = 4;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** The prop, in both spellings Vue accepts for it. */
const DRAFT_KEY = new Set(["draft-key", "draftKey"]);

const BUILDER = "composerDraftKey";

/** A literal attribute, or a `v-bind` whose argument names one. */
interface Prop {
  type: number;
  name: string;
  /** DIRECTIVE only: the part after the colon in `:draft-key`. */
  arg?: { type: number; content: string };
  /** DIRECTIVE only: the bound expression. */
  exp?: { type: number; content: string };
  loc: { start: { line: number } };
}

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  props?: Prop[];
  children?: Node[];
  loc: { start: { line: number } };
}

const isComponent = (name: string): boolean => name.endsWith(".vue");

const isDraftKey = (prop: Prop): boolean =>
  (prop.type === ATTRIBUTE && DRAFT_KEY.has(prop.name)) ||
  (prop.type === DIRECTIVE &&
    prop.name === "bind" &&
    prop.arg?.type === SIMPLE_EXPRESSION &&
    DRAFT_KEY.has(prop.arg.content));

/** The names `defineProps<{ ... }>()` declares in this script, if any. */
function declaredProps(script: string): Set<string> {
  const names = new Set<string>();
  const source = ts.createSourceFile(
    "script.ts",
    script,
    ts.ScriptTarget.Latest,
    true,
  );
  const visit = (node: ts.Node): void => {
    if (
      ts.isCallExpression(node) &&
      node.expression.getText() === "defineProps"
    ) {
      const declared = node.typeArguments?.[0];
      if (declared && ts.isTypeLiteralNode(declared)) {
        for (const member of declared.members) {
          if (member.name) names.add(member.name.getText());
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return names;
}

/** A call to the builder, with nothing wrapped around it. */
function callsBuilder(expression: string): boolean {
  const source = ts.createSourceFile(
    "expression.ts",
    expression,
    ts.ScriptTarget.Latest,
    true,
  );
  const [statement] = source.statements;
  if (!statement || !ts.isExpressionStatement(statement)) return false;
  const call = statement.expression;
  return ts.isCallExpression(call) && call.expression.getText() === BUILDER;
}

describe("every draft key comes from the builder", () => {
  const files = collect(CLIENT_SRC, isComponent);
  const offenders: string[] = [];
  let bound = 0;

  for (const file of files) {
    const raw = readFileSync(file, "utf8");
    if (!/draft-?[Kk]ey/.test(raw)) continue;

    const { descriptor } = parseSfc(raw, { filename: file });
    const ast = descriptor.template?.ast as unknown as Node | undefined;
    if (!ast) continue;
    // One script block is enough here, unlike the gates that scan scripts: the
    // violation is a binding in the template, which is read whole, and the
    // script only says which props defineProps declares, and that macro
    // stands in `<script setup>` alone.
    const script = descriptor.scriptSetup ?? descriptor.script;
    const props = declaredProps(script?.content ?? "");

    const visit = (node: Node): void => {
      for (const prop of node.type === ELEMENT ? (node.props ?? []) : []) {
        if (!isDraftKey(prop)) continue;
        bound += 1;
        const expression =
          prop.type === DIRECTIVE ? (prop.exp?.content.trim() ?? "") : null;
        if (
          expression === null ||
          !(callsBuilder(expression) || props.has(expression))
        ) {
          offenders.push(`${asPath(file)}:${prop.loc.start.line}`);
        }
      }
      for (const child of node.children ?? []) visit(child);
    };
    visit(ast);
  }

  it("has draft keys to check", () => {
    // A walk that finds nothing would pass silently.
    expect(files.length).toBeGreaterThan(250);
    expect(bound).toBeGreaterThanOrEqual(10);
  });

  it("finds no key spelled out by hand", () => {
    expect(offenders).toEqual([]);
  });
});
