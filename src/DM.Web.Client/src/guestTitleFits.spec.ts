/**
 * @vitest-environment node
 */

/**
 * A heading of the guest surface never wraps to a second line.
 *
 * The rule is the owner's and it is absolute, so it needs a check that runs
 * without a browser. Nothing in jsdom can measure text, so this file measures
 * it from the font the site actually ships: PTSans-Bold.ttf, the face the
 * headings are set in. shared/lib/testing/fontFace.ts reads its glyph
 * advances, its character map and its GPOS kerning. The kerning matters here:
 * ignoring it overstates a heading by up to several pixels, which is more than
 * the tightest margin here.
 *
 * The budget is the card, not a number typed twice: every term is read back out
 * of AuthSurface.sass and Variables.sass, so a change to the card's width, its
 * gutter, the reserve under the cross or the size of the title moves this check
 * with it. A value that stops being found fails the run rather than defaulting.
 *
 * Two things keep the measurement honest. The model is calibrated against a
 * real browser: "ПОДТВЕРЖДЕНИЕ ВХОДА" measured 239.6px in Chrome at the
 * shipped size, and the parser has to agree to a tenth of a pixel. And a
 * canary heading, long enough that no card could hold it, has to fail the same
 * budget - a green run with a broken parser would otherwise mean nothing.
 *
 * What this file cannot see: the frame before a web font arrives, when the
 * fallback face is wider. That is a separate question and a separate fix.
 */
import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { loadFace } from "@/shared/lib/testing/fontFace";

const HERE = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(resolve(HERE, p), "utf8");

// ─────────────────────────────────────────────────────────── the budgets ──

const SHEET = read("assets/styles/AuthSurface.sass");
const VARIABLES = read("assets/styles/Variables.sass");

function px(source: string, pattern: RegExp, what: string): number {
  const found = source.match(pattern);
  if (!found) throw new Error("не нашел в листе: " + what);
  return Number(found[1]);
}

/**
 * A step of the spacing scale. It is written as a multiple of the grid rather
 * than as a number, so reading one means doing the multiplication the sheet
 * does.
 */
const GRID = px(VARIABLES, /\$grid-step:\s*(\d+)px/, "шаг сетки");
const step = (name: string, what: string): number =>
  px(
    VARIABLES,
    new RegExp("\\" + name + ":\\s*(\\d+)\\s*\\*\\s*\\$grid-step"),
    what,
  ) * GRID;

/**
 * The body of one rule of the sheet. Sass has no braces, so a block runs to the
 * first line that starts in the first column.
 *
 * Every value below is read out of the block it belongs to and not out of the
 * sheet at large, because a pattern let loose on the sheet answers a question
 * about a value that is gone with the next rule that happens to declare the
 * same property: the caption of the main button, taken off the scale and
 * written as a literal again, was answered by the size of a label row four
 * rules further down, and the run stayed green.
 */
const rule = (selector: string): string => {
  const lines = SHEET.split("\n");
  const opens = lines.indexOf(selector);
  if (opens < 0) throw new Error("не нашел в листе: правило " + selector);

  const body: string[] = [];
  for (let at = opens + 1; at < lines.length; at += 1) {
    if (lines[at].trim() !== "" && !/^\s/.test(lines[at])) break;
    body.push(lines[at]);
  }
  return body.join("\n");
};

const CARD_WIDE = px(SHEET, /\$auth-card-width:\s*(\d+)px/, "ширина карточки");
const CARD_NARROW = px(
  SHEET,
  /\$auth-card-width-compact:\s*(\d+)px/,
  "ширина узкой карточки",
);
const GUTTER = px(
  rule(".auth-surface .auth-card"),
  /padding:\s*(\d+)px/,
  "поля карточки",
);
const FRAME = px(
  rule(".auth-surface .auth-card"),
  /border:\s*(\d+)px/,
  "рамка карточки",
);
const RESERVE = px(
  rule(".auth-surface .auth-card:has(> .auth-close) .auth-title"),
  /padding:\s*0 (\d+)px/,
  "резерв заголовка под крестик",
);
const SIZE = px(VARIABLES, /\$title-font-size:\s*(\d+)px/, "кегль заголовка");
const TRACKING = Number(
  rule(".auth-surface .auth-title").match(
    /letter-spacing:\s*([\d.]+)px/,
  )?.[1] ?? NaN,
);

/**
 * What the card keeps between itself and the edge of the window: the dialog
 * writes it as its own cap.
 */
const MARGIN = (() => {
  const found = rule(".auth-surface.auth-dialog").match(
    /max-width:\s*calc\(100vw - #\{\$([\w-]+)\} \* 2\)/,
  );
  if (!found) throw new Error("не нашел в листе: поле карточки от края окна");
  return step("$" + found[1], "поле карточки от края окна");
})();

/** The size the caption of the main button is set in. */
const BUTTON_SIZE = (() => {
  const name = rule(".auth-surface .auth-button").match(
    /font-size:\s*\$([\w-]+)/,
  )?.[1];
  if (!name) throw new Error("не нашел в листе: кегль главной кнопки");
  return px(
    VARIABLES,
    new RegExp("\\$" + name + ":\\s*(\\d+)px"),
    "кегль главной кнопки",
  );
})();

/**
 * The window a guest screen is judged in: any window that leaves every card
 * the width it declares. A phone is one of them: it draws the page at the
 * fixed width of the layout of a phone, wider than any card, and shows the
 * card of the full version, and the sheet keeps no form of the card for a
 * narrow window (the first test below).
 */
const DESKTOP = 1200;

/** How wide the card is: what it asks for, or what the window leaves it. */
const card = (window: number, wide: boolean): number =>
  Math.min(wide ? CARD_WIDE : CARD_NARROW, window - 2 * MARGIN);
/** The content box of a card of that width. */
const inside = (width: number): number => width - 2 * FRAME - 2 * GUTTER;
/** What the cross takes out of the heading: nothing on a page, which has none. */
const reserve = (dialog: boolean): number => (dialog ? RESERVE : 0);
const budget = (window: number, dialog: boolean, wide: boolean): number =>
  inside(card(window, wide)) - 2 * reserve(dialog);

const BUDGET = {
  /** A dialog: it carries the cross, and the title keeps the reserve. */
  dialogNarrow: budget(DESKTOP, true, false),
  dialogWide: budget(DESKTOP, true, true),
  /** A page from a letter: no cross on it, nothing to close. */
  pageNarrow: budget(DESKTOP, false, false),
  pageWide: budget(DESKTOP, false, true),
};

// ────────────────────────────────────────────────────────────── the rule ──

/**
 * Headings whose card is the wide one, and what makes it wide. The width is
 * decided by what the card holds - an address, a password, a list of causes -
 * and the register of that is pages/account/guestCardWidth.spec.ts; this map
 * only says which budget each heading is judged against. Everything not listed
 * is judged against the narrow budget of its kind, which is the strict
 * reading: a heading that fits the narrow card fits anywhere.
 */
const ON_WIDE_CARD = new Map<string, string>([
  ["Войти", "поле пароля"],
  ["Регистрация", "поле почты и поле пароля"],
  ["Восстановление доступа", "поле почты"],
  ["Запрос на отключение", "поле почты"],
  ["Новый пароль", "поле пароля"],
]);

const TITLE = /<h([12]) class="auth-title">([^<]+)<\/h[12]>/g;
const SOURCES = ["features/auth/ui", "pages/account"];

function vueFiles(): { full: string; shown: string }[] {
  const found: { full: string; shown: string }[] = [];
  const walk = (at: string, shown: string) => {
    for (const entry of readdirSync(at, { withFileTypes: true })) {
      const full = resolve(at, entry.name);
      if (entry.isDirectory()) walk(full, shown + "/" + entry.name);
      else if (entry.name.endsWith(".vue"))
        found.push({ full, shown: shown + "/" + entry.name });
    }
  };
  for (const dir of SOURCES) walk(resolve(HERE, dir), dir);
  return found;
}

function titlesInCode() {
  const found: { text: string; dialog: boolean; file: string }[] = [];
  for (const { full, shown } of vueFiles()) {
    for (const m of readFileSync(full, "utf8").matchAll(TITLE)) {
      // <h2> opens a dialog, <h1> a page from a letter: only the dialog
      // carries a cross, and only it holds the reserve under it.
      found.push({ text: m[2], dialog: m[1] === "2", file: shown });
    }
  }
  return found;
}

/**
 * The captions of the main button, read the way the headings are read.
 *
 * A caption is what stands between the end of the opening tag and the next tag,
 * so the scan has to find that end itself: an attribute of this control can
 * hold a ">" (v-if, :disabled), and a pattern stopping at the first one would
 * cut the tag in half. Quotes are honoured for that reason. A caption assembled
 * at runtime is skipped - there is no text of it here to measure.
 */
function captionsInCode() {
  const found: { text: string; file: string }[] = [];
  for (const { full, shown } of vueFiles()) {
    const source = readFileSync(full, "utf8");
    for (const m of source.matchAll(/class="auth-button[^"]*"/g)) {
      let at = m.index + m[0].length;
      let quote: string | null = null;
      for (; at < source.length; at += 1) {
        const ch = source[at];
        if (quote) {
          if (ch === quote) quote = null;
        } else if (ch === '"' || ch === "'") quote = ch;
        else if (ch === ">") break;
      }
      const ends = source.indexOf("<", at + 1);
      const text = source.slice(at + 1, ends < 0 ? undefined : ends).trim();
      if (text && !text.includes("{{")) found.push({ text, file: shown });
    }
  }
  return found;
}

describe("a heading of the guest surface stands on one line", () => {
  const face = loadFace(resolve(HERE, "assets/fonts/PTSans-Bold.ttf"));
  const measure = (text: string, size: number) =>
    face.width(text.toUpperCase(), size, TRACKING);
  const all = titlesInCode();

  it("measures the way the browser does", () => {
    // Measured in Chrome on 12 September 2026 in the shipped face: 239.6px.
    expect(measure("Подтверждение входа", SIZE)).toBeCloseTo(239.6, 1);
    expect(BUDGET).toEqual({
      dialogNarrow: 254,
      dialogWide: 334,
      pageNarrow: 302,
      pageWide: 382,
    });
  });

  it("keeps the card of the full version on a phone: no rule reads the width of the window", () => {
    // A heading that stepped down a size and a cross on a line of its own
    // were a form of the card for a narrow window; a phone shows the card of
    // the full version.
    expect(SHEET).not.toMatch(/@media[^(]*\(max-width/);
  });

  it("would catch a heading that does not fit", () => {
    const canary = "Подтвердите отключение двухфакторной аутентификации";
    expect(measure(canary, SIZE)).toBeGreaterThan(BUDGET.pageWide);
  });

  it("finds every heading of the surface", () => {
    expect(all.length).toBeGreaterThanOrEqual(17);
    for (const [text] of ON_WIDE_CARD) {
      expect(
        all.some((t) => t.text === text),
        `"${text}" числится широким, а в коде его нет`,
      ).toBe(true);
    }
  });

  const cases = [
    ...new Map(all.map((t) => [t.text + "|" + t.dialog, t])).values(),
  ];

  it.each(cases.map((t) => [t.text, t] as const))("holds %s", (_, title) => {
    const wide = ON_WIDE_CARD.has(title.text);
    const allowed = budget(DESKTOP, title.dialog, wide);
    const need = measure(title.text, SIZE);
    expect(
      need,
      `${title.text.toUpperCase()} просит ${need.toFixed(1)}px при бюджете ${allowed}px ` +
        `(${title.file}, ${title.dialog ? "диалог" : "страница из письма"}` +
        `${wide ? ", широкая карточка: " + ON_WIDE_CARD.get(title.text) : ""})`,
    ).toBeLessThanOrEqual(allowed);
  });
});

describe("the caption of the main button stands on one line too", () => {
  // The same face and the same measurement: the button is bold and set in the
  // family the card is. It runs the whole width of the card and carries no
  // padding of its own, so its budget is the content box whole - the cross
  // stands above it and takes nothing out of it.
  const face = loadFace(resolve(HERE, "assets/fonts/PTSans-Bold.ttf"));
  const all = captionsInCode();

  it("finds the captions", () => {
    expect(all.length).toBeGreaterThanOrEqual(10);
  });

  it.each(
    [...new Map(all.map((c) => [c.text, c])).values()].map(
      (c) => [c.text, c] as const,
    ),
  )("holds %s", (_, caption) => {
    // The narrow card is the smallest box a caption stands in.
    const allowed = inside(card(DESKTOP, false));
    const need = face.width(caption.text, BUTTON_SIZE, 0);
    expect(
      need,
      `${caption.text} просит ${need.toFixed(1)}px при бюджете ${allowed}px (${caption.file})`,
    ).toBeLessThanOrEqual(allowed);
  });
});
