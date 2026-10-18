/**
 * @vitest-environment jsdom
 */

/**
 * The footer at every width (owner, 5 October 2026, on the bottom of a topic on
 * a phone: "с хуя футер поломан визуально?!").
 *
 * What was broken: below the shell breakpoint the two columns stacked and the
 * footer grew to 189px, while the paper strip stayed 150px high, so the last
 * legal link stood on the bare page under a hard edge across the footer. The
 * rule kept here:
 *
 * 1. The strip covers the footer from its torn edge to the bottom at any
 *    height the footer takes, and the torn edge is as tall as on a desktop.
 * 2. The credits and the legal links stand side by side at every width, as on
 *    the desktop: a phone lays the page out as wide as the narrowest layout
 *    of the shell (index.html) and has no footer of its own.
 * 3. No line of the footer wraps at any of these widths.
 *
 * Nothing in jsdom lays out a page, so the rules are read out of the compiled
 * stylesheet, the cascade applied by order (every rule here weighs one class),
 * and the lines are measured in the face the site ships, calibrated against
 * Chrome.
 */
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { loadFace } from "@/shared/lib/testing/fontFace";
import {
  componentCss,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";
import Footer from "./Footer.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = resolve(HERE, "../..");

function px(file: string, pattern: RegExp, what: string): number {
  const found = readFileSync(resolve(SRC, file), "utf8").match(pattern);
  if (!found) throw new Error(`${file}: no ${what}`);
  return Number(found[1]);
}

const BP_SHELL = px(
  "assets/styles/_Breakpoints.sass",
  /\$bp-shell:\s*(\d+)px/,
  "shell breakpoint",
);

/**
 * Chrome's classic scrollbar on Windows, which .main always draws
 * (overflow-y: scroll); the same figure widgets/header/menuSteps.spec.ts
 * measured. Overlay scrollbars take nothing, so a line that fits beside a
 * drawn one fits everywhere.
 */
const SCROLLBAR = 15;
/** Chrome lays text out in 1/64 px; a line is judged with a pixel to spare. */
const SLACK = 1;
/** The narrowest layout of the shell, the one a phone draws smaller. */
const NARROWEST = px(
  "app/App.vue",
  /\$narrowest-layout:\s*(\d+)px/,
  "narrowest layout",
);

// ───────────────────────────────────────────────────────── the stylesheet ──

interface Rule {
  /** The widest window the rule applies in: its @media (max-width) or none. */
  upTo: number;
  selectors: string[];
  declarations: Declarations;
}

const SIDES = ["top", "right", "bottom", "left"] as const;

/** A padding shorthand opened into its four sides. */
function sides(value: string): string[] {
  const [t, r = t, b = t, l = r] = value.split(/\s+/);
  return [t, r, b, l];
}

/** The compiled sheet in source order, each @media (max-width) opened. */
function rulesOf(css: string): Rule[] {
  const out: Rule[] = [];
  const block =
    /@media \(max-width: (\d+)px\)\s*\{((?:[^{}]*\{[^{}]*\})*)\s*\}|([^{}@]+\{[^{}]*\})/g;
  for (const [, width, media, plain] of css.matchAll(block)) {
    const upTo = width ? Number(width) : Infinity;
    for (const { selector, declarations } of styleRules(media ?? plain)) {
      out.push({
        upTo,
        selectors: selector.split(",").map((s) => s.trim()),
        declarations,
      });
    }
  }
  return out;
}

const CSS = componentCss(resolve(HERE, "Footer.vue"));
const RULES = rulesOf(CSS);

/**
 * What one selector gets at a window width: every rule that names it and
 * applies there, later over earlier, a padding shorthand opened into sides so
 * that a later side overrides it and it overrides an earlier side.
 */
function at(window: number, selector: string): Declarations {
  const out: Declarations = {};
  for (const rule of RULES) {
    if (rule.upTo < window || !rule.selectors.includes(selector)) continue;
    for (const [property, value] of Object.entries(rule.declarations)) {
      if (property === "padding") {
        sides(value).forEach((v, i) => (out[`padding-${SIDES[i]}`] = v));
      } else {
        out[property] = value;
      }
    }
  }
  return out;
}

const length = (value: string | undefined) => parseFloat(value ?? "0") || 0;

// ──────────────────────────────────────────────────────────── the lines ──

const face = loadFace(resolve(SRC, "assets/fonts/PTSans-Regular.ttf"));
const FONT_SIZE = length(at(Infinity, ".credits")["font-size"]);

/**
 * The badge "18+" at the end of the copyright line: its box, drawn outside
 * its width (the page keeps content-box), and its gap.
 */
const AGE = at(Infinity, ".age");
const BADGE =
  length(AGE.width) + 2 * length(AGE.border) + length(AGE["margin-left"]);

/** Each column's lines as the reader sees them, measured in the face. */
function columns(): { credits: number[]; legal: number[] } {
  const footer = mount(Footer, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  const widths = (selector: string) =>
    footer.findAll(`${selector} > div`).map((line) => {
      const badge = line.find(".age");
      let text = line.text();
      if (badge.exists()) text = text.replace(badge.text(), "");
      const words = text.replace(/\s+/g, " ").trim();
      return (
        face.width(badge.exists() ? `${words} ` : words, FONT_SIZE, 0) +
        (badge.exists() ? BADGE : 0)
      );
    });
  const found = { credits: widths(".credits"), legal: widths(".legal") };
  footer.unmount();
  return found;
}

// ─────────────────────────────────────────────────────────────── the specs ──

/** Widths on both sides of every breakpoint the footer answers to. */
const WINDOWS = [1366, BP_SHELL + 1, BP_SHELL, 768, NARROWEST];

/** The height of the footer on a desktop, the one its picture is drawn at. */
const FOOTER_HEIGHT = px(
  "assets/styles/Layout.sass",
  /\$footer-height:\s*(\d+)px/,
  "footer height",
);

/**
 * The owner on a phone, 12 October 2026: the background of the footer is
 * broken. The strip was drawn at the footer's own height, so a footer taller
 * than its picture drew the torn edge larger than on a desktop. It is two
 * layers of one picture now: the torn edge at its desktop height, and under it
 * the same picture as tall as the footer, of which only the flat paper below
 * the first layer shows.
 */
describe("the paper strip of the footer", () => {
  it("draws the torn edge at its desktop height at every width", () => {
    expect(FOOTER_HEIGHT).toBeGreaterThan(0);
    for (const window of WINDOWS) {
      const strip = at(window, ".footer:before");
      expect(strip, `${window}px`).toMatchObject({
        position: "absolute",
        top: "0",
        bottom: "0",
        "background-position": "left top",
        "background-repeat": "repeat-x",
        // The edge first and at a fixed height; the paper under it as tall as
        // the footer, so a footer taller than the picture has no bare page
        // under its last lines.
        "background-size": `auto ${FOOTER_HEIGHT}px, auto 100%`,
      });
      const layers = (strip["background-image"] ?? "").split(/,\s*(?=url\()/);
      expect(layers, `${window}px`).toHaveLength(2);
      expect(layers[0], `${window}px`).toMatch(/footer-decoration\.png/);
      expect(layers[1], `${window}px: one picture`).toBe(layers[0]);
      expect(strip.height, `${window}px`).toBeUndefined();
      expect(at(window, ".footer").overflow, `${window}px`).toBe("hidden");
    }
  });
});

describe("the columns of the footer", () => {
  it("stand side by side at every width", () => {
    for (const window of WINDOWS) {
      expect(at(window, ".footer")["flex-direction"], `${window}px`).not.toBe(
        "column",
      );
    }
  });

  it("keep every line on one line at every width", () => {
    const { credits, legal } = columns();
    expect(credits).toHaveLength(5);
    expect(legal).toHaveLength(2);
    const widest = (lines: number[]) => Math.max(...lines);

    for (const window of WINDOWS) {
      const room = window - SCROLLBAR;
      const footer = at(window, ".footer");
      const side = (column: string) => {
        const own = at(window, column);
        return {
          width: own.width,
          left: length(own["padding-left"]),
          right: length(own["padding-right"]),
        };
      };
      const c = side(".credits");
      const l = side(".legal");

      if (c.width === "auto" && l.width === "auto") {
        // Each as wide as its text, never closer than the gap.
        const need =
          length(footer["padding-left"]) +
          c.left +
          widest(credits) +
          length(footer["column-gap"]) +
          widest(legal) +
          l.right +
          length(footer["padding-right"]);
        expect(need + SLACK, `${window}px, two columns`).toBeLessThanOrEqual(
          room,
        );
      } else {
        // Fixed columns under the sidebars.
        expect(
          widest(credits) + c.left + c.right + SLACK,
          `${window}px, credits`,
        ).toBeLessThanOrEqual(length(c.width));
        expect(
          widest(legal) + l.left + l.right + SLACK,
          `${window}px, legal`,
        ).toBeLessThanOrEqual(length(l.width));
      }
    }
  });

  it("measures the lines the way Chrome does", () => {
    // Widths of the two longest lines, in the shipped face at 14px, measured
    // in headless Chrome on 6 October 2026 (a Range over each line).
    const { credits, legal } = columns();
    expect(Math.max(...credits)).toBeCloseTo(269.92, 0);
    expect(Math.max(...legal)).toBeCloseTo(193.11, 0);
    // The copyright line, the badge included.
    expect(credits[0]).toBeCloseTo(215.13, 0);
  });

  it("reads the copyright and the credits before the legal links", () => {
    const footer = mount(Footer, {
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    const parts = footer.findAll(".footer > *").map((el) => el.classes()[0]);
    expect(parts).toEqual(["credits", "legal"]);
    expect(footer.get(".credits > div").text()).toMatch(/^© Dungeon Master/);
    footer.unmount();
  });
});
