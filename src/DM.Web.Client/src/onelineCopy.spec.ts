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
import { readdirSync, readFileSync } from "fs";
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
    // The day calendar's caption. "Июль 2026" became a button when the month
    // and year grids were put behind it, and a control is exactly the kind of
    // part that gets laid out as a flex item on the next edit.
    file: "shared/ui/DatePicker/CalendarGrid.vue",
    strip: "dp-header",
    line: "‹ {monthLabel} ›",
  },
  {
    file: "pages/profile/ProfilePage.vue",
    strip: "mod-header",
    line: "ПАНЕЛЬ МОДЕРАЦИИ {modSummary}",
  },
  {
    // The editor stands on every form of the site, so this one strip was the
    // defect repeated everywhere: a flex row of counters copied as three lines.
    file: "shared/ui/BBCodeEditor/BBCodeEditor.vue",
    strip: "status-bar",
    line: "{wordCountLabel} | {charCountLabel} | Есть черновик | {draftStatusText}",
  },
  {
    // The pair of mode switches above every editor, the strip the status bar
    // was fixed with: a flex row copied as "[bbcode]\nwysiwyg".
    file: "shared/ui/BBCodeEditor/BBCodeEditor.vue",
    strip: "mode-tabs",
    line: "[bbcode] wysiwyg",
  },
  {
    file: "pages/support/SupportPage.vue",
    strip: "discord-fallback",
    line: "Если удобнее, напишите нам в Discord",
  },
  {
    // Every accordion of the site in its one-title mode (the rules pages, the
    // bans table, the FAQ): a flex row copied as a newline and then the title,
    // because the marker beside it is a flex item and a flex item is a line.
    // The marker puts no character on the screen — the triangle is pseudo
    // content — so the title is the whole line.
    file: "shared/ui/ExpandableList/ExpandableList.vue",
    strip: "expandable-row--title",
    line: "{item.title}",
  },
  {
    // The events strip of the global chat: a flex row that copied as three
    // lines, and with no space in front of the bar at all ("зарисовок|").
    file: "pages/global-chat/ChatEventsPanel.vue",
    strip: "row-main",
    line: "{stateWord}: {primaryEvent.title}{primaryMeta} | описание | {countLabel} запланировано{nearestText}",
  },
];

/** @vue/compiler-core NodeTypes — the members this walk reads. */
const ELEMENT = 1;
const TEXT = 2;
const INTERPOLATION = 5;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** The directives that make an element the alternative to a sibling. */
const ALTERNATIVE = new Set(["else", "else-if"]);

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
 * The children the template compiler keeps (`whitespace: "condense"`): a
 * whitespace-only node is dropped when it spans a newline or sits at either
 * end of the element, and is a single space otherwise.
 *
 * `leadingBranchOnly` also drops a v-else / v-else-if child. It never renders
 * beside the branch it answers, so no selection holds both: the walk that
 * reads what a copy yields passes the flag, and the walk that finds the parts
 * of a strip does not, because the same declarations paint both branches.
 */
const children = (node: Node, leadingBranchOnly = false): Node[] => {
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
const copyText = (node: Node): string => {
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

/** A `+mixin` include, with or without arguments. */
const INCLUDE = /^\+([\w-]+)/;

/**
 * Every declaration of an indented-Sass block, under the chain of selectors
 * it is nested in. A `prop: value` always has whitespace after the colon in
 * Sass, which is what separates it from `&:hover` and `a:hover`; a selector
 * group spans lines, every one but the last ending in a comma.
 *
 * A block reaches a layout through `+mixin` as often as it writes one, and the
 * defect this check exists for was written that way: the `display: flex` that
 * split a one-title accordion row over two lines lived in `=expandable-row`,
 * not in the component. `mixins` is what the include stands for — pass the
 * shared ones to read a component, pass nothing to read a mixin body.
 */
const declarations = (
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

/**
 * The shared mixins, by name, with what they declare on the element itself —
 * a `&:hover` inside one paints a state, not the box, so only the body's own
 * level is kept.
 */
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

const MIXINS = sharedMixins();

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
      const inside = descendants(root);
      const parts = new Set(inside.flatMap(classesOf));
      // Every class the strip itself wears, not just the one it is named by:
      // a row that is `.expandable-row .expandable-row--title` takes its box
      // from the first and its flow from the second.
      const own = new Set(classesOf(root));
      // A part that puts no characters on the screen — an icon button, a
      // spacer — has nothing in the clipboard to break, so what it declares
      // is none of this rule's business either. Read from the markup rather
      // than listed, so it cannot outlive the silence it is granted for.
      const written = new Set(
        inside.filter((node) => copyText(node) !== "").flatMap(classesOf),
      );
      const broken: string[] = [];

      for (const style of styles) {
        for (const decl of declarations(
          style.content,
          style.loc.start.line,
          MIXINS,
        )) {
          // Pseudo-elements are the one place a selection cannot reach, so
          // what they declare is none of this rule's business.
          if (decl.selector.includes("::")) continue;
          const painted = subjects(decl.selector);
          const isPart = painted.some((cls) => parts.has(cls));
          if (!isPart && !painted.some((cls) => own.has(cls))) continue;
          if (isPart && !painted.some((cls) => written.has(cls))) continue;
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

/**
 * The rows a reader copies whole, but that hold more than one cell. A CSS
 * table row copies its cells tab-separated, the way the site's data tables do;
 * a grid or flex row copies them one per line. Both of these were grids, and
 * the staff table was left with a header moved to a table over rows that were
 * not, so one file copied its header and its rows two different ways.
 *
 * Only the row's own display is read. What a cell does inside itself — the
 * staff table breaks a role's joke name onto a second line and gives every
 * player a line — is the row's meaning and not a defect.
 */
const TABLE_ROWS: { file: string; row: string }[] = [
  { file: "pages/rules/RulesStaffTable.vue", row: "admin-row" },
  {
    file: "shared/ui/ExpandableList/ExpandableList.vue",
    row: "expandable-row--grid",
  },
];

describe("multi-cell rows", () => {
  for (const { file, row } of TABLE_ROWS) {
    it(`.${row} lays its cells out as a table, not as items`, () => {
      const { ast, styles } = sfcOf(file);
      const own = new Set(classesOf(elementOf(ast, row)));
      const broken: string[] = [];

      for (const style of styles) {
        for (const decl of declarations(
          style.content,
          style.loc.start.line,
          MIXINS,
        )) {
          if (decl.selector.includes("::")) continue;
          if (!subjects(decl.selector).some((cls) => own.has(cls))) continue;
          if (decl.prop === "display" && SPLITS_ITEMS.test(decl.value))
            broken.push(
              `${file}:${decl.line} ${decl.selector} — ${decl.prop}: ${decl.value}`,
            );
        }
      }

      expect(broken).toEqual([]);
    });
  }
});
