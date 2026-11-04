/**
 * What a reader copies from a piece of a template, and what a sheet declares
 * on its parts, read without a render (onelineCopy.spec.ts). An interpolated
 * string literal is that string, any other one its expression in braces;
 * whitespace is condensed as the template compiler does. Spec-only.
 */
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC } from "./sourceTree";

/** @vue/compiler-core NodeTypes — the members this walk reads. */
export const ELEMENT = 1;
export const TEXT = 2;
export const INTERPOLATION = 5;
export const ATTRIBUTE = 6;
export const DIRECTIVE = 7;

/** The directives that make an element the alternative to a sibling. */
const ALTERNATIVE = new Set(["else", "else-if"]);

/** The slice of the template AST this walk reads; the rest is ignored. */
export interface Node {
  type: number;
  /** A TEXT node holds its string here, an INTERPOLATION its expression. */
  content?: string | { content?: string };
  props?: { type: number; name: string; value?: { content: string } }[];
  children?: Node[];
}

/** One declaration of an indented-Sass block, with the chain it sits in. */
export interface Declaration {
  selector: string;
  prop: string;
  value: string;
  line: number;
}

const WHITESPACE_ONLY = /^[\t\r\n\f ]*$/;
const STRING_LITERAL = /^(["'])(.*)\1$/;

const textOf = (node: Node): string =>
  typeof node.content === "string" ? node.content : "";

/** An element that never renders beside the branch it is an alternative to. */
const isAlternative = (node: Node): boolean =>
  (node.props ?? []).some(
    (prop) => prop.type === DIRECTIVE && ALTERNATIVE.has(prop.name),
  );

/** A literal interpolation is its own text; any other one stands for itself. */
const expressionOf = (node: Node): string => {
  const raw = (
    typeof node.content === "object" ? (node.content?.content ?? "") : ""
  ).trim();
  const literal = STRING_LITERAL.exec(raw);
  return literal ? literal[2] : `{${raw}}`;
};

/**
 * The children the template compiler keeps (`whitespace: "condense"`).
 * `leadingBranchOnly` drops a v-else / v-else-if child, which never renders
 * beside its branch: the copy walk passes it, the strip walk does not.
 */
export const children = (node: Node, leadingBranchOnly = false): Node[] => {
  const kept = (node.children ?? [])
    .filter(
      (child) =>
        child.type === ELEMENT ||
        child.type === TEXT ||
        child.type === INTERPOLATION,
    )
    .filter((child) => !(leadingBranchOnly && isAlternative(child)));
  return kept.filter((child, index) => {
    if (child.type !== TEXT || !WHITESPACE_ONLY.test(textOf(child)))
      return true;
    return !(
      textOf(child).includes("\n") ||
      index === 0 ||
      index === kept.length - 1
    );
  });
};

/** What a reader gets when they select the strip and copy it. */
export const copyText = (node: Node): string => {
  const parts: string[] = [];
  const walk = (current: Node): void => {
    if (current.type === TEXT) parts.push(textOf(current));
    else if (current.type === INTERPOLATION) parts.push(expressionOf(current));
    else children(current, true).forEach(walk);
  };
  walk(node);
  return parts
    .join("")
    .replace(/[\t\r\n\f ]+/g, " ")
    .trim();
};

export const classesOf = (node: Node): string[] =>
  (
    node.props?.find((p) => p.type === ATTRIBUTE && p.name === "class")?.value
      ?.content ?? ""
  )
    .split(/\s+/)
    .filter(Boolean);

export const descendants = (node: Node, out: Node[] = []): Node[] => {
  for (const child of children(node)) {
    if (child.type !== ELEMENT) continue;
    out.push(child);
    descendants(child, out);
  }
  return out;
};

/** Every element that carries the class, in the order the template writes them. */
export const elementsOf = (root: Node, cls: string): Node[] =>
  [root, ...descendants(root)].filter((node) => classesOf(node).includes(cls));

/** The one element that carries the class, or a failure naming the count. */
export const elementOf = (root: Node, cls: string): Node => {
  const found = elementsOf(root, cls);
  if (found.length !== 1)
    throw new Error(`.${cls}: ${found.length} elements carry it, expected one`);
  return found[0];
};

/** A `+mixin` include, with or without arguments. */
const INCLUDE = /^\+([\w-]+)/;

/**
 * Every declaration of an indented-Sass block under its selector chain (a
 * `prop: value` has whitespace after the colon). `mixins` stands for what a
 * `+mixin` includes: pass the shared ones for a component, none for a mixin.
 */
export const declarations = (
  sass: string,
  firstLine: number,
  mixins: Map<string, Declaration[]> = new Map(),
): Declaration[] => {
  const out: Declaration[] = [];
  const stack: { indent: number; selector: string }[] = [];
  let group: string[] = [];
  let groupIndent = 0;

  sass.split("\n").forEach((raw, index) => {
    const text = raw.trim();
    if (!text || text.startsWith("//")) return;
    const indent = raw.length - raw.trimStart().length;
    while (stack.length && stack[stack.length - 1].indent >= indent)
      stack.pop();

    const declaration = /^([a-z-]+):[ \t]+(\S.*)$/.exec(text);
    if (declaration && !text.endsWith(",")) {
      out.push({
        selector: stack.map((entry) => entry.selector).join(" "),
        prop: declaration[1],
        value: declaration[2].trim(),
        line: firstLine + index,
      });
      return;
    }
    const include = INCLUDE.exec(text);
    if (include) {
      const chain = stack.map((entry) => entry.selector).join(" ");
      for (const inherited of mixins.get(include[1]) ?? [])
        out.push({ ...inherited, selector: chain, line: firstLine + index });
      return;
    }
    if (text.endsWith(",")) {
      if (!group.length) groupIndent = indent;
      group.push(text.slice(0, -1).trim());
      return;
    }
    const selector =
      group.length && groupIndent === indent
        ? [...group, text].join(", ")
        : text;
    group = [];
    stack.push({ indent, selector });
  });

  return out;
};

/** The shared mixins by name, with what they declare on the element's own level. */
const sharedMixins = (): Map<string, Declaration[]> => {
  const out = new Map<string, Declaration[]>();
  const dir = join(CLIENT_SRC, "assets/styles");

  for (const file of readdirSync(dir).filter((n) => n.endsWith(".sass"))) {
    const lines = readFileSync(join(dir, file), "utf8").split("\n");
    let name = "";
    let body: string[] = [];
    const close = () => {
      if (name)
        out.set(
          name,
          declarations(body.join("\n"), 0).filter((decl) => !decl.selector),
        );
      name = "";
      body = [];
    };

    for (const raw of lines) {
      const opening = /^=([\w-]+)/.exec(raw);
      if (opening) {
        close();
        name = opening[1];
        continue;
      }
      if (!name) continue;
      // Back at the left margin: the mixin body has ended.
      if (raw.trim() && !/^[\t ]/.test(raw)) close();
      else body.push(raw);
    }
    close();
  }
  return out;
};

export const MIXINS = sharedMixins();

/** The classes a rule paints: the last one of every part of the selector. */
export const subjects = (selector: string): string[] =>
  selector
    .split(",")
    .map((part) => {
      const classes = part.match(/\.[A-Za-z][\w-]*/g) ?? [];
      return classes.length ? classes[classes.length - 1].slice(1) : "";
    })
    .filter(Boolean);

export const sfcOf = (file: string) => {
  const path = join(CLIENT_SRC, file);
  const { descriptor } = parseSfc(readFileSync(path, "utf8"), {
    filename: path,
  });
  const ast = descriptor.template?.ast as unknown as Node | undefined;
  if (!ast) throw new Error(`${file} has no template`);
  return { ast, styles: descriptor.styles.filter((s) => s.lang === "sass") };
};
