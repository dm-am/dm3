/**
 * @vitest-environment node
 */

/**
 * An accent fill carries a label, and the label has to be readable on it.
 *
 * A fill comes in two shapes. A solid one is one colour wherever it is
 * painted, and one ratio settles it. A relative one is a translucent overlay —
 * the shape UI_STANDARDS asks of a control, so that it reads on the page, on a
 * card and on a modal footer alike — and it has no colour until something is
 * under it. The ordinary button is the second kind: on $bg-page its resting
 * fill composes to #f2f2f2, on a form footer (which IS $bg-element-accent) to
 * #dedede. Such a fill is therefore measured on every surface the site
 * sanctions, not on one.
 *
 * The pairs are not restated here. Each block below is located in its own
 * source by its selector path, its own background and colour declarations are
 * resolved through the Sass wrappers of Themes.sass down to the hex literals of
 * ThemeVariables.css, and the contrast is computed for the light and the dark
 * palette apart. Apart, because the two palettes are different colours and the
 * defect this guards lived in one of them at a time: in the dark theme
 * --accent-green and --text-on-green are the same hex, so the unread counter
 * was a green pill with no digit inside it (ratio 1.00), while the light theme
 * showed the same digit at 1.15.
 *
 * Every fill above is measured against its own backdrop and its own ink, and
 * that is not enough on its own. The two buttons at the end of a form are read
 * TOGETHER: the main action and the "Отмена" beside it, and the reader's
 * question is not whether either is legible but which of them is the one that
 * submits. Both used to pass every rule in this file while being the same grey
 * a step apart, so a block below measures them against each other, in every
 * state and both themes. A defect that survives one measurement usually
 * survives because nobody compared the two things the eye compares.
 *
 * And a fill is spent on one thing (owner's standard, 27 September 2026): the
 * blue of $link fills the main action of a form and nothing else a reader
 * could take for one, and no button on the site is coloured at all. A
 * selected tag, an active group or a hovered link filled with $link was a
 * second main action on the page; a red or a green button said in colour what
 * the confirmation behind it already says in words.
 *
 * The keyboard focus ring is measured here too, for the same reason and by the
 * same machinery: it is a non-text indicator, its colour is a token, the surface
 * under it is a token, and the ratio is therefore a fact about the sources. It
 * got its own block at the bottom of this file when the form controls turned
 * out to mark focus by tinting their border from $border to $border-focus —
 * 1.77 in the light theme, where the floor is 3.0.
 *
 * Thresholds are WCAG 2.1 AA: 4.5 for body text, 3.0 for large text, 3.0 for
 * the outline of a control against the surface behind it.
 *
 * Sources and not a rendered app: these declarations live in scoped <style>
 * blocks that never coexist in one DOM, and each of them is reached only
 * through a state (hover, active tab, unread count) that a render test would
 * have to stage site by site.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/** WCAG 2.1 AA. */
const AA = { text: 4.5, largeText: 3.0, nonText: 3.0 };

const THEMES = ["light", "dark"];

/** Surfaces a control may sit on: its outline has to read on every one. */
const SURFACES = ["$bg-page", "$bg-element", "$bg-element-accent"];

type Size = "text" | "largeText";

interface Fill {
  /** What the reader sees, for the failure message. */
  name: string;
  file: string;
  /** Selector path from the top of the stylesheet down to the block. */
  path: string[];
  size: Size;
}

/**
 * Every place that paints a label on a fill of its own.
 *
 * The selected tag, the active tag group, the active tab of the subscriptions
 * and the hovered notification link stood here as fills of $link or of green.
 * Under the standard none of them is a fill of a colour: a choice wears the
 * active segment's $bg-element-accent and the weight, and a link stays a
 * link. The tag of the game creation form is the one choice told by its weight
 * alone: it wears the bubble of an applied filter, which has no chosen look
 * (12.10). The walk further down keeps $link out of them.
 */
const FILLS: Fill[] = [
  {
    name: "main action (+button-primary)",
    file: "assets/styles/Inputs.sass",
    path: ["@mixin button-primary()"],
    size: "text",
  },
];

/**
 * The main action is drawn by one mixin, and these take it instead of a copy:
 * the page's button and the guest card's. A copy of the fill lived in each and
 * was one hover away from drifting.
 */
const PRIMARY_CALLERS: { file: string; block: string[] }[] = [
  { file: "shared/ui/Button/Button.vue", block: ["button", "&.primary"] },
  {
    file: "assets/styles/AuthSurface.sass",
    block: [".auth-surface .auth-button"],
  },
];

/**
 * Where $link may fill a block that carries a label: the main action, and no
 * other place. A bar with no words on it (a timer running out) is not a label
 * on a fill and is not counted.
 */
const LINK_FILL_ALLOWED = new Set(["assets/styles/Inputs.sass"]);

/** The coloured buttons the system used to offer, which nothing may call. */
const COLOURED_BUTTONS = ["button-danger", "button-outline"];

/**
 * Nowhere fills with $link-hover. The one entry this map ever held was the
 * mockup catalog of the pending primary-button pick, and it went away with
 * the decision, exactly as its own note promised.
 */
const ALLOWED_LINK_HOVER_FILL: Record<string, string> = {};

// --- theme palettes -------------------------------------------------------

type Palette = Record<string, string>;

const THEME_SOURCE = readFileSync(
  join(CLIENT_SRC, "assets/styles/ThemeVariables.css"),
  "utf8",
).replace(/\/\*[\s\S]*?\*\//g, "");

function palette(header: string): Palette {
  const at = THEME_SOURCE.indexOf(header);
  if (at < 0) throw new Error(`ThemeVariables.css has no "${header}" block`);
  const body = THEME_SOURCE.slice(at + header.length);
  const tokens: Palette = {};
  const declarations = body.slice(0, body.indexOf("}"));
  for (const [, name, value] of declarations.matchAll(
    /^\s*--([\w-]+):\s*([^;]+);/gm,
  )) {
    tokens[name] = value.trim();
  }
  return tokens;
}

const PALETTES: Record<string, Palette> = {
  light: palette("html.theme_Light {"),
  dark: palette("html.theme_Dark {"),
};

/** $token -> --token, the wrapper layer the stylesheets actually write. */
const WRAPPERS = new Map<string, string>();
for (const [, name, token] of readFileSync(
  join(CLIENT_SRC, "assets/styles/Themes.sass"),
  "utf8",
).matchAll(/^\$([\w-]+):\s*var\(--([\w-]+)\)\s*$/gm)) {
  WRAPPERS.set(`$${name}`, token);
}

const NAMED: Record<string, string> = { white: "#ffffff", black: "#000000" };

/** A translucent overlay: the shape of a relative control fill. */
const RGBA =
  /^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)$/;

/**
 * Resolves a declaration value through the wrappers down to what CSS paints:
 * a hex, or the `rgba(...)` of a relative fill.
 */
function paint(value: string, theme: string): string {
  let current = value.trim();
  for (let step = 0; step < 8; step += 1) {
    if (current.startsWith("$")) {
      const token = WRAPPERS.get(current);
      if (!token) throw new Error(`Themes.sass has no wrapper for ${current}`);
      current = `var(--${token})`;
      continue;
    }
    const alias = /^var\(--([\w-]+)\)$/.exec(current);
    if (alias) {
      const next = PALETTES[theme][alias[1]];
      if (next === undefined) {
        throw new Error(`the ${theme} palette has no --${alias[1]}`);
      }
      current = next.trim();
      continue;
    }
    if (current in NAMED) return NAMED[current];
    if (/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(current)) return current;
    if (RGBA.test(current)) return current;
    throw new Error(`${current} is not a colour a contrast can be read from`);
  }
  throw new Error(`${value} does not resolve to a colour`);
}

/** Resolves a declaration value to the one hex a browser would paint. */
function hex(value: string, theme: string): string {
  const painted = paint(value, theme);
  if (RGBA.test(painted)) {
    throw new Error(
      `${value} is a relative fill: it has no colour until a surface is under it`,
    );
  }
  return painted;
}

/** The three channels of a hex, short form expanded. */
function channels(colour: string): number[] {
  const digits = colour.slice(1);
  const full =
    digits.length === 3 ? digits.replace(/./g, (one) => one + one) : digits;
  return [0, 2, 4].map((at) => parseInt(full.slice(at, at + 2), 16));
}

/** An overlay laid over a backdrop, the way a browser composes the two. */
function compose(overlay: RegExpExecArray, backdrop: string): string {
  const alpha = overlay[4] === undefined ? 1 : Number(overlay[4]);
  const under = channels(backdrop);
  return `#${[1, 2, 3]
    .map((at) =>
      Math.round(Number(overlay[at]) * alpha + under[at - 1] * (1 - alpha)),
    )
    .map((value) => value.toString(16).padStart(2, "0"))
    .join("")}`;
}

/**
 * What a fill actually paints, and where. A solid colour paints itself and is
 * one entry; a relative one paints a different colour on every surface the
 * site sanctions, and every one of them is measured.
 */
function surfacesOf(
  value: string,
  theme: string,
): { on: string; colour: string }[] {
  const painted = paint(value, theme);
  const overlay = RGBA.exec(painted);
  if (!overlay) return [{ on: "", colour: painted }];
  return SURFACES.map((surface) => ({
    on: surface,
    colour: compose(overlay, hex(surface, theme)),
  }));
}

function luminance(colour: string): number {
  const digits = colour.slice(1);
  const full =
    digits.length === 3 ? digits.replace(/./g, (one) => one + one) : digits;
  const [r, g, b] = [0, 2, 4]
    .map((at) => parseInt(full.slice(at, at + 2), 16) / 255)
    .map((c) =>
      c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4),
    );
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(one: string, other: string): number {
  const a = luminance(one);
  const b = luminance(other);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

const ratio = (fill: string, ink: string, theme: string): number =>
  contrast(hex(fill, theme), hex(ink, theme));

// --- stylesheet blocks ----------------------------------------------------

interface Line {
  indent: number;
  text: string;
}

const STYLE_BLOCK = /<style[^>]*>([\s\S]*?)<\/style>/g;

function stylesheet(file: string): Line[] {
  const source = readFileSync(join(CLIENT_SRC, file), "utf8");
  const body = file.endsWith(".vue")
    ? Array.from(source.matchAll(STYLE_BLOCK), (found) => found[1]).join("\n")
    : source;
  return body
    .split("\n")
    .map((raw) => raw.replace(/\r$/, ""))
    .filter((raw) => raw.trim() !== "" && !raw.trim().startsWith("//"))
    .map((raw) => ({
      indent: raw.length - raw.trimStart().length,
      text: raw.trim(),
    }));
}

/** The body of the block at the given selector path, its own lines included. */
function block(file: string, path: string[]): Line[] {
  const lines = stylesheet(file);
  let from = 0;
  let to = lines.length;
  for (const selector of path) {
    const childIndent = lines[from].indent;
    let at = -1;
    for (let i = from; i < to; i += 1) {
      if (lines[i].indent === childIndent && lines[i].text === selector) {
        at = i;
        break;
      }
    }
    if (at < 0) {
      throw new Error(`${file} has no "${selector}" in ${path.join(" > ")}`);
    }
    from = at + 1;
    to = at + 1;
    while (to < lines.length && lines[to].indent > childIndent) to += 1;
  }
  return lines.slice(from, to);
}

/** The last value the block declares for the property, or null. */
function declared(body: Line[], property: RegExp): string | null {
  if (body.length === 0) return null;
  const own = Math.min(...body.map((line) => line.indent));
  let value: string | null = null;
  for (const line of body) {
    if (line.indent !== own) continue;
    const found = /^([a-z-]+):\s+(.+)$/.exec(line.text);
    if (found && property.test(found[1])) value = found[2].trim();
  }
  return value;
}

const BACKGROUND = /^background(-color)?$/;
const IMAGE = /^background-image$/;
const INK = /^color$/;
const OUTLINE = /^border-color$/;

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

/** One layer of a fill, laid over what is already there. */
function over(value: string, backdrop: string, theme: string): string {
  if (value === "transparent" || value === "none") return backdrop;
  const painted = paint(value, theme);
  const overlay = RGBA.exec(painted);
  return overlay ? compose(overlay, backdrop) : painted;
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
  const restBody = block(file, rest);
  const stateBody =
    state.join(" > ") === rest.join(" > ") ? restBody : block(file, state);
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
  file: "assets/styles/Inputs.sass",
  states: {
    rest: ["@mixin button()"],
    hover: ["@mixin button()", "&:hover:not(:disabled)"],
    press: ["@mixin button()", "&:active:not(:disabled)"],
  },
};

const PRIMARY: Button = {
  name: "primary button",
  file: "assets/styles/Inputs.sass",
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

// --- source walk ----------------------------------------------------------

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (STYLE_FILES.some((ext) => name.endsWith(ext))) {
      out.push(full);
    }
  }
  return out;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

describe("contrast of accent fills", () => {
  it("reads the palettes and the wrappers it measures through", () => {
    // Empty maps would let every assertion below pass by measuring nothing.
    for (const theme of THEMES) {
      expect(Object.keys(PALETTES[theme]).length).toBeGreaterThan(50);
    }
    expect(WRAPPERS.size).toBeGreaterThan(50);
  });

  it("gives every fill an ink of its own", () => {
    // A fill with no ink is not a passing pair, it is an unmeasured one: the
    // label falls back to whatever the surrounding control declared, which is
    // how four destructive buttons ended up with $text on $accent-red.
    for (const fill of FILLS) {
      const body = block(fill.file, fill.path);
      const where = `${fill.name} (${fill.file})`;
      expect(declared(body, BACKGROUND), `${where}: no fill`).not.toBeNull();
      expect(declared(body, INK), `${where}: no ink of its own`).not.toBeNull();
    }
  });

  it("keeps the label readable on the fill in both themes", () => {
    const offenders: string[] = [];
    for (const fill of FILLS) {
      const body = block(fill.file, fill.path);
      const background = declared(body, BACKGROUND);
      const ink = declared(body, INK);
      if (background === null || ink === null) continue;
      for (const theme of THEMES) {
        for (const { on, colour } of surfacesOf(background, theme)) {
          const measured = contrast(colour, hex(ink, theme));
          if (measured >= AA[fill.size]) continue;
          const where = on ? `${background} over ${on}` : background;
          offenders.push(
            `${fill.name} (${fill.file}), ${theme}: ${ink} on ${where} is ${measured.toFixed(2)}, AA asks ${AA[fill.size]}`,
          );
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("keeps the outline of a filled control apart from every surface", () => {
    const offenders: string[] = [];
    for (const fill of FILLS) {
      const body = block(fill.file, fill.path);
      const outline = declared(body, OUTLINE);
      if (outline === null) continue;
      // A border written as `transparent` draws nothing of its own, so the
      // edge of the control is its fill and the fill is what gets measured
      // here instead. That is the shape the primary button wears: a border in
      // the colour of the fill stays behind when a state changes the fill,
      // and the old line ends up drawn around the new one.
      const edge =
        outline === "transparent" ? declared(body, BACKGROUND) : outline;
      if (edge === null) continue;
      for (const theme of THEMES) {
        for (const surface of SURFACES) {
          const measured = ratio(edge, surface, theme);
          if (measured >= AA.nonText) continue;
          offenders.push(
            `${fill.name} (${fill.file}), ${theme}: ${edge} on ${surface} is ${measured.toFixed(2)}, AA asks ${AA.nonText}`,
          );
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("colours no button at all", () => {
    // The red fill and the outlined pill in a colour were the last coloured
    // buttons: a destructive action in a danger zone, "Отписаться" and
    // "Отметить все прочитанными" over a list. The danger of an action is
    // spelled out by the confirmation it opens, as it is in every window, and
    // on a page a colour belongs to the main action alone.
    const offenders: string[] = [];
    for (const file of collect(CLIENT_SRC)) {
      const source = readFileSync(file, "utf8");
      for (const mixin of COLOURED_BUTTONS) {
        const called = new RegExp(`^\\s*(\\+|@include\\s+)${mixin}\\b`, "m");
        if (called.test(source)) offenders.push(`${asPath(file)}: +${mixin}`);
      }
    }
    expect(offenders).toEqual([]);
    // Nor are they offered: a variant on the shelf is the next caller's
    // shortcut back to a coloured button.
    const inputs = readFileSync(
      join(CLIENT_SRC, "assets/styles/Inputs.sass"),
      "utf8",
    );
    for (const mixin of COLOURED_BUTTONS) {
      expect(inputs, `@mixin ${mixin} is offered again`).not.toMatch(
        new RegExp(`^(@mixin\\s+|=)${mixin}\\b`, "m"),
      );
    }
  });

  it("draws the main action from one mixin", () => {
    for (const { file, block: path } of PRIMARY_CALLERS) {
      const body = block(file, path);
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
    for (const file of collect(CLIENT_SRC)) {
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
    // where the two inks the site owns both fall short in the light theme,
    // which is exactly what the primary button's hover state used to be.
    const best = Math.max(
      ratio("$link-hover", "$text-on-fill", "light"),
      ratio("$link-hover", "$text", "light"),
    );
    expect(
      best,
      "$link-hover became fillable, re-measure before using it",
    ).toBeLessThan(AA.text);

    const offenders: string[] = [];
    const allowed: string[] = [];
    for (const file of collect(CLIENT_SRC)) {
      const source = readFileSync(file, "utf8");
      if (!/^\s*background(-color)?:\s*\$link-hover\s*$/m.test(source))
        continue;
      const path = asPath(file);
      (path in ALLOWED_LINK_HOVER_FILL ? allowed : offenders).push(path);
    }
    expect(offenders).toEqual([]);
    // The walk really reaches the sources: the one sanctioned use is found.
    expect(allowed.sort()).toEqual(Object.keys(ALLOWED_LINK_HOVER_FILL).sort());
  });
});

describe("the two buttons at the end of a form, against each other", () => {
  // The rule the file was missing. Every other block here measures one fill
  // against the surface under it or the ink on it, and the primary button
  // passed all of them while being the ordinary button plus four per cent of
  // black: on the $bg-element-accent of the grey footer some forms had then,
  // the pair measured 1.07, which is one control drawn twice. Nothing broke,
  // which is why it stood — a reader hunting for the button that submits
  // simply had to read the words. The footer is gone; the pair stands on a
  // page, a card or a window, and all three surfaces are measured.
  //
  // The floor is the 3.0 of a non-text indicator (WCAG 1.4.11): which of two
  // controls is the main one is exactly that, information carried by colour
  // alone. It is measured in every state, because a pair that separates at
  // rest and collapses under the pointer is a pair that fails at the moment
  // the reader is aiming.

  it("finds all six states it measures", () => {
    // A path that stops resolving would throw; a state silently reading the
    // rest block would not, and it would compare the wrong two colours.
    for (const button of [ORDINARY, PRIMARY]) {
      for (const state of BUTTON_STATES) {
        expect(
          () => block(button.file, button.states[state]),
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
    const ink = declared(block(PRIMARY.file, PRIMARY.states.rest), INK);
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

describe("contrast of the keyboard focus ring on a form control", () => {
  // One block, because the ring is declared once: InputsGlobal.sass applies
  // @mixin input() to input, textarea and select for the whole document, so
  // this is the only place the ring can be given to all three — and the only
  // place it can go missing for all three at once, which is what happened.
  // The field ring is the frame itself: on focus the border turns $link and a
  // 1px shadow of the same token sits against it, two pixels of one colour
  // following the field's own shape. It lives in +input-base, which +input
  // includes, so every field takes it - and every field would lose it at once.
  const FILE = "assets/styles/Inputs.sass";
  const RING = ["@mixin input-base()", "&:focus"];

  /** The colour of the ring: the token the frame turns on focus. */
  function ringColour(): string {
    const colour = declared(block(FILE, RING), /^border-color$/);
    if (colour === null) {
      throw new Error("@mixin input-base() turns no border colour on focus");
    }
    return colour;
  }

  it("draws a ring at all, and draws it from a token", () => {
    // A hex here would be outside the palettes and unmeasurable below, which is
    // the same as unmeasured. The shadow must be the same token as the frame:
    // two colours would be two rings.
    const body = block(FILE, RING);
    const colour = declared(body, /^border-color$/);
    expect(colour).toMatch(/^\$[\w-]+$/);
    expect(declared(body, /^box-shadow$/)).toBe(`0 0 0 1px ${colour}`);
  });

  it("keeps the ring apart from every surface a field sits on", () => {
    const colour = ringColour();
    const offenders: string[] = [];
    for (const theme of THEMES) {
      for (const surface of SURFACES) {
        const measured = ratio(colour, surface, theme);
        if (measured >= AA.nonText) continue;
        offenders.push(
          `${theme}: ${colour} on ${surface} is ${measured.toFixed(2)}, AA asks ${AA.nonText}`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("the ground of a field", () => {
  // Every field of one line on a page is drawn after the search of the filter
  // bar as it stood before 27 September (owner's standard, 28 September 2026),
  // and that search stood on a solid $bg-element. A field takes the ground as
  // an overlay instead, because a form stands on a card of $bg-element as
  // often as on the page (the account's sections, the new game): a solid
  // $bg-element there is the card itself, and the field is a frame round
  // nothing. So the overlay is calibrated to BE $bg-element over the page, in
  // both themes, and to part from the card it may stand on.
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
