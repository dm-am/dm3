/**
 * @vitest-environment node
 */

/**
 * A composition that reads as one line copies as one line.
 *
 * A selection serializer does not read pixels, it walks boxes: it starts a
 * new line at every block-level one, flex and grid items are blockified, and
 * a box taken out of the flow is a line of its own wherever it is painted. So
 * both idioms turn a strip that looks like "‹ Июль 2026 ›" into three lines
 * in the clipboard. The site builds such a strip one way instead: the
 * container stays in normal flow, the parts stay inline, the visible gap is a
 * margin on the parts, and the space between the words is a real text node —
 * the global `.copy-space` span, zero-width on screen and a plain " " in a
 * copy. Where a part has to reserve width, it reserves it with a
 * pseudo-element, which a selection cannot reach at all.
 *
 * Both ways of breaking it were live: the period picker centered its label
 * over the sizer out of flow, and the profile's moderation header was a flex
 * row. Neither shows on screen, which is why they need a check.
 *
 * The check reads the sources rather than a rendered page. The declarations
 * live in scoped style blocks that no jsdom applies, and reaching the
 * moderation header at runtime takes a loaded profile, four stores and six
 * requests — the same reason ProfilePage.spec.ts reads its template.
 *
 * The expected line is spelled the way markup can be read statically: an
 * interpolation of a string literal is that string, any other one is its
 * expression in braces. Whitespace is condensed exactly as the template
 * compiler condenses it, so parts glued together in the DOM are glued
 * together here too.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/**
 * The strips, by the class of the element that draws the whole line, and the
 * text a reader gets when they copy it.
 */
const STRIPS: { file: string; strip: string; line: string }[] = [
  {
    file: "shared/ui/MonthYearPicker/MonthYearPicker.vue",
    strip: "myp-field",
    line: "‹ {label} ›",
  },
  {
    file: "pages/profile/ProfilePage.vue",
    strip: "mod-header",
    line: "ПАНЕЛЬ МОДЕРАЦИИ {modSummary}",
  },
];

/** @vue/compiler-core NodeTypes — the members this walk reads. */
const ELEMENT = 1;
const TEXT = 2;
const INTERPOLATION = 5;
const ATTRIBUTE = 6;

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  /** A TEXT node holds its string here, an INTERPOLATION its expression. */
  content?: string | { content?: string };
  props?: { type: number; name: string; value?: { content: string } }[];
  children?: Node[];
}

/** One declaration of an indented-Sass block, with the chain it sits in. */
interface Declaration {
  selector: string;
  prop: string;
  value: string;
  line: number;
}

const WHITESPACE_ONLY = /^[\t\r\n\f ]*$/;
const STRING_LITERAL = /^(["'])(.*)\1$/;

const textOf = (node: Node): string =>
  typeof node.content === "string" ? node.content : "";

/** A literal interpolation is its own text; any other one stands for itself. */
const expressionOf = (node: Node): string => {
  const raw = (
    typeof node.content === "object" ? (node.content?.content ?? "") : ""
  ).trim();
  const literal = STRING_LITERAL.exec(raw);
  return literal ? literal[2] : `{${raw}}`;
};

/**
 * The children the template compiler keeps (`whitespace: "condense"`): a
 * whitespace-only node is dropped when it spans a newline or sits at either
 * end of the element, and is a single space otherwise.
 */
const children = (node: Node): Node[] => {
  const kept = (node.children ?? []).filter(
    (child) =>
      child.type === ELEMENT ||
      child.type === TEXT ||
      child.type === INTERPOLATION,
  );
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
const copyText = (node: Node): string => {
  const parts: string[] = [];
  const walk = (current: Node): void => {
    if (current.type === TEXT) parts.push(textOf(current));
    else if (current.type === INTERPOLATION) parts.push(expressionOf(current));
    else children(current).forEach(walk);
  };
  walk(node);
  return parts
    .join("")
    .replace(/[\t\r\n\f ]+/g, " ")
    .trim();
};

const classesOf = (node: Node): string[] =>
  (
    node.props?.find((p) => p.type === ATTRIBUTE && p.name === "class")?.value
      ?.content ?? ""
  )
    .split(/\s+/)
    .filter(Boolean);

const descendants = (node: Node, out: Node[] = []): Node[] => {
  for (const child of children(node)) {
    if (child.type !== ELEMENT) continue;
    out.push(child);
    descendants(child, out);
  }
  return out;
};

/** The one element that carries the class, or a failure naming the count. */
const elementOf = (root: Node, cls: string): Node => {
  const found = [root, ...descendants(root)].filter((node) =>
    classesOf(node).includes(cls),
  );
  if (found.length !== 1)
    throw new Error(`.${cls}: ${found.length} elements carry it, expected one`);
  return found[0];
};

/**
 * Every declaration of an indented-Sass block, under the chain of selectors
 * it is nested in. A `prop: value` always has whitespace after the colon in
 * Sass, which is what separates it from `&:hover` and `a:hover`; a selector
 * group spans lines, every one but the last ending in a comma.
 */
const declarations = (sass: string, firstLine: number): Declaration[] => {
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

/** The classes a rule paints: the last one of every part of the selector. */
const subjects = (selector: string): string[] =>
  selector
    .split(",")
    .map((part) => {
      const classes = part.match(/\.[A-Za-z][\w-]*/g) ?? [];
      return classes.length ? classes[classes.length - 1].slice(1) : "";
    })
    .filter(Boolean);

/** Displays that lay the children out as items instead of as words. */
const SPLITS_ITEMS = /^(inline-)?(flex|grid)$/;
/** Displays that make a part a line of its own. */
const BLOCKIFIED = /^(block|flow-root|list-item|table|table-cell)$/;
/** Positions that take a part out of the flow it is written in. */
const OUT_OF_FLOW = /^(absolute|fixed)$/;

const sfcOf = (file: string) => {
  const path = join(CLIENT_SRC, file);
  const { descriptor } = parseSfc(readFileSync(path, "utf8"), {
    filename: path,
  });
  const ast = descriptor.template?.ast as unknown as Node | undefined;
  if (!ast) throw new Error(`${file} has no template`);
  return { ast, styles: descriptor.styles.filter((s) => s.lang === "sass") };
};

describe("one-line compositions", () => {
  for (const { file, strip, line } of STRIPS) {
    it(`.${strip} copies as "${line}"`, () => {
      expect(copyText(elementOf(sfcOf(file).ast, strip))).toBe(line);
    });

    it(`.${strip} keeps its parts inline and in the flow`, () => {
      const { ast, styles } = sfcOf(file);
      const root = elementOf(ast, strip);
      const parts = new Set(descendants(root).flatMap(classesOf));
      const broken: string[] = [];

      for (const style of styles) {
        for (const decl of declarations(style.content, style.loc.start.line)) {
          // Pseudo-elements are the one place a selection cannot reach, so
          // what they declare is none of this rule's business.
          if (decl.selector.includes("::")) continue;
          const painted = subjects(decl.selector);
          const isPart = painted.some((cls) => parts.has(cls));
          if (!isPart && !painted.includes(strip)) continue;
          if (
            (decl.prop === "display" && SPLITS_ITEMS.test(decl.value)) ||
            (isPart &&
              decl.prop === "display" &&
              BLOCKIFIED.test(decl.value)) ||
            (isPart && decl.prop === "position" && OUT_OF_FLOW.test(decl.value))
          ) {
            broken.push(
              `${file}:${decl.line} ${decl.selector} — ${decl.prop}: ${decl.value}`,
            );
          }
        }
      }

      // A strip with no parts would pass every line above without reading
      // anything at all.
      expect(parts.size).toBeGreaterThan(0);
      expect(broken).toEqual([]);
    });
  }
});
