/**
 * @vitest-environment node
 */

/**
 * UI_STANDARDS says it in one line — "Иконки-кнопки и числовые ссылки:
 * обязательный aria-label" — and nothing checked it. Two screens carry the
 * same message toolbar: the global chat names its like / edit / delete
 * buttons, the messenger copied the markup without the names, and the same
 * three controls announce by name in one chat and as "кнопка, кнопка, кнопка"
 * in the other. Copying is how markup spreads; a rule that lives only in prose
 * does not travel with it.
 *
 * A tooltip is not a substitute. Tooltip.vue puts `aria-describedby` on the
 * wrapper span, and only while the tooltip is on screen: it describes a
 * control that is already named, it cannot be the name. The name goes on the
 * control.
 *
 * The check walks the template of every .vue and asks one question of every
 * <button>, <a> and <router-link>: does the source establish a name?
 *
 *   - `aria-label`, `aria-labelledby` or `title`, literal or bound — yes;
 *   - static text — only when it holds a letter or a digit. "<<", "...", "+"
 *     and "—" are punctuation, and a reader announces "ссылка меньше меньше";
 *   - `<slot />` — yes, the caller fills it (Button.vue, RemoveButton.vue);
 *   - `<img alt="...">` — yes;
 *   - a child component — yes, unless its own template root is
 *     `aria-hidden="true"`. That is what makes SvgIcon decorative, and it is
 *     read out of the components themselves, so an icon added later is
 *     classified by its own markup instead of by a list kept here.
 *
 * What the source cannot answer the test does not judge: an expression is
 * opaque, so `<router-link>{{ topic.title }}</router-link>` passes — and so
 * would Paging's page numbers, which is why Paging.spec.ts renders those and
 * asserts the names there.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { basename, dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** @vue/compiler-core NodeTypes — the members a parsed template holds. */
const ELEMENT = 1;
const TEXT = 2;
const SIMPLE_EXPRESSION = 4;
const INTERPOLATION = 5;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** A literal attribute, or a `v-bind` whose argument names one. */
interface Prop {
  type: number;
  name: string;
  /** ATTRIBUTE only. */
  value?: { content: string };
  /** DIRECTIVE only: the part after the colon in `:aria-label`. */
  arg?: { type: number; content: string };
}

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  /** ELEMENT only. */
  tag?: string;
  /** TEXT only. */
  content?: string;
  props?: Prop[];
  children?: Node[];
  loc: { start: { line: number } };
}

const NAMING = ["aria-label", "aria-labelledby", "title"];
const INTERACTIVE = new Set(["button", "a", "routerlink"]);
const LETTER_OR_DIGIT = /[\p{L}\p{N}]/u;

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

/** `router-link` and `RouterLink` are one tag. */
const plain = (tag: string): string => tag.replace(/-/g, "").toLowerCase();

/** Vue's own rule: a capital or a hyphen means "not an HTML element". */
const isComponent = (tag: string): boolean =>
  /[A-Z]/.test(tag) || tag.includes("-");

const prop = (node: Node, name: string): Prop | undefined =>
  node.props?.find(
    (p) =>
      (p.type === ATTRIBUTE && p.name === name) ||
      (p.type === DIRECTIVE &&
        p.name === "bind" &&
        p.arg?.type === SIMPLE_EXPRESSION &&
        p.arg.content === name),
  );

const isHidden = (node: Node): boolean => {
  const hidden = prop(node, "aria-hidden");
  return hidden?.type === ATTRIBUTE && hidden.value?.content === "true";
};

const isNamed = (node: Node): boolean =>
  NAMING.some((name) => prop(node, name) !== undefined);

/** Components made decorative by their own root, SvgIcon first among them. */
const decorativeIn = (templates: Map<string, Node>): Set<string> => {
  const decorative = new Set<string>();
  for (const [file, ast] of templates) {
    const roots = (ast.children ?? []).filter((node) => node.type === ELEMENT);
    if (roots.length === 1 && isHidden(roots[0])) {
      decorative.add(plain(basename(file, ".vue")));
    }
  }
  return decorative;
};

/** Does anything inside the element give it a name? */
const namedByContent = (node: Node, decorative: Set<string>): boolean => {
  for (const child of node.children ?? []) {
    if (child.type === TEXT) {
      if (LETTER_OR_DIGIT.test(child.content ?? "")) return true;
      continue;
    }
    // An expression is opaque — assume it renders something readable.
    if (child.type === INTERPOLATION) return true;
    if (child.type !== ELEMENT) continue;
    if (isHidden(child)) continue;
    const tag = child.tag ?? "";
    if (tag === "slot" || isNamed(child)) return true;
    if (tag === "img") {
      const alt = prop(child, "alt");
      if (alt !== undefined && (alt.type === DIRECTIVE || !!alt.value?.content))
        return true;
      continue;
    }
    if (isComponent(tag) && !INTERACTIVE.has(plain(tag))) {
      if (!decorative.has(plain(tag))) return true;
      continue;
    }
    if (namedByContent(child, decorative)) return true;
  }
  return false;
};

describe("every control carries an accessible name", () => {
  const templates = new Map<string, Node>();
  for (const file of collect(CLIENT_SRC)) {
    const { descriptor } = parseSfc(readFileSync(file, "utf8"), {
      filename: file,
    });
    const ast = descriptor.template?.ast as unknown as Node | undefined;
    if (ast) templates.set(file, ast);
  }
  const decorative = decorativeIn(templates);

  it("has a tree to check", () => {
    // A walk that finds nothing would pass silently.
    expect(templates.size).toBeGreaterThan(250);
    expect(decorative.has("svgicon")).toBe(true);
  });

  it("holds for every button and link in a shipped template", () => {
    const anonymous: string[] = [];
    for (const [file, ast] of templates) {
      const path = where(file);
      const visit = (node: Node): void => {
        if (
          node.type === ELEMENT &&
          INTERACTIVE.has(plain(node.tag ?? "")) &&
          !isHidden(node) &&
          !isNamed(node) &&
          !namedByContent(node, decorative)
        ) {
          anonymous.push(`${path}:${node.loc.start.line}: <${node.tag}>`);
        }
        for (const child of node.children ?? []) visit(child);
      };
      visit(ast);
    }
    expect(anonymous).toEqual([]);
  });
});
