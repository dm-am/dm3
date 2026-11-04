/**
 * @vitest-environment node
 */

/**
 * The guest surface against the prototype it was drawn from: the facts of the
 * sheet no mounted component can be asked about, since jsdom applies no
 * stylesheet. The sheet is read as text instead.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";
import { SURFACE, ruleBody } from "@/shared/lib/testing/guestSheet";

const read = (...parts: string[]): string =>
  readFileSync(join(CLIENT_SRC, ...parts), "utf8");

/**
 * The three screens a dead link lands on. Each says the same two things in the
 * same two voices: what probably went wrong in the card's own type, and the way
 * out a step smaller and grey.
 */
const DEAD_LINK_SCREENS = [
  "AccountActivationPage.vue",
  "PasswordResetPage.vue",
  "UsernameChangePage.vue",
];
const DIALOG = read("shared", "ui", "Layout", "AuthDialog.vue");

/** How many times a literal stands in a text. Declarations, not prose: the
 * number is looked for after a colon, so a comment that mentions it does not
 * count as a second source. */
const times = (source: string, literal: string): number =>
  source.split(literal).length - 1;

/**
 * The sheet with its own string variables written out where they are
 * interpolated: a selector assembled out of a variable reads as one word in the
 * source, and the rule below has to see the selector the browser will see.
 */
const written = (source: string): string => {
  const values = new Map<string, string>();
  for (const [, name, value] of source.matchAll(
    /^(\$[\w-]+):\s*'([^']*)'\s*$/gm,
  )) {
    values.set(name, value);
  }

  return source.replace(
    /#\{(\$[\w-]+)\}/g,
    (whole, name: string) => values.get(name) ?? whole,
  );
};

/**
 * Selectors in which one `:has()` stands inside another.
 *
 * Selectors Level 4 forbids it, and a browser that meets one drops the whole
 * rule without a word: the sheet compiles, nothing on screen says so, and the
 * element keeps whatever the rule was going to change. Nesting is counted by
 * parentheses rather than matched by a pattern, so `:not(:has(...))`, which is
 * legal and used here, is not reported.
 */
const nestedHas = (source: string): string[] => {
  const offenders: string[] = [];

  for (const raw of written(source).split("\n")) {
    const line = raw.trim();
    if (!line.includes(":has(")) continue;

    let depth = 0;
    let openedAt: number | null = null;
    for (let i = 0; i < line.length; i += 1) {
      if (line.startsWith(":has(", i)) {
        if (openedAt !== null) {
          offenders.push(line);
          break;
        }
        openedAt = depth;
      }
      if (line[i] === "(") depth += 1;
      else if (line[i] === ")") {
        depth -= 1;
        if (openedAt !== null && depth === openedAt) openedAt = null;
      }
    }
  }

  return offenders;
};

describe("the cross that closes a guest dialog", () => {
  const cross = () => ruleBody(SURFACE, ".auth-surface .auth-close");

  it("is two bars and not a glyph", () => {
    // Not the glyph the site uses elsewhere, and the same drawing as
    // the status cross and the in-field one: 14 by 2, rounded by a pixel,
    // crossed at forty-five degrees. The drawing is the mixin of the tokens,
    // which the button takes whole.
    const bars = read("assets", "styles", "_AuthTokens.sass").split(
      "=auth-bar-cross",
    )[1];
    expect(cross()).toContain("+auth-bar-cross");
    expect(bars).toContain("&::before,");
    expect(bars).toContain("&::after");
    expect(bars).toContain("width: 14px");
    expect(bars).toContain("height: 2px");
    expect(bars).toContain("border-radius: 1px");
    expect(bars).toContain("transform: rotate(45deg)");
    expect(bars).toContain("transform: rotate(-45deg)");
    // A glyph would need a size; the bars must not have one, or the button
    // would keep a line box it no longer fills.
    expect(cross()).not.toContain("font-size");
    expect(bars).not.toContain("font-size");
  });

  it("keeps the button empty, so nothing stands behind the bars", () => {
    expect(DIALOG).toContain('class="auth-close"');
    expect(DIALOG).not.toContain("symbols.close");
  });

  it("shows the keyboard where the focus is", () => {
    // The ring follows the button's own rounded edge: an outline in the corner
    // of the card is clipped by the card's edge.
    expect(cross()).toContain("&:focus-visible");
    expect(cross()).toContain("box-shadow: 0 0 0 2px $link");
  });

  it("keeps the 32px target the title reserves room for", () => {
    expect(cross()).toContain("width: 32px");
    expect(cross()).toContain("height: 32px");
    expect(SURFACE).toContain(
      ".auth-surface .auth-card:has(> .auth-close) .auth-title",
    );
  });
});

describe("the weights of the surface", () => {
  /**
   * The site ships PT Sans in two faces, 400 and 700. A weight between them is
   * drawn regular or synthesised depending on the engine.
   */
  it("asks for no weight the font does not have", () => {
    const asked = [...SURFACE.matchAll(/font-weight:\s*([\w]+)/g)].map(
      (m) => m[1],
    );

    expect(
      asked.length,
      "у листа не осталось ни одной насыщенности",
    ).toBeGreaterThan(0);
    expect(
      [...new Set(asked)].filter((w) => !["400", "700"].includes(w)),
      "PT Sans идет в двух начертаниях, 400 и 700; остальное браузер додумывает",
    ).toEqual([]);
  });
});

describe("the secondary action of a guest card", () => {
  it("has one name and one recipe", () => {
    const action = ruleBody(SURFACE, ".auth-surface .auth-action");

    expect(action).toContain("+inline-link-button");
    expect(action).toContain("font-weight: 700");
    // In the label row it takes the size of the label it stands beside: two
    // sizes on one line read as a slip of the typesetting, and a pixel of
    // difference is too little to read as anything else. Which half is the
    // name of the field and which is the act is said by colour and by work.
    expect(
      ruleBody(SURFACE, ".auth-surface .auth-label-row .auth-action"),
    ).toContain("font-size: $auth-font-size");
  });
});

describe("the main button of a guest card", () => {
  /**
   * It is the site's main action at the guest's geometry: the card owns the
   * width, the 52 and the 16px caption, and +button-primary (_Buttons.sass) owns
   * the fill, the ink, the weight, the hover, the press and the ring's step out.
   */
  it("takes the colours of the main action from the site's one mixin", () => {
    const button = ruleBody(SURFACE, ".auth-surface .auth-button");

    expect(button).toContain("+button-primary");
    expect(button).toContain("height: $auth-control-height");
    for (const copy of [
      "background-color: $link",
      "color: $text-on-fill",
      "$primary-hover-overlay",
      "$primary-press-overlay",
      "outline-offset",
    ]) {
      expect(button, `the guest button restates ${copy}`).not.toContain(copy);
    }
  });
});

describe("the box of a guest card", () => {
  /**
   * One box for two jobs, the rule of the site and the caution before a step
   * that cannot be taken back: one geometry, one neutral frame, and the
   * alignment as the only difference. Red is left to the lines under fields and
   * to the status cross.
   */
  it("draws one box, framed like a field and not like a refusal", () => {
    const base = ruleBody(SURFACE, ".auth-surface .auth-warnbox");

    expect(base).toContain("padding: .8rem 1rem");
    expect(base).toContain("margin: 0 0 1rem");
    expect(base).toContain("line-height: 1.5");
    expect(base).toContain("border: 1px solid $border");
    expect(base).not.toContain("$accent-red");
    // The alignment is the modifiers' business, not the base's.
    expect(base).not.toContain("text-align");
  });

  it("centres the rule and the caution, and repeats no geometry doing it", () => {
    const notice = ruleBody(SURFACE, ".auth-surface .auth-warnbox--notice");
    const base = ruleBody(SURFACE, ".auth-surface .auth-warnbox");

    expect(notice).toContain("text-align: center");
    // The gap under the bold opening line belongs to every box of the surface
    // and not to this one modifier: three boxes with two different gaps read as
    // three different boxes.
    expect(base).toContain("margin-bottom: 4px");
    expect(notice).not.toContain("margin-bottom");
    // A modifier that re-declares the base's padding is a second geometry: the
    // difference between the three is the alignment alone.
    expect(notice).not.toContain("padding");
    expect(notice).not.toContain("border");
  });

  // A dead link says what happened in two lines: what probably went wrong,
  // then the way out.
  it("says a refusal in two lines, the second in its own voice", () => {
    for (const screen of DEAD_LINK_SCREENS) {
      expect(
        read("pages", "account", screen),
        screen + ": the second line lost its own voice",
      ).toContain('class="auth-note"');
    }
  });
});

describe("the width of a guest card", () => {
  /**
   * The card and the dialog wrapper around it measure the same, or a click on
   * the gutter between them lands on the dialog instead of closing it.
   */
  it("is written once for the wide card and once for the narrow one", () => {
    const guest = SURFACE + DIALOG;

    expect(times(guest, ": 432px")).toBe(1);
    expect(times(guest, ": 352px")).toBe(1);
    expect(SURFACE).toContain("$auth-card-width: 432px");
    expect(SURFACE).toContain("$auth-card-width-compact: 352px");
  });
});

describe("the rhythm of a guest card", () => {
  /** The guest card stands on the site's 8px grid. */
  it("stands the label one step above its field and keeps three of gutter", () => {
    expect(ruleBody(SURFACE, ".auth-surface .auth-label")).toContain(
      "margin: 0 0 8px",
    );
    expect(ruleBody(SURFACE, ".auth-surface .auth-card")).toContain(
      "padding: 24px",
    );
  });
});

describe("the card that states an outcome", () => {
  /**
   * Width follows what the card holds: the wide card where an address or a
   * password is typed and where the causes of a refusal are listed, the narrow
   * one elsewhere. The screen says which once, in a word that reaches the dialog
   * wrapper and the card inside it together, and the sheet does not work it out
   * from the contents: that took one :has inside another, and a browser drops
   * such a rule whole.
   */
  it("takes its width from the word the screen puts on it", () => {
    expect(ruleBody(SURFACE, ".auth-surface.auth-dialog--compact")).toContain(
      "width: $auth-card-width-compact",
    );
    expect(ruleBody(SURFACE, ".auth-surface .auth-card--compact")).toContain(
      "max-width: $auth-card-width-compact",
    );
    // The one word, on both boxes, from the one prop.
    expect(DIALOG).toContain("'auth-dialog--compact': compact");
    expect(DIALOG).toContain("'auth-card--compact': compact");
  });

  it("is not measured by what it holds", () => {
    const offenders = nestedHas(SURFACE);

    expect(
      offenders,
      "one :has inside another is invalid by Selectors Level 4: the browser drops the whole rule and the screen keeps the width it had",
    ).toEqual([]);
  });

  it("keeps its prose centred even when a field follows it", () => {
    // Owner's call: the sentences stay on the card's own axis and only the
    // controls keep the left one. Asserted as an absence, because what can come
    // back by accident is the rule that pulled the prose left.
    expect(SURFACE, "the prose is pulled left again").not.toContain(
      ".auth-surface .auth-card:has(.auth-field) .auth-text",
    );
    expect(SURFACE, "the prose is pulled left again").not.toContain(
      ".auth-surface .auth-card:has(.auth-field) .auth-note",
    );
  });
});

/**
 * The way out of a guest card. In a dialog the cross is the way out, with
 * Escape and a click outside; a button appears only when the card still has
 * something to do, named by the deed, so a button whose whole job is to close
 * is forbidden. On a page reached from a letter there is no cross: every
 * settled answer carries the way onward as a button named by its destination.
 * docs/conventions/UI_STANDARDS.md ("Dialog Standards") carries the rule whole.
 */
describe("the way out of a guest card", () => {
  const SCREENS = [
    ["features", "auth", "ui"],
    ["pages", "account"],
  ];

  const guestScreens = (): { source: string; shown: string }[] =>
    SCREENS.flatMap((at) =>
      collect(join(CLIENT_SRC, ...at), (name) => name.endsWith(".vue")),
    ).map((file) => ({
      source: readFileSync(file, "utf8"),
      shown: asPath(file),
    }));

  it("gives no card a control that only closes it", () => {
    // The caption between two tags, so the cross keeps its own aria-label: the
    // label names the control for a reader who cannot see the bars, and it is
    // the cross that is being named.
    const offenders = guestScreens()
      .filter(({ source }) => />\s*Закрыть\s*</.test(source))
      .map(({ shown }) => shown);

    expect(
      offenders,
      'кнопка "Закрыть" повторяет крестик: у диалога выход дает крестик, ' +
        "а кнопка появляется, только если на карточке есть что сделать, и " +
        "называется этим делом",
    ).toEqual([]);
  });

  it("draws that one way out itself, and draws nothing beside it", () => {
    // The cross is named for the reader who cannot see it, and it is the whole
    // of what the dialog offers.
    expect(DIALOG).toContain('aria-label="Закрыть"');
    expect(
      />\s*Отмена\s*</.test(DIALOG),
      'у гостевой поверхности нет "Отмены"',
    ).toBe(false);
  });
});
