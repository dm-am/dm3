/**
 * @vitest-environment node
 */

/**
 * The guest surface against the prototype it was drawn from.
 *
 * These are the facts of the sheet that no mounted component can be asked
 * about — jsdom applies no stylesheet, so a card that lost its ring, a cross
 * that lost its bars or a box that took the geometry of the box beside it all
 * mount perfectly well and look wrong only on a screen. The sheet is read as
 * text instead, the way focusRing.spec.ts reads the sheets it guards.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const read = (...parts: string[]): string =>
  readFileSync(join(CLIENT_SRC, ...parts), "utf8");

const SURFACE = read("assets", "styles", "AuthSurface.sass");

/**
 * The three screens a dead link lands on. Each says the same two things in the
 * same two voices: what probably went wrong in the card's own type, and the way
 * out a step smaller and grey.
 */
const DEAD_LINK_SCREENS = [
  {
    what: "activation",
    path: ["pages", "account", "AccountActivationPage.vue"],
    note: 'class="auth-note"',
  },
  {
    what: "password reset",
    path: ["pages", "account", "PasswordResetPage.vue"],
    note: 'class="auth-note"',
  },
  {
    what: "username change",
    path: ["pages", "account", "UsernameChangePage.vue"],
    note: 'class="auth-note"',
  },
];
const DIALOG = read("shared", "ui", "Layout", "AuthDialog.vue");
const PASSWORD_INPUT = read(
  "shared",
  "ui",
  "PasswordInput",
  "PasswordInput.vue",
);

/**
 * The body of one rule: everything indented under the line that opens it. Sass
 * has no braces, so the block ends at the first line that starts in the first
 * column — the next rule, or the comment above it.
 */
const block = (source: string, selector: string): string => {
  const lines = source.split("\n");
  const opens = lines.findIndex((line) => line === selector);
  expect(opens, `"${selector}" is not declared`).toBeGreaterThan(-1);

  const body: string[] = [];
  for (let i = opens + 1; i < lines.length; i += 1) {
    const line = lines[i];
    if (line.trim() !== "" && !/^\s/.test(line)) break;
    body.push(line);
  }
  return body.join("\n");
};

/** How many times a literal stands in a text. Declarations, not prose: the
 * number is looked for after a colon, so a comment that mentions it does not
 * count as a second source. */
const times = (source: string, literal: string): number =>
  source.split(literal).length - 1;

/**
 * The sheet with its own string variables written out where they are
 * interpolated.
 *
 * A selector assembled out of a variable reads as one word in the source, and
 * the rule below has to see the selector the browser will see: the nesting that
 * cost the dialogs their width was spelled out in a variable and interpolated
 * into a place that could not take it, which is precisely why reading the
 * source for the text of the selector found nothing wrong with it.
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
  const cross = () => block(SURFACE, ".auth-surface .auth-close");

  it("is two bars and not a glyph", () => {
    // Not the glyph the site uses elsewhere, and the same drawing as
    // the status cross and the in-field one: 14 by 2, rounded by a pixel,
    // crossed at forty-five degrees.
    expect(cross()).toContain("&::before,");
    expect(cross()).toContain("&::after");
    expect(cross()).toContain("width: 14px");
    expect(cross()).toContain("height: 2px");
    expect(cross()).toContain("border-radius: 1px");
    expect(cross()).toContain("transform: rotate(45deg)");
    expect(cross()).toContain("transform: rotate(-45deg)");
    // A glyph would need a size; the bars must not have one, or the button
    // would keep a line box it no longer fills.
    expect(cross()).not.toContain("font-size");
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
   * The site ships PT Sans, and PT Sans is two faces: 400 and 700. A weight
   * between them is not a weight the reader gets. The browser either draws the
   * regular face and slants nothing, or synthesises a heavier one out of it,
   * and which of the two happens depends on the engine - so the line that asked
   * for 500 ("Имя свободно") was a different line on different machines and the
   * same line as its neighbours on most of them.
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
    const action = block(SURFACE, ".auth-surface .auth-action");

    expect(action).toContain("+inline-link-button");
    expect(action).toContain("font-weight: 700");
    // In the label row it takes the size of the label it stands beside: two
    // sizes on one line read as a slip of the typesetting, and a pixel of
    // difference is too little to read as anything else. Which half is the
    // name of the field and which is the act is said by colour and by work.
    expect(
      block(SURFACE, ".auth-surface .auth-label-row .auth-action"),
    ).toContain("font-size: $auth-font-size");
  });
});

describe("the main button of a guest card", () => {
  /**
   * It is the site's main action at the guest's geometry: the card owns the
   * width, the 52 and the 16px caption, and +button-primary (Inputs.sass) owns
   * the fill, the ink, the weight, the lightening hover and press and the
   * ring's step out, for this button and the page's alike. A copy of the
   * colours lived here and was the one place the two could part.
   */
  it("takes the colours of the main action from the site's one mixin", () => {
    const button = block(SURFACE, ".auth-surface .auth-button");

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
   * One box for two jobs: the rule of the site and the caution before a step
   * that cannot be taken back. They used to differ in frame and in geometry
   * both — three paddings and a red frame on all of them — and that was cut
   * down: one geometry, one neutral frame, and the alignment as the only
   * difference. Red is left to the lines under fields and to the status cross,
   * whose job it is.
   *
   * The third block that shares the base, the list of causes behind a refusal,
   * has since had the frame taken off it and is asserted below.
   */
  it("draws one box, framed like a field and not like a refusal", () => {
    const base = block(SURFACE, ".auth-surface .auth-warnbox");

    expect(base).toContain("padding: .8rem 1rem");
    expect(base).toContain("margin: 0 0 1rem");
    expect(base).toContain("line-height: 1.5");
    expect(base).toContain("border: 1px solid $border");
    expect(base).not.toContain("$accent-red");
    // The alignment is the modifiers' business, not the base's.
    expect(base).not.toContain("text-align");
  });

  it("centres the rule and the caution, and repeats no geometry doing it", () => {
    const notice = block(SURFACE, ".auth-surface .auth-warnbox--notice");
    const base = block(SURFACE, ".auth-surface .auth-warnbox");

    expect(notice).toContain("text-align: center");
    // The gap under the bold opening line belongs to every box of the surface
    // and not to this one modifier: three boxes with two different gaps read as
    // three different boxes.
    expect(base).toContain("margin-bottom: 4px");
    expect(notice).not.toContain("margin-bottom");
    // A modifier that re-declares the base's padding is the old model coming
    // back: the difference between the three is the alignment alone.
    expect(notice).not.toContain("padding");
    expect(notice).not.toContain("border");
  });

  // A dead link says what happened in two lines and not in a list inside a
  // box: what probably went wrong, then the way out. Asserted as an absence,
  // because what can come back is the box - three screens used to carry it, and
  // with it a third recipe for a box on a card that already has two.
  it("says a refusal in two lines and not in a box of its own", () => {
    expect(SURFACE, "the box of causes is back").not.toContain(
      ".auth-surface .auth-warnbox--reasons",
    );

    for (const screen of DEAD_LINK_SCREENS) {
      const source = read(...screen.path);
      expect(source, screen.what + ": the box of causes is back").not.toContain(
        "auth-warnbox--reasons",
      );
      expect(
        source,
        screen.what + ": the second line lost its own voice",
      ).toContain(screen.note);
    }
  });
});

describe("the width of a guest card", () => {
  /**
   * The card and the dialog wrapper around it have to measure the same, or a
   * click on the gutter between them lands on the dialog instead of closing
   * it. Written twice, the pair drifted the moment one of the two was tuned.
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
  /**
   * The guest card stands on the site's 8px grid. Both numbers were off
   * it and off each other: the label kept .4rem, and the card kept a different
   * gutter at the top, at the sides and at the foot.
   */
  it("stands the label one step above its field and keeps three of gutter", () => {
    expect(block(SURFACE, ".auth-surface .auth-label")).toContain(
      "margin: 0 0 8px",
    );
    expect(block(SURFACE, ".auth-surface .auth-card")).toContain(
      "padding: 24px",
    );
  });
});

describe("the strip under a field", () => {
  /**
   * Nothing in the strip is indented. It used to begin where the text typed into
   * the field begins, fifteen pixels in, and one line standing to the right of
   * the whole column read as a mistake: the scale showed it worst, its bar drawn
   * across the full width from the left edge and only its caption moved. Every
   * other form of the site prints the reason under a field with no inset either,
   * so the guest surface was the single place with an axis of its own.
   *
   * Asserted as an absence, because that is what can come back by accident: the
   * inset was reintroduced once already.
   */
  it("indents nothing in it", () => {
    // The message keeps its own block for the top margin; what must not come
    // back is a left inset in it.
    expect(
      block(SURFACE, ".auth-surface .auth-field + .auth-message"),
      "the line under a field is indented again",
    ).not.toContain("padding-left");

    expect(
      SURFACE,
      "the inset is used by the controls themselves, not by text",
    ).toContain("$auth-field-inset: 14px");
    expect(SURFACE, "an indented strip is back").not.toContain(
      "padding-left: calc(#{$auth-field-inset} + 1px)",
    );
    expect(SURFACE, "the caption of the scale is indented again").not.toContain(
      ".auth-surface .auth-field + .auth-meter > :not(.strength-bar)",
    );
    expect(SURFACE, "the verdict on a name is indented again").not.toContain(
      ".auth-surface .auth-field .username-status",
    );
  });

  it("holds its place under the code field and says nothing", () => {
    // The shape of the code is shown by the field itself. The strip is held
    // open for the refusal, and it is hidden rather than emptied so its height
    // does not depend on what it happens to hold.
    expect(
      block(
        SURFACE,
        ".auth-surface .auth-message--reserved:not(.auth-message--error)",
      ),
    ).toContain("visibility: hidden");
  });
});

describe("the card that states an outcome", () => {
  /**
   * Width follows what the card holds: the wide card is
   * kept where an address or a password is typed and where the causes of a
   * refusal are listed, and everything else is narrow. WHICH of the two a
   * screen wants is the screen's to say, and it says it once, in a word that
   * reaches the dialog wrapper and the card inside it together: a card that
   * shrank alone would leave a transparent strip inside the lit rectangle, and
   * a click there would land on the dialog instead of closing it.
   *
   * The sheet used to work the width out from the contents of the card, and
   * that is what the two rules below are here to keep out. It cost the dialogs
   * their narrow measure in silence: the width of a dialog sits on the wrapper,
   * so the wrapper's rule had one :has inside another, and a browser drops such
   * a rule whole.
   */
  it("takes its width from the word the screen puts on it", () => {
    expect(block(SURFACE, ".auth-surface.auth-dialog--compact")).toContain(
      "width: $auth-card-width-compact",
    );
    expect(block(SURFACE, ".auth-surface .auth-card--compact")).toContain(
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

describe("the second factor", () => {
  it("sets the code in the surface own face and pays the spacing back", () => {
    const field = block(
      SURFACE,
      ".auth-surface .auth-field input.auth-input--code",
    );

    expect(field).toContain("text-align: center");
    // The spacing is added after the last symbol too, so the same amount comes
    // back as an indent: without it the line sits off centre by half a gap.
    expect(field).toContain("letter-spacing: .3em");
    expect(field).toContain("text-indent: .3em");
    // No family of its own and no height of its own: one field set apart from
    // the rest of the form read as a piece of someone else s.
    expect(field, "the code took a face of its own again").not.toContain(
      "font-family",
    );
    expect(field, "the code took a height of its own again").not.toContain(
      "height:",
    );
  });

  it("sets the longer recovery code tighter", () => {
    const recovery = block(
      SURFACE,
      ".auth-surface .auth-field input.auth-input--code.auth-input--code-recovery",
    );

    expect(recovery).toContain("letter-spacing: .1em");
    expect(recovery).toContain("text-indent: .1em");
  });

  it("takes the switch between the two kinds from the site's own control", () => {
    const pill = block(SURFACE, ".auth-surface .auth-card .auth-segmented");

    // Only what this card changes: the pill runs the full width and the two
    // halves split it. A fill, an edge or a weight written here would be the
    // component redrawn by hand, and a copy drifts from what it copied.
    expect(pill).toContain("width: 50%");
    expect(pill).not.toContain("background");
    expect(pill).not.toContain("border-radius");
    expect(pill).not.toContain("font-weight");
  });
});

describe("the eye of a password field", () => {
  it("is the size of the prototype's and in its ink", () => {
    const toggle = PASSWORD_INPUT.slice(
      PASSWORD_INPUT.indexOf(".password-toggle"),
    );

    expect(toggle).toContain("width: 19px");
    expect(toggle).toContain("height: 19px");
    // Full-strength ink from the start: dimmed until the pointer finds it, the
    // one control of a password field reads as unavailable.
    expect(toggle).toContain("color: $text");
    expect(toggle).not.toContain("color: $text-muted");
  });
});

/**
 * The way out of a guest card, and the one shape it may not take.
 *
 * Two surfaces, one rule, read off whether the card carries a cross.
 *
 * In a dialog the cross is the way out, and Escape and a click outside are the
 * same way out with nothing on screen to say so. A button appears there only
 * when the card still has something to do, and it is named by the deed:
 * "Создать аккаунт", "Отправить письмо", "Попробовать другую почту". A button
 * whose whole job is to close is therefore forbidden. It repeats the cross,
 * takes a line the card does not have, and stands in the place the main action
 * of the card stands in, so the reader compares the two and finds them the same
 * size. A card with nothing left to do keeps no button at all: the receipt
 * after a registration is closed by the cross.
 *
 * On a page reached from a letter there is no cross, because there is nothing
 * to close - the surface is the page. Every answer it settles on carries the
 * way onward as a button named by its destination ("Войти", "На главную",
 * "Перейти в профиль"). The states that have not settled carry nothing: while
 * the link is being checked there is nothing to leave, and the answer arrives
 * on its own.
 *
 * What stands here is the half of the rule the markup can be asked about: the
 * caption that may not come back. docs/conventions/UI_STANDARDS.md ("Dialog
 * Standards") carries the rule whole.
 */
describe("the way out of a guest card", () => {
  const SCREENS = [
    ["features", "auth", "ui"],
    ["pages", "account"],
  ];

  const guestScreens = (): { source: string; shown: string }[] => {
    const found: { source: string; shown: string }[] = [];
    const walk = (at: string[], shown: string) => {
      for (const entry of readdirSync(join(CLIENT_SRC, ...at), {
        withFileTypes: true,
      })) {
        if (entry.isDirectory())
          walk([...at, entry.name], shown + "/" + entry.name);
        else if (entry.name.endsWith(".vue"))
          found.push({
            source: read(...at, entry.name),
            shown: shown + "/" + entry.name,
          });
      }
    };
    for (const at of SCREENS) walk(at, at.join("/"));
    return found;
  };

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
    // of what the dialog offers: the grey footer of Form.vue, with its
    // "Отмена", is not part of this contour.
    expect(DIALOG).toContain('aria-label="Закрыть"');
    expect(
      />\s*Отмена\s*</.test(DIALOG),
      'у гостевой поверхности нет "Отмены"',
    ).toBe(false);
  });
});

describe("the check line between a field and the button", () => {
  /**
   * "Запомнить меня" stands under the strip of the password field and over the
   * button, and the strip is empty until the server refuses. So the row is
   * placed twice, and both placements have to leave the card the same height:
   * the two margins are opposites, and their sum is the 16px the base hangs
   * below itself either way.
   *
   * Measured headless on the login card at 432px, from the frame of the field
   * or from the ink of the refusal down to the box of the checkbox, and from
   * the words of the row down to the button. One pair of margins served both
   * states: 34.2 over the box against 18.1 under the row with the strip empty,
   * and 14.2 against 18.1 with the refusal in it. Now 26.2 / 26.1 and 16.2 /
   * 16.1, and 68.3px from the field to the button in both, as before.
   *
   * Read off the sheet rather than off a mounted card, for the reason at the
   * head of this file: jsdom applies no stylesheet, so the two placements are
   * the same picture to it.
   */
  it("keeps one gap above the row and the same gap below it", () => {
    const empty = block(
      SURFACE,
      ".auth-surface .auth-message + .auth-checkline",
    );
    const refused = block(
      SURFACE,
      ".auth-surface .auth-message--error + .auth-checkline",
    );

    // The two placements answer the two states of the strip, and both leave the
    // same 17.3px above the checkbox and below the row - the gap the consent
    // row of the registration keeps above its own button, measured by ink and
    // not by margin. The card grows when the refusal arrives, which is what the
    // owner asked for. Round margins here measured 15 and 26 against that 17.3.
    expect(empty).toContain("margin-top: -15.7px");
    expect(empty).toContain("margin-bottom: 1rem");
    expect(refused).toContain("margin-top: 5.3px");
    expect(refused).toContain("margin-bottom: 1rem");
  });

  it("leaves the reserved strip alone", () => {
    // The strip's height is what keeps every card of the surface still when the
    // server answers, and only the cards with a check line under it are being
    // placed. A shift written into the strip would move the rest of them.
    expect(
      block(SURFACE, ".auth-surface .auth-field + .auth-message"),
      "сдвиг чек-строки уехал в полосу сообщения и двигает все карточки",
    ).toContain("min-height: calc(#{$auth-strip} - #{$auth-strip-gap})");
  });
});
