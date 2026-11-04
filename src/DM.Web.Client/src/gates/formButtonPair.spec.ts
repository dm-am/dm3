/**
 * @vitest-environment node
 */

/**
 * The two buttons at the end of a form are read together: the main action and
 * the "Отмена" beside it, and the reader's question is which of them submits.
 * Each fill against its own backdrop and ink is fillContrast.spec.ts's; this
 * file measures them against each other, in every state and both themes.
 * Thresholds are WCAG 2.1 AA: 4.5 for the caption, 3.0 for the two fills
 * against each other.
 */
import { describe, it, expect } from "vitest";
import {
  AA,
  SURFACES,
  THEMES,
  contrast,
  hex,
  over,
} from "@/shared/lib/testing/themeColours";
import {
  BACKGROUND,
  IMAGE,
  INK,
  blockAt,
  declared,
} from "@/shared/lib/testing/sheetBlocks";

/** A flat wash painted in the image layer: linear-gradient(one, the same one). */
const FLAT_WASH = /^linear-gradient\(\s*([^,()]+)\s*,\s*([^,()]+)\s*\)$/;

/**
 * The one colour of a flat wash. The button scale is painted in the image
 * layer so that a variant's own fill survives under it (see +button), and a
 * wash of two equal stops is the only shape allowed there: a real gradient has
 * no single colour and could not be measured at all.
 */
function wash(image: string): string {
  const found = FLAT_WASH.exec(image);
  if (!found) throw new Error(`${image} is not a flat wash of one colour`);
  if (found[1].trim() !== found[2].trim()) {
    throw new Error(`${image} is a gradient, and a gradient has no one colour`);
  }
  return found[1].trim();
}

/**
 * What a button state actually paints on a surface: the variant's own
 * background-color, then the wash of the scale over it. A state block declares
 * only what it changes, so anything it leaves out is read from the rest block
 * the same way a browser reads it — from the cascade.
 */
function stateFill(
  file: string,
  rest: string[],
  state: string[],
  theme: string,
  surface: string,
): string {
  const restBody = blockAt(file, rest);
  const stateBody =
    state.join(" > ") === rest.join(" > ") ? restBody : blockAt(file, state);
  const base =
    declared(stateBody, BACKGROUND) ?? declared(restBody, BACKGROUND);
  const image = declared(stateBody, IMAGE) ?? declared(restBody, IMAGE);
  if (base === null && image === null) {
    throw new Error(`${file}: ${state.join(" > ")} paints no fill at all`);
  }
  let colour = hex(surface, theme);
  if (base !== null) colour = over(base, colour, theme);
  if (image !== null && image !== "none") {
    colour = over(wash(image), colour, theme);
  }
  return colour;
}

/**
 * The two buttons a form footer puts side by side. The states are the three
 * the scale owns; each is located by its own selector, so a state that stops
 * being declared fails here instead of quietly inheriting the one above it.
 */
type ButtonState = "rest" | "hover" | "press";

interface Button {
  /** What the reader sees, for the failure message. */
  name: string;
  file: string;
  states: Record<ButtonState, string[]>;
}

const BUTTON_STATES: ButtonState[] = ["rest", "hover", "press"];

const ORDINARY: Button = {
  name: "ordinary button",
  file: "assets/styles/_Buttons.sass",
  states: {
    rest: ["@mixin button()"],
    hover: ["@mixin button()", "&:hover:not(:disabled)"],
    press: ["@mixin button()", "&:active:not(:disabled)"],
  },
};

const PRIMARY: Button = {
  name: "primary button",
  file: "assets/styles/_Buttons.sass",
  states: {
    rest: ["@mixin button-primary()"],
    hover: ["@mixin button-primary()", "&:hover:not(:disabled)"],
    press: ["@mixin button-primary()", "&:active:not(:disabled)"],
  },
};

/** The colour the button paints in that state, on that surface. */
const buttonFill = (
  button: Button,
  state: ButtonState,
  theme: string,
  surface: string,
): string =>
  stateFill(
    button.file,
    button.states.rest,
    button.states[state],
    theme,
    surface,
  );

describe("the two buttons at the end of a form, against each other", () => {
  // The floor is the 3.0 of a non-text indicator (WCAG 1.4.11): which of two
  // controls is the main one is information carried by colour alone. Measured
  // in every state and on a page, a card and a window, because a pair that
  // collapses under the pointer fails at the moment the reader is aiming.

  it("finds all six states it measures", () => {
    // A path that stops resolving would throw; a state silently reading the
    // rest block would not, and it would compare the wrong two colours.
    for (const button of [ORDINARY, PRIMARY]) {
      for (const state of BUTTON_STATES) {
        expect(
          () => blockAt(button.file, button.states[state]),
          `${button.name}: no ${state} state`,
        ).not.toThrow();
      }
    }
  });

  it("keeps the main action apart from the one beside it, in every state", () => {
    const offenders: string[] = [];
    for (const theme of THEMES) {
      for (const surface of SURFACES) {
        for (const state of BUTTON_STATES) {
          const main = buttonFill(PRIMARY, state, theme, surface);
          const other = buttonFill(ORDINARY, state, theme, surface);
          const measured = contrast(main, other);
          if (measured >= AA.nonText) continue;
          offenders.push(
            `${theme}, ${state} on ${surface}: the primary paints ${main} and the ordinary button ${other}, ${measured.toFixed(2)}, AA asks ${AA.nonText}`,
          );
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("keeps the caption on the main action readable in every state", () => {
    // The hover and press fills are new colours: the rest state is $link, and
    // both others lighten it. Lightening and not darkening, because over the
    // dark theme's $link a darkening step takes this ratio to 4.44.
    const ink = declared(blockAt(PRIMARY.file, PRIMARY.states.rest), INK);
    expect(ink, "the primary button declares no ink of its own").not.toBeNull();
    const offenders: string[] = [];
    for (const theme of THEMES) {
      for (const state of BUTTON_STATES) {
        // The fill is solid, so the surface under it changes nothing.
        const measured = contrast(
          buttonFill(PRIMARY, state, theme, SURFACES[0]),
          hex(ink as string, theme),
        );
        if (measured >= AA.text) continue;
        offenders.push(
          `${theme}, ${state}: ${ink} on the primary fill is ${measured.toFixed(2)}, AA asks ${AA.text}`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});
