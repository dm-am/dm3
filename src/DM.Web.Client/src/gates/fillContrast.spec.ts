/**
 * @vitest-environment node
 */

/**
 * An accent fill carries a label, and the label has to be readable on it. A
 * solid fill is one colour wherever it is painted; a relative one is a
 * translucent overlay with no colour until something is under it, so it is
 * measured on every surface the site sanctions. Each block is located in its
 * own source by its selector path, its declarations are resolved down to the
 * hex literals of ThemeVariables.css, and the contrast is computed for the
 * light and the dark palette apart. A fill is spent on one thing: the blue of
 * $link fills the main action of a form and nothing else, and no button is
 * coloured. Thresholds are WCAG 2.1 AA: 4.5 for body text, 3.0 for large text
 * and for the outline of a control. The pair of buttons at the end of a form is
 * formButtonPair.spec.ts's, the focus ring focusRing.spec.ts's.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  CLIENT_SRC,
  asPath,
  styleFiles,
} from "@/shared/lib/testing/sourceTree";
import {
  AA,
  PALETTES,
  RGBA,
  SURFACES,
  THEMES,
  WRAPPERS,
  channels,
  compose,
  contrast,
  hex,
  paint,
  ratio,
  surfacesOf,
} from "@/shared/lib/testing/themeColours";
import {
  BACKGROUND,
  INK,
  OUTLINE,
  blockAt,
  declared,
  stylesheet,
  type Line,
} from "@/shared/lib/testing/sheetBlocks";

/**
 * The place that paints a label on a fill of its own. A choice wears the
 * active segment's $bg-element-accent and the weight, and a link stays a link;
 * the walk further down keeps $link out of them.
 */
const FILL = {
  name: "main action (+button-primary)",
  file: "assets/styles/_Buttons.sass",
  path: ["@mixin button-primary()"],
};

/** The main action is drawn by one mixin, and these take it instead of a copy. */
const PRIMARY_CALLERS: { file: string; block: string[] }[] = [
  { file: "shared/ui/Button/Button.vue", block: ["button", "&.primary"] },
  {
    file: "assets/styles/_AuthControls.sass",
    block: [".auth-surface .auth-button"],
  },
];

/**
 * Where $link may fill a block that carries a label: the main action, and no
 * other place. A bar with no words on it (a timer running out) is not a label
 * on a fill and is not counted.
 */
const LINK_FILL_ALLOWED = new Set(["assets/styles/_Buttons.sass"]);

/** The coloured buttons the system used to offer, which nothing may call. */
const COLOURED_BUTTONS = ["button-danger", "button-outline"];

describe("contrast of accent fills", () => {
  it("reads the palettes and the wrappers it measures through", () => {
    // Empty maps would let every assertion below pass by measuring nothing.
    for (const theme of THEMES) {
      expect(Object.keys(PALETTES[theme]).length).toBeGreaterThan(50);
    }
    expect(WRAPPERS.size).toBeGreaterThan(50);
  });

  it("gives every fill an ink of its own", () => {
    // A fill with no ink is not a passing pair, it is an unmeasured one.
    const body = blockAt(FILL.file, FILL.path);
    const where = `${FILL.name} (${FILL.file})`;
    expect(declared(body, BACKGROUND), `${where}: no fill`).not.toBeNull();
    expect(declared(body, INK), `${where}: no ink of its own`).not.toBeNull();
  });

  it("keeps the label readable on the fill in both themes", () => {
    const body = blockAt(FILL.file, FILL.path);
    const background = declared(body, BACKGROUND);
    const ink = declared(body, INK);
    if (background === null || ink === null) return;
    const offenders: string[] = [];
    for (const theme of THEMES) {
      for (const { on, colour } of surfacesOf(background, theme)) {
        const measured = contrast(colour, hex(ink, theme));
        if (measured >= AA.text) continue;
        const where = on ? `${background} over ${on}` : background;
        offenders.push(
          `${FILL.name} (${FILL.file}), ${theme}: ${ink} on ${where} is ${measured.toFixed(2)}, AA asks ${AA.text}`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });

  it("keeps the outline of a filled control apart from every surface", () => {
    const body = blockAt(FILL.file, FILL.path);
    const outline = declared(body, OUTLINE);
    if (outline === null) return;
    // A border written as `transparent` draws nothing of its own, so the
    // edge of the control is its fill and the fill is what gets measured
    // here instead. That is the shape the primary button wears: a border in
    // the colour of the fill stays behind when a state changes the fill,
    // and the old line ends up drawn around the new one.
    const edge =
      outline === "transparent" ? declared(body, BACKGROUND) : outline;
    if (edge === null) return;
    const offenders: string[] = [];
    for (const theme of THEMES) {
      for (const surface of SURFACES) {
        const measured = ratio(edge, surface, theme);
        if (measured >= AA.nonText) continue;
        offenders.push(
          `${FILL.name} (${FILL.file}), ${theme}: ${edge} on ${surface} is ${measured.toFixed(2)}, AA asks ${AA.nonText}`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });

  it("colours no button at all", () => {
    // The danger of an action is spelled out by the confirmation it opens.
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const source = readFileSync(file, "utf8");
      for (const mixin of COLOURED_BUTTONS) {
        const called = new RegExp(`^\\s*(\\+|@include\\s+)${mixin}\\b`, "m");
        if (called.test(source)) offenders.push(`${asPath(file)}: +${mixin}`);
      }
    }
    expect(offenders).toEqual([]);
    for (const sheet of ["Inputs.sass", "_Buttons.sass", "_Links.sass"]) {
      const source = readFileSync(
        join(CLIENT_SRC, "assets/styles", sheet),
        "utf8",
      );
      for (const mixin of COLOURED_BUTTONS) {
        expect(source, `@mixin ${mixin} is offered again`).not.toMatch(
          new RegExp(`^(@mixin\\s+|=)${mixin}\\b`, "m"),
        );
      }
    }
  });

  it("draws the main action from one mixin", () => {
    for (const { file, block: path } of PRIMARY_CALLERS) {
      const body = blockAt(file, path);
      const where = `${file}: ${path.join(" > ")}`;
      expect(
        body.map((line) => line.text),
        `${where} takes the main action's colours from +button-primary`,
      ).toContain("+button-primary");
      // The block's own declarations and those of a bare "&" under it, the
      // shape a declaration takes when it has to follow a mixin.
      const own = Math.min(...body.map((line) => line.indent));
      const flat: Line[] = [];
      let bare = -1;
      for (const line of body) {
        if (bare >= 0 && line.indent <= bare) bare = -1;
        if (line.indent === own && line.text === "&") bare = own;
        else if (line.indent === own || (bare >= 0 && line.indent === own + 2))
          flat.push({ indent: own, text: line.text });
      }
      expect(declared(flat, BACKGROUND), `${where} fills it again`).toBeNull();
      expect(declared(flat, INK), `${where} inks it again`).toBeNull();
    }
  });

  it("fills a label with $link on the main action and nowhere else", () => {
    // A selected tag and an active group are choices: the group wears the
    // active segment's look, the tag of the creation form its weight on the
    // bubble's fill; a hovered link stays a link. Blue under words on a page
    // is the one button that submits.
    const offenders: string[] = [];
    let seen = 0;
    for (const file of styleFiles()) {
      const path = asPath(file);
      const lines = stylesheet(path);
      lines.forEach((line, at) => {
        if (!/^background(-color)?:\s+\$link$/.test(line.text)) return;
        seen += 1;
        // The declarations of the block this one stands in: the run of lines
        // at its indent, nested rules skipped.
        let from = at;
        while (from > 0 && lines[from - 1].indent >= line.indent) from -= 1;
        let to = at;
        while (to < lines.length - 1 && lines[to + 1].indent >= line.indent)
          to += 1;
        const labelled = lines
          .slice(from, to + 1)
          .some(
            (one) => one.indent === line.indent && /^color:\s/.test(one.text),
          );
        if (labelled && !LINK_FILL_ALLOWED.has(path))
          offenders.push(`${path}: ${line.text} under a label`);
      });
    }
    // The walk reaches the sources: the main action's own fill is found.
    expect(seen).toBeGreaterThan(0);
    expect(offenders).toEqual([]);
  });

  it("keeps $link-hover out of fills, no ink of the site reads on it", () => {
    // The ban is measured, not remembered: $link-hover sits at the luminance
    // where both inks the site owns fall short in the light theme.
    const best = Math.max(
      ratio("$link-hover", "$text-on-fill", "light"),
      ratio("$link-hover", "$text", "light"),
    );
    expect(
      best,
      "$link-hover became fillable, re-measure before using it",
    ).toBeLessThan(AA.text);

    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const source = readFileSync(file, "utf8");
      if (/^\s*background(-color)?:\s*\$link-hover\s*$/m.test(source))
        offenders.push(asPath(file));
    }
    expect(offenders).toEqual([]);
  });
});

describe("the ground of a field", () => {
  // Every field of one line on a page takes the ground as an overlay, because
  // a form stands on a card of $bg-element as often as on the page. So the
  // overlay is calibrated to be $bg-element over the page, in both themes, and
  // to part from the card it may stand on.
  const FIELD = "$input-bg-overlay";

  it("is $bg-element over the page, in both themes", () => {
    for (const theme of THEMES) {
      const overlay = RGBA.exec(paint(FIELD, theme));
      expect(overlay, `${theme}: ${FIELD} is not an overlay`).not.toBeNull();
      expect(
        compose(overlay as RegExpExecArray, hex("$bg-page", theme)),
        theme,
      ).toBe(hex("$bg-element", theme).toLowerCase());
    }
  });

  it("parts from a card of $bg-element it stands on", () => {
    for (const theme of THEMES) {
      const card = hex("$bg-element", theme);
      const ground = compose(
        RGBA.exec(paint(FIELD, theme)) as RegExpExecArray,
        card,
      );
      const step = Math.abs(channels(ground)[0] - channels(card)[0]);
      expect(step, `${theme}: ${ground} on ${card}`).toBeGreaterThanOrEqual(4);
    }
  });

  it("has a solid twin for autofill that is the same colour over the page", () => {
    // Chrome paints an autofilled field its own colour, and the one cover that
    // works is an inset shadow, which takes no alpha: the twin has to be the
    // overlay flattened, or an autofilled field stands out of its form.
    for (const theme of THEMES) {
      expect(hex("$input-bg-autofill", theme).toLowerCase(), theme).toBe(
        compose(
          RGBA.exec(paint(FIELD, theme)) as RegExpExecArray,
          hex("$bg-page", theme),
        ),
      );
    }
  });
});
