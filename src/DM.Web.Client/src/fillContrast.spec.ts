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
 * under it. The primary button is the second kind: composed over $bg-page it
 * is $bg-element-accent exactly, composed over a form footer (which IS
 * $bg-element-accent) it is a step darker again. Such a fill is therefore
 * measured on every surface the site sanctions, not on one.
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

/** Every place that paints a label on a fill of its own. */
const FILLS: Fill[] = [
  {
    name: "primary submit button",
    file: "shared/ui/Button/Button.vue",
    path: ["button", "&.primary"],
    size: "text",
  },
  {
    name: "destructive button",
    file: "assets/styles/Inputs.sass",
    path: ["@mixin button-danger()"],
    size: "text",
  },
  {
    name: "unread counter of the mobile header",
    file: "widgets/header/Header.vue",
    path: [".counter-value"],
    size: "text",
  },
  {
    name: "active tab of the subscriptions filter",
    file: "pages/personal/SubscriptionsPage.vue",
    path: [".tabs", "button", "&.active"],
    size: "text",
  },
  {
    name: "selected tag chip",
    file: "entities/game/ui/TagSelector.vue",
    path: [".tag-button", "&.selected"],
    size: "text",
  },
  {
    name: "active tag group of the moderation list",
    file: "pages/moderation/ModerationTags.vue",
    path: [".group-item", "&.active"],
    size: "text",
  },
  {
    name: "hovered notification link",
    file: "pages/personal/NotificationsPage.vue",
    path: [".view-link", "&:hover"],
    size: "text",
  },
];

/** Destructive buttons take the pair from one mixin instead of copying it. */
const DANGER_CALLERS = [
  "shared/ui/ConfirmDialog/ConfirmDialog.vue",
  "shared/ui/StatusButtons/StatusButtons.vue",
  "pages/game/CharacterEdit.vue",
];

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
const INK = /^color$/;
const OUTLINE = /^border-color$/;

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
      const outline = declared(block(fill.file, fill.path), OUTLINE);
      if (outline === null) continue;
      for (const theme of THEMES) {
        for (const surface of SURFACES) {
          const measured = ratio(outline, surface, theme);
          if (measured >= AA.nonText) continue;
          offenders.push(
            `${fill.name} (${fill.file}), ${theme}: ${outline} on ${surface} is ${measured.toFixed(2)}, AA asks ${AA.nonText}`,
          );
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("keeps the destructive pair in one place", () => {
    for (const file of DANGER_CALLERS) {
      const source = readFileSync(join(CLIENT_SRC, file), "utf8");
      expect(
        source,
        `${file}: the danger button must take +button-danger`,
      ).toContain("+button-danger");
      expect(
        source,
        `${file}: a hand-rolled red fill brings back the ink it never declares`,
      ).not.toMatch(/^\s*background(-color)?:\s*\$accent-red\s*$/m);
    }
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

describe("contrast of the keyboard focus ring on a form control", () => {
  // One block, because the ring is declared once: InputsGlobal.sass applies
  // @mixin input() to input, textarea and select for the whole document, so
  // this is the only place the ring can be given to all three — and the only
  // place it can go missing for all three at once, which is what happened.
  const FILE = "assets/styles/Inputs.sass";
  const RING = ["@mixin input()", "&:focus-visible"];

  /** The colour of the ring, taken out of the shorthand it is written in. */
  function ringColour(): string {
    const outline = declared(block(FILE, RING), /^outline$/);
    if (outline === null) {
      throw new Error("@mixin input() declares no focus outline");
    }
    const parts = outline.split(/\s+/);
    return parts[parts.length - 1];
  }

  it("draws a ring at all, and draws it from a token", () => {
    // A hex here would be outside the palettes and unmeasurable below, which is
    // the same as unmeasured.
    expect(declared(block(FILE, RING), /^outline$/)).toMatch(
      /^\d+px solid \$[\w-]+$/,
    );
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
