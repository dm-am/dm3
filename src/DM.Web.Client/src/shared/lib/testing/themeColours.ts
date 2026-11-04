/**
 * The colours of the two themes from ThemeVariables.css and Themes.sass, and
 * WCAG 2.1 contrast. A translucent fill is composed over every sanctioned
 * surface. Spec-only.
 */
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { CLIENT_SRC } from "./sourceTree";

/** WCAG 2.1 AA. */
export const AA = { text: 4.5, largeText: 3.0, nonText: 3.0 };

export const THEMES = ["light", "dark"];

/** Surfaces a control may sit on: its outline has to read on every one. */
export const SURFACES = ["$bg-page", "$bg-element", "$bg-element-accent"];

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

const LIGHT = palette("html.theme_Light {");

/** The dark theme declares only what differs; the rest comes from :root, as in the browser. */
export const PALETTES: Record<string, Palette> = {
  light: LIGHT,
  dark: { ...LIGHT, ...palette("html.theme_Dark {") },
};

/** $token -> --token, the wrapper layer the stylesheets actually write. */
export const WRAPPERS = new Map<string, string>();
for (const [, name, token] of readFileSync(
  join(CLIENT_SRC, "assets/styles/Themes.sass"),
  "utf8",
).matchAll(/^\$([\w-]+):\s*var\(--([\w-]+)\)\s*$/gm)) {
  WRAPPERS.set(`$${name}`, token);
}

const NAMED: Record<string, string> = { white: "#ffffff", black: "#000000" };

/** A translucent overlay: the shape of a relative control fill. */
export const RGBA =
  /^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)$/;

/**
 * Resolves a declaration value through the wrappers down to what CSS paints:
 * a hex, or the `rgba(...)` of a relative fill.
 */
export function paint(value: string, theme: string): string {
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
export function hex(value: string, theme: string): string {
  const painted = paint(value, theme);
  if (RGBA.test(painted)) {
    throw new Error(
      `${value} is a relative fill: it has no colour until a surface is under it`,
    );
  }
  return painted;
}

/** The three channels of a hex, short form expanded. */
export function channels(colour: string): number[] {
  const digits = colour.slice(1);
  const full =
    digits.length === 3 ? digits.replace(/./g, (one) => one + one) : digits;
  return [0, 2, 4].map((at) => parseInt(full.slice(at, at + 2), 16));
}

/** An overlay laid over a backdrop, the way a browser composes the two. */
export function compose(overlay: RegExpExecArray, backdrop: string): string {
  const alpha = overlay[4] === undefined ? 1 : Number(overlay[4]);
  const under = channels(backdrop);
  return `#${[1, 2, 3]
    .map((at) =>
      Math.round(Number(overlay[at]) * alpha + under[at - 1] * (1 - alpha)),
    )
    .map((value) => value.toString(16).padStart(2, "0"))
    .join("")}`;
}

/** What a fill paints: itself, or one colour per sanctioned surface. */
export function surfacesOf(
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
  const [r, g, b] = channels(colour)
    .map((channel) => channel / 255)
    .map((c) =>
      c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4),
    );
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function contrast(one: string, other: string): number {
  const a = luminance(one);
  const b = luminance(other);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

export const ratio = (fill: string, ink: string, theme: string): number =>
  contrast(hex(fill, theme), hex(ink, theme));

/** One layer of a fill, laid over what is already there. */
export function over(value: string, backdrop: string, theme: string): string {
  if (value === "transparent" || value === "none") return backdrop;
  const painted = paint(value, theme);
  const overlay = RGBA.exec(painted);
  return overlay ? compose(overlay, backdrop) : painted;
}
