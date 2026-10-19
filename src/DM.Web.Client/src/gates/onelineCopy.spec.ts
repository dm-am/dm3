/**
 * @vitest-environment node
 */

/**
 * A composition that reads as one line copies as one line. A selection
 * serializer walks boxes: it starts a new line at every block-level one, flex
 * and grid items are blockified, and a box out of the flow is a line of its
 * own. So such a strip keeps the container in normal flow and the parts
 * inline, the gap a margin on the parts and the space between the words a real
 * text node (the global `.copy-space`, zero-width on screen); a part that has
 * to reserve width does it with a pseudo-element. Read off the sources: the
 * declarations live in scoped style blocks no jsdom applies. How a copy and a
 * declaration are read is shared/lib/testing/clipboardText.ts's.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  ELEMENT,
  MIXINS,
  children,
  classesOf,
  copyText,
  declarations,
  descendants,
  elementOf,
  elementsOf,
  sfcOf,
  subjects,
  type Node,
} from "@/shared/lib/testing/clipboardText";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

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
    // The day calendar's caption: the month and the year are two buttons,
    // each opening its grid.
    file: "shared/ui/DatePicker/CalendarGrid.vue",
    strip: "dp-header",
    line: "‹ {monthName} {viewYear} ›",
  },
  {
    // The editor's status line, on every form of the site.
    file: "shared/ui/BBCodeEditor/EditorStatusLine.vue",
    strip: "status-bar",
    line: "{wordCountLabel} | {charCountLabel} | Есть черновик | {draftStatusText}",
  },
  {
    // The pair of mode switches above every editor.
    file: "shared/ui/BBCodeEditor/EditorModeSwitch.vue",
    strip: "mode-tabs",
    line: "[bbcode] wysiwyg",
  },
  {
    // Every accordion of the site in its one-title mode. The marker puts no
    // character on the screen, so the title is the whole line.
    file: "shared/ui/ExpandableList/ExpandableList.vue",
    strip: "expandable-row--title",
    line: "{item.title}",
  },
  {
    // The events strip of the global chat.
    file: "pages/global-chat/ChatEventsPanel.vue",
    strip: "row-main",
    line: "{stateWord}: {primaryEvent.title}{primaryMeta} | описание | {countLabel} запланировано{nearestText}",
  },
  {
    // The events line when the list was refused: why, and the way to read it
    // again, in the line's own words and its one line.
    file: "pages/global-chat/ChatEventsPanel.vue",
    strip: "row-failed",
    line: "Эвенты не загрузились | повторить",
  },
  {
    // A line of the upcoming events the count opens: the title whole, then
    // the date and "закрытый"; on a narrow list it wraps as text. A flex row
    // would copy the title apart from its date.
    file: "pages/global-chat/ChatEventsPanel.vue",
    strip: "rest-item",
    line: "{ev.title}, {formatSiteTime(ev.startsUtc, DAY_MONTH_TIME_FORMAT)}, закрытый",
  },
];

/** Displays that lay the children out as items instead of as words. */
const SPLITS_ITEMS = /^(inline-)?(flex|grid)$/;
/** Displays that make a part a line of its own. */
const BLOCKIFIED = /^(block|flow-root|list-item|table|table-cell)$/;
/** Positions that take a part out of the flow it is written in. */
const OUT_OF_FLOW = /^(absolute|fixed)$/;

/**
 * Whether a declaration splits a line apart: a flex or grid box anywhere, a
 * part made a line of its own or taken out of the flow.
 */
const breaksLine = (
  decl: { prop: string; value: string },
  isPart: boolean,
): boolean =>
  (decl.prop === "display" && SPLITS_ITEMS.test(decl.value)) ||
  (isPart && decl.prop === "display" && BLOCKIFIED.test(decl.value)) ||
  (isPart && decl.prop === "position" && OUT_OF_FLOW.test(decl.value));

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
          if (breaksLine(decl, isPart))
            broken.push(
              `${file}:${decl.line} ${decl.selector} — ${decl.prop}: ${decl.value}`,
            );
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
 * a grid or flex row copies them one per line. Only the row's own display is
 * read: what a cell does inside itself is the row's meaning.
 */
const TABLE_ROWS: { file: string; row: string }[] = [
  { file: "widgets/sidebar/SiteAddresses.vue", row: "address-row" },
  {
    file: "shared/ui/ExpandableList/ExpandableList.vue",
    row: "expandable-row--grid",
  },
];

/**
 * The rows of the guest surface: the login, registration, access recovery and
 * two-factor cards, and the pages their letters lead to. Read apart from the
 * strips above because their layout is in a sheet of its own, AuthSurface.sass,
 * imported once from app/main.ts, and their text is in each screen that draws
 * the row.
 */
const GUEST_SHEETS = [
  "assets/styles/_AuthField.sass",
  "assets/styles/_AuthControls.sass",
];

/** The blocks of that sheet that paint a row a reader sees as one line. */
const GUEST_ROWS = [
  ".auth-surface .auth-label-row",
  ".auth-surface .auth-checkline",
  ".auth-surface .auth-checkline--top",
];

/**
 * What one part of a row puts in the clipboard: an element by its own text,
 * the zero-width .copy-space and a plain space of the markup by a single " ".
 *
 * The parts are read one at a time and not as one run, because what holds two
 * of them apart has to be a character somebody wrote. The indentation of a
 * template reaches the DOM as a space too, so a row that has lost its
 * .copy-space still reads correctly — right up to the reformat that pulls the
 * next tag onto one line and takes the space away with it. Read this way, the
 * space has to be in the markup for the line to come out right.
 */
const partsOf = (row: Node): string[] =>
  children(row, true)
    .map((child) => {
      if (child.type === ELEMENT)
        return classesOf(child).includes("copy-space") ? " " : copyText(child);
      // A text node of the markup: its own words, or the space it is made of.
      return copyText(child) || " ";
    })
    .filter((text) => text !== "");

/**
 * Every screen that draws one of those rows, and the text a reader gets from
 * it. A screen that draws the same row twice names both, in the order the
 * template writes them; a row whose right-hand half is a v-if reads as its
 * leading branch, the way the strips above do.
 *
 * The check line has no .copy-space in it and needs none: the box puts no
 * character on the screen, so there is nothing to separate, and a space there
 * would land at the head of the copy.
 */
const GUEST_LINES: {
  file: string;
  row: string;
  lines: string[];
  /** The components the screen draws above its own rows, read before it. */
  components?: string[];
}[] = [
  {
    file: "features/auth/ui/LoginForm.vue",
    components: ["features/auth/ui/LoginSecondFactor.vue"],
    row: "auth-label-row",
    // Two rows, in the order the card draws them: the kind of code on the
    // second step, where the label names what is being typed and the link
    // beside it offers the other kind; then the password of the first step,
    // whose link is whichever way out the state calls for. The address above it
    // wears a lone label: every way out of this card hangs off the strip under
    // the password, and that is the strip they answer.
    lines: ["{codeName} {otherCodeAction}", "Пароль Отправить письмо повторно"],
  },
  {
    file: "features/auth/ui/LoginForm.vue",
    row: "auth-checkline",
    lines: ["Запомнить меня"],
  },
  {
    file: "features/auth/ui/RegistrationForm.vue",
    row: "auth-label-row",
    lines: ["Почта Войти", "Почта Изменить"],
  },
  {
    file: "features/auth/ui/RegistrationForm.vue",
    row: "auth-checkline",
    lines: ["Я принимаю правила сайта"],
  },
  {
    file: "features/auth/ui/AccessRecoveryForm.vue",
    row: "auth-label-row",
    lines: ["Почта Нет доступа к почте?"],
  },
  {
    file: "pages/account/AccountActivationPage.vue",
    row: "auth-label-row",
    lines: ["Имя пользователя Изменить"],
  },
];

describe("the one-line rows of the guest surface", () => {
  const rules = GUEST_SHEETS.flatMap((sheet) =>
    declarations(readFileSync(join(CLIENT_SRC, sheet), "utf8"), 1, MIXINS).map(
      (decl) => ({ ...decl, sheet }),
    ),
  );

  for (const row of GUEST_ROWS) {
    it(`${row} lays its parts out as words, not as items`, () => {
      // The block that opens with this selector, and everything nested in it.
      // What the sheet says about a part somewhere else is another rule's
      // business: the label of a row is floated out of the line by design, and
      // a float is blockified wherever it is written.
      const inside = rules.filter(
        (decl) => decl.selector === row || decl.selector.startsWith(`${row} `),
      );
      const broken = inside
        .filter((decl) => {
          // Pseudo-elements are the one place a selection cannot reach.
          if (decl.selector.includes("::")) return false;
          return breaksLine(decl, decl.selector !== row);
        })
        .map(
          (decl) =>
            `${decl.sheet}:${decl.line} ${decl.selector} — ${decl.prop}: ${decl.value}`,
        );

      // A selector nobody writes any more would pass every line above without
      // reading a thing.
      expect(inside.length).toBeGreaterThan(0);
      expect(broken).toEqual([]);
    });
  }

  for (const { file, row, lines, components = [] } of GUEST_LINES) {
    it(`.${row} of ${file} copies as ${lines.map((line) => `"${line}"`).join(" and ")}`, () => {
      expect(
        [...components, file]
          .flatMap((source) => elementsOf(sfcOf(source).ast, row))
          .map((node) => partsOf(node).join("")),
      ).toEqual(lines);
    });
  }
});

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
