/**
 * @vitest-environment node
 */

/**
 * The shared style layer owns values a component may not re-decide by writing
 * a number where a token belongs; a number is wrong only next to the scale it
 * disagrees with.
 *
 * z-index: the scale in _ZIndex.sass says what sits over what, and a literal
 * silently outranks it; local stacking inside one component (-1, 0, 1, 2)
 * stays allowed. Width: the layout is one at every width, so no rule asks the
 * window how wide it is; a block that must reflow asks its own box (@container).
 * Global rules: a
 * partial that emits a rule is emitted again into every scoped stylesheet that
 * imports it. Assets: a file under assets/ that nothing references is weight in
 * every clone. One edge: a rule in an accent down a single side is the mark of
 * a quote and a [mod] block in post text, and of nothing else. Colour: a hex
 * outside assets/styles/ belongs to one theme, since the palettes are switched
 * by a class on <html>.
 *
 * The rest of the layer has a gate each: pageControls.spec.ts,
 * roundedCorners.spec.ts, pictures.spec.ts and textCut.spec.ts.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { basename, join } from "path";
import {
  CLIENT_SRC,
  asPath,
  collect,
  isStyle,
  styleFiles,
} from "@/shared/lib/testing/sourceTree";
import { GLOBAL_SHEETS, styleTextOf } from "@/shared/lib/testing/styles";

/** Values that order children INSIDE one stacking context, not on the scale. */
const LOCAL_STACKING = new Set([-1, 0, 1, 2]);

const Z_LITERAL = /^\s*z-index:\s*(-?\d+)\s*;?\s*$/gm;

/** A query of the window's width or of the kind of pointer: either one lays out a page apart. */
const MEDIA_WIDTH =
  /@media[^\n{]*((?:max-width|min-width|hover|pointer|any-hover|any-pointer)[^)]*)/g;

/** A colour written as itself: #abc, #aabbcc, and the alpha-carrying forms. */
const HEX_LITERAL = /#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/;

describe("the z-index scale", () => {
  it("is where a stacking order above the local one comes from", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const source = readFileSync(file, "utf8");
      for (const [, value] of source.matchAll(Z_LITERAL)) {
        if (LOCAL_STACKING.has(Number(value))) continue;
        offenders.push(
          `${asPath(file)}: z-index: ${value} is outside assets/styles/_ZIndex.sass, so nothing keeps it in the order the scale documents`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("the layout", () => {
  it("is one at every width: no rule asks the window how wide it is", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      const source = readFileSync(file, "utf8");
      for (const [, value] of source.matchAll(MEDIA_WIDTH)) {
        offenders.push(
          `${path}: @media at ${value} lays the page out apart for a width of the window or a kind of pointer; a block that must reflow asks its own box (@container)`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("a global rule", () => {
  it("reads the sheets main.ts emits once", () => {
    expect(GLOBAL_SHEETS.size).toBeGreaterThan(3);
  });

  it("is emitted once and not per importing stylesheet", () => {
    const offenders: string[] = [];
    for (const file of collect(join(CLIENT_SRC, "assets/styles"), isStyle)) {
      // Comments stripped first: a comment naming where a rule lives is not the
      // rule.
      const source = readFileSync(file, "utf8").replace(/^\s*\/\/.*$/gm, "");
      if (!source.includes("@keyframes")) continue;
      const path = asPath(file);
      if (GLOBAL_SHEETS.has(path)) continue;
      offenders.push(
        `${path}: declares @keyframes in a partial, so every scoped stylesheet that imports it ships its own copy`,
      );
    }
    expect(offenders).toEqual([]);
  });
});

describe("a colour outside the palettes", () => {
  it("is a token and not a literal", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      // assets/styles/ IS the palettes: ThemeVariables.css is where the hexes
      // of both themes are declared and calibrated against each other.
      if (path.startsWith("assets/styles/")) continue;
      for (const line of styleTextOf(file).split("\n")) {
        if (!HEX_LITERAL.test(line)) continue;
        offenders.push(
          `${path}: ${line.trim()} — a hex belongs to one theme, and the theme is switched by a class on <html>`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("an asset in the tree", () => {
  it("is reachable from the code", () => {
    const haystack = [
      ...collect(CLIENT_SRC, (name) =>
        [".ts", ".vue", ".sass", ".scss", ".css"].some((ext) =>
          name.endsWith(ext),
        ),
      ),
      join(CLIENT_SRC, "..", "index.html"),
    ]
      .map((file) => readFileSync(file, "utf8"))
      .join("\n");

    const assets = [
      ...collect(join(CLIENT_SRC, "assets/images"), () => true),
      ...collect(join(CLIENT_SRC, "assets/fonts"), () => true),
    ];
    expect(assets.length).toBeGreaterThan(5);

    const offenders = assets
      .filter((file) => !haystack.includes(basename(file)))
      .map((file) => `${asPath(file)}: nothing in the client names this file`);
    expect(offenders).toEqual([]);
  });
});

/** An accent as a value: the palette's own hues, and a saturated literal. */
const ACCENT_VALUE =
  /\$(?:accent-|border-accent-|link\b|highlight-overlay-)|var\(--(?:accent-|border-accent-|link\b)/;

/** A single side, written as a border or as an inset shadow along one axis. */
const ONE_EDGE =
  /border-(?:left|right|top|bottom|inline-start|inline-end|block-start|block-end)(?:-color)?:\s*([^\n;]+)|box-shadow:\s*inset\s+(-?[\d.]+px\s+0\s+[^\n;]+|0\s+-?[\d.]+px\s+[^\n;]+)/g;

/** A literal that carries a hue, as opposed to a grey or a white veil. */
const isColoured = (value: string): boolean => {
  if (ACCENT_VALUE.test(value)) return true;
  const hex = value.match(/#([0-9a-fA-F]{3,8})\b/);
  if (hex) {
    const full =
      hex[1].length <= 4
        ? hex[1]
            .slice(0, 3)
            .split("")
            .map((one) => one + one)
            .join("")
        : hex[1].slice(0, 6);
    const [r, g, b] = [0, 2, 4].map((at) =>
      parseInt(full.slice(at, at + 2), 16),
    );
    return !(r === g && g === b);
  }
  const rgb = value.match(/rgba?\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)/);
  if (rgb) return !(rgb[1] === rgb[2] && rgb[2] === rgb[3]);
  return false;
};

describe("a coloured edge down one side", () => {
  it("is the mark of a quote and of a [mod] block, and of nothing else", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      // The two blocks the mark belongs to are drawn here, and the guest sheet
      // draws the one horizontal rule of a footnote in the neutral border.
      if (path === "assets/styles/_BbcodeContent.sass") continue;
      const clean = styleTextOf(file);
      for (const found of clean.matchAll(ONE_EDGE)) {
        const value = (found[1] ?? found[2]).trim();
        if (!isColoured(value)) continue;
        offenders.push(
          `${path}: ${found[0].trim()} — the kind of a panel is its tint and its whole frame, not a rule down one side`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });
});

/** The declarations of a style file, comments out, with where each stands. */
const declarationsOf = (file: string) =>
  styleTextOf(file)
    .split("\n")
    .map((line, at) => ({ line: line.trim(), at: at + 1 }))
    .filter(({ line }) => /^[a-z$-][\w-]*\s*:/.test(line));

describe("the opacity scale", () => {
  // Nine dimmings once stood between 0.22 and 0.8, each picked by eye; the
  // scale in Variables.sass has three steps, and a dimming takes one by name.
  const SCALE = "assets/styles/Variables.sass";
  const ALLOWED: Record<string, string> = {
    "$disabled-hatch-opacity":
      "the share of the ink the stripes of a disabled control take, not a dimming",
  };

  it("is where every opacity comes from", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      for (const { line, at } of declarationsOf(file)) {
        const opacity = line.match(/^opacity:\s*([^;!]+)/);
        if (opacity && /^[\d.]+$/.test(opacity[1].trim())) {
          if (!/^[01]$/.test(opacity[1].trim()))
            offenders.push(`${path}:${at}: ${line}`);
          continue;
        }
        const token = line.match(/^(\$[\w-]*opacity[\w-]*):\s*([\d.]+)\s*$/);
        if (!token || path === SCALE || token[1] in ALLOWED) continue;
        offenders.push(`${path}:${at}: ${line}`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it("has three steps", () => {
    const steps = readFileSync(join(CLIENT_SRC, SCALE), "utf8").match(
      /^\$opacity-[\w-]+:\s*[\d.]+$/gm,
    );
    expect(steps).toHaveLength(3);
  });
});

describe("the scale of durations", () => {
  // A transition or an animation names its time by a token, and a curve of
  // its own is the reveal's alone (_Animations.sass); a literal 0.2s or 0.25s
  // next to the 0.15s and 0.3s of the scale is a tempo nobody chose.
  const SHEETS = [
    "assets/styles/Variables.sass",
    "assets/styles/_Animations.sass",
  ];
  const ALLOWED: Record<string, string> = {
    "assets/styles/Reset.sass":
      "the reduced-motion reset, which brings every time to nothing, and the one pulse that draws the eye to the target of a link",
  };
  const MOTION = /^(transition|animation)(-duration|-delay|-timing-function)?:/;

  it("is where a time and a curve come from", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const path = asPath(file);
      if (SHEETS.includes(path) || path in ALLOWED) continue;
      for (const { line, at } of declarationsOf(file)) {
        if (!MOTION.test(line)) continue;
        const literal = [...line.matchAll(/\b([\d.]+)(m?s)\b/g)].some(
          ([, value]) => Number(value) !== 0,
        );
        if (literal || /cubic-bezier/.test(line))
          offenders.push(`${path}:${at}: ${line}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("the weight of type", () => {
  // The face of the site ships 400 and 700. A 500 drew as 400 and a 600 as
  // 700 on PT Sans, and as something else in the system face a glyph fell to.
  it("is regular or bold and nothing between", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      for (const { line, at } of declarationsOf(file)) {
        const weight = line.match(/^font-weight:\s*([^;!\s]+)/);
        if (!weight) continue;
        if (/^(400|700|normal|bold|inherit)$/.test(weight[1])) continue;
        offenders.push(`${asPath(file)}:${at}: ${line}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});

describe("the break of a long word", () => {
  // One property, overflow-wrap: word-wrap is its old name, and word-break:
  // break-word is overflow-wrap: anywhere by CSS Text 3. Two of them on one
  // rule said the same thing twice, three times in places.
  it("is written as overflow-wrap", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      for (const { line, at } of declarationsOf(file)) {
        if (/^word-wrap:|^word-break:\s*break-word/.test(line))
          offenders.push(`${asPath(file)}:${at}: ${line}`);
      }
    }
    expect(offenders).toEqual([]);
  });
});
