/**
 * @vitest-environment node
 */

/**
 * A draft key is built, never spelled.
 *
 * Drafts were already per-entity, but the keys were assembled out of literals
 * on the screens that needed them: `topic_${id}` here, `blog_${id}_comment`
 * there, `post_meta_${id}`, `chat_room_${id}`, `global-chat`. Six schemes, no
 * single place holding them apart, and nothing stopping the next screen from
 * reusing a key that already belongs to something else — which is one game's
 * comment box opening with the text written for another game.
 *
 * shared/lib/utils/draftKey.ts is that single place. This check reads the
 * template AST of every .vue and looks at `draft-key` wherever it is bound: a
 * literal is a violation whatever it spells, and the only expressions accepted
 * are a call to the builder and a bare pass-through of the component's own
 * declared prop (PublicationForm hands the page's key down to the editor).
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources it walks. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

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

const collect = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (name.endsWith(".vue")) {
      out.push(full);
    }
  }
  return out;
};

const where = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

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
  const files = collect(CLIENT_SRC);
  const offenders: string[] = [];
  let bound = 0;

  for (const file of files) {
    const raw = readFileSync(file, "utf8");
    if (!/draft-?[Kk]ey/.test(raw)) continue;

    const { descriptor } = parseSfc(raw, { filename: file });
    const ast = descriptor.template?.ast as unknown as Node | undefined;
    if (!ast) continue;
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
          offenders.push(`${where(file)}:${prop.loc.start.line}`);
        }
      }
      for (const child of node.children ?? []) visit(child);
    };
    visit(ast);
  }

  it("has draft keys to check", () => {
    // A walk that finds nothing would pass silently. The floor is the count at
    // the time of writing: the support form dropped its draft along with its
    // editor, so a form that quietly stops autosaving is caught here rather than
    // by somebody losing a long comment.
    expect(files.length).toBeGreaterThan(250);
    expect(bound).toBeGreaterThanOrEqual(10);
  });

  it("finds no key spelled out by hand", () => {
    expect(offenders).toEqual([]);
  });
});
