/**
 * @vitest-environment node
 */

/**
 * The award tiles and the achievement tiles are one design read twice, and the
 * moderation catalogues show the same records again.
 *
 * Every one of them is drawn in four metals — bronze, silver, gold, platinum —
 * and in neutral text on the page, and in nothing else. The icon and the tier
 * badge carry the metal of the tier; the caption, the numbers and the bar under
 * them are neutral. The rule broke once already, and not by a new colour: the
 * badge was refilled with one dark token so the numeral could take the metal,
 * and that token was a dark patch on every tile of the light theme. Before
 * that the numeral itself had been darkened, and read as black on the metal.
 * So the badge is filled with its metal under a light numeral, in both themes.
 *
 * The rule is held on the sources as an allowlist, not as a list of the
 * colours that went wrong: a colour-bearing declaration of a tile may name a
 * metal, the tile's `--tier-metal` (which entities/achievement/lib/tierMetal
 * fills with a metal and nothing else), the badge ink, or a neutral of the
 * palette. A fifth colour added any other way fails here on the day it
 * appears.
 *
 * An allowlist only reads what is written, and the dark patch comes back just
 * as well through what is not. An icon that loses its own colour does not go
 * blank: it inherits the tile's text, #333 in the light theme. A metal with a
 * dark fallback paints every tier without a metal in that same #333. A ring in
 * the metal on one badge names nothing but the metal, and still draws that
 * badge unlike its pair. So the icons, the badges and the fallback are asked
 * for by what they must be, not by what they may name.
 *
 * Stylesheets rather than a rendered profile: these declarations are scoped
 * style blocks, the badge needs a loaded profile with an earned tier to appear
 * at all, and the popover ones need a hovered tooltip on top of that. What a
 * rule resolves to is read off the compiled stylesheet (shared/lib/testing/
 * styles), where a nested `&__` block has its full class name and a sass
 * variable its value; the allowlist reads the sources, because what it asks is
 * which sass name a declaration spells, and compiling erases the name.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import {
  compileSass,
  componentCss,
  ruleMap,
  styleRules,
  type Declarations,
  type StyleRule,
} from "@/shared/lib/testing/styles";
import {
  TIER_METALS,
  achievementMetal,
  awardMetal,
  metalStyle,
} from "./entities/achievement/lib/tierMetal";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const sourceOf = (file: string): string =>
  readFileSync(join(CLIENT_SRC, file), "utf8");

const THEME_VARIABLES = "assets/styles/ThemeVariables.css";
const AWARDS = "pages/profile/ProfileAwardsSection.vue";
const ACHIEVEMENTS = "pages/profile/ProfileAchievementsSection.vue";
const AWARD_TYPES = "pages/moderation/ModerationAwardTypes.vue";
const AWARD_SERIES = "pages/moderation/ModerationAwardsSeries.vue";
const ACHIEVEMENT_CATALOGUE = "pages/moderation/ModerationAchievements.vue";

/** The profile sections are tiles from top to bottom, so all of them is read. */
const WHOLE_FILES = [AWARDS, ACHIEVEMENTS];

/** A moderation page is a form around the icons; only the icon blocks are read. */
const ICON_BLOCKS: Array<[string, string]> = [
  [AWARD_TYPES, "type-icon"],
  [AWARD_SERIES, "grant-icon"],
  [ACHIEVEMENT_CATALOGUE, "cat-icon"],
];

/** Every file that draws a tile or a catalogue icon of one. */
const TILE_FILES = [...WHOLE_FILES, ...ICON_BLOCKS.map(([file]) => file)];

/** The icons a metal is handed to: the two profile tiles, and the two
 * catalogues that bind metalStyle (see "is handed its metal" below). */
const METAL_ICONS: Array<[string, string]> = [
  [AWARDS, "award-icon"],
  [ACHIEVEMENTS, "chain-icon"],
  [AWARD_TYPES, "type-icon"],
  [AWARD_SERIES, "grant-icon"],
];

/** A locked chain has earned no tier, so it has no metal to show, and is
 * muted on purpose; every other state of an icon wears its metal. */
const LOCKED = /--locked(?![\w-])/;

/** The badges that carry a tier, by their compiled class: the numeral on an
 * achievement, the contest label on an award, and the numeral of each row in
 * the achievement popover. */
const BADGES: Array<[string, string]> = [
  [ACHIEVEMENTS, "chain-degree"],
  [AWARDS, "award-series"],
  [ACHIEVEMENTS, "chain-popover__tier-degree"],
];

/** What paints a badge: its ink, the halo under the ink, and its fill. */
const BADGE_PAINT = ["color", "text-shadow", "background", "background-color"];

/** A ring around a badge, in either of the ways one is drawn. */
const RING = /^(outline(-[\w-]+)?|box-shadow)$/;

const compiled = new Map<string, StyleRule[]>();

/** A file's stylesheet, compiled the way the browser gets it, rule by rule. */
function rulesOf(file: string): StyleRule[] {
  let rules = compiled.get(file);
  if (!rules) {
    rules = styleRules(componentCss(join(CLIENT_SRC, file)));
    compiled.set(file, rules);
  }
  return rules;
}

/** Whether a selector, or one of its comma-separated parts, lands on an
 * element of this class: the class stands in the last compound, not in an
 * ancestor, and not as the start of a longer class name. */
function landsOn(selector: string, className: string): boolean {
  const own = new RegExp(`\\.${className}(?![\\w-])`);
  return selector
    .split(",")
    .some((part) => own.test(/[^\s>+~]+$/.exec(part.trim())?.[0] ?? ""));
}

/** The rules of a file that land on an element of this class. */
function rulesAt(file: string, className: string): StyleRule[] {
  return rulesOf(file).filter(({ selector }) => landsOn(selector, className));
}

/** Everything declared at an element of this class, later rules winning. */
function declaredAt(file: string, className: string): Declarations {
  return Object.assign(
    {},
    ...rulesAt(file, className).map(({ declarations }) => declarations),
  ) as Declarations;
}

/**
 * The fallback of every `var(--tier-metal ...)` in a list of rules, brackets
 * counted so a fallback that is itself a var() is read whole; null where the
 * metal has no fallback at all.
 */
function metalFallbacks(rules: StyleRule[]): Array<[string, string | null]> {
  const found: Array<[string, string | null]> = [];
  for (const { selector, declarations } of rules) {
    for (const [property, value] of Object.entries(declarations)) {
      for (const { index } of value.matchAll(/var\(--tier-metal(?![\w-])/g)) {
        let depth = 0;
        let end = index + "var".length;
        for (; end < value.length; end += 1) {
          if (value[end] === "(") depth += 1;
          else if (value[end] === ")") {
            depth -= 1;
            if (depth === 0) break;
          }
        }
        const rest = value.slice(index + "var(--tier-metal".length, end).trim();
        found.push([
          `${selector} { ${property}: ${value} }`,
          rest.startsWith(",") ? rest.slice(1).trim() : null,
        ]);
      }
    }
  }
  return found;
}

/** Tokens a tile may name: the metals, the tile's metal, the badge ink. */
const ALLOWED_TOKENS = new Set<string>([
  ...Object.values(TIER_METALS),
  "--tier-metal",
  "--tier-badge-text",
  "--tier-badge-halo",
]);

/** The neutrals of the palette: text, the page, the tooltip surface the
 * popovers sit on, and the site-wide progress scale. */
const NEUTRAL_SASS = new Set([
  "$text",
  "$text-muted",
  "$bg-page",
  "$tooltip-text",
  "$tooltip-link",
  "$tooltip-link-hover",
  "$progress-bg-overlay",
  "$progress-fill-overlay",
]);

/** A white wash over the tooltip, which is dark in both themes. */
const WHITE_WASH = /^rgba\(255,\s*255,\s*255,\s*0?\.\d+\)$/;

/** Words a colour-bearing value may hold besides colours. */
const PLAIN_WORDS = new Set([
  "solid",
  "dashed",
  "dotted",
  "none",
  "transparent",
  "inherit",
  "currentColor",
  "px",
  "em",
]);

const COLOUR_PROPERTY =
  /^(color|fill|stroke|background|background-color|border(-top|-right|-bottom|-left)?(-color)?|outline(-color)?|text-shadow|box-shadow|text-decoration-color|caret-color):\s*(.+)$/;

/** Style lines of a file: the <style> blocks, trimmed, comments dropped. */
function styleLinesOf(source: string): string[] {
  return [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)]
    .map((found) => found[1])
    .join("\n")
    .split("\n")
    .map((line) => line.replace(/\/\/.*$/, ""));
}

/** The lines of one block, found by its selector line, down to its dedent. */
function blockOf(source: string, selector: string): string[] {
  const lines = styleLinesOf(source);
  const start = lines.findIndex((line) => line.trim() === selector);
  if (start < 0) return [];
  const indent = lines[start].length - lines[start].trimStart().length;
  const block: string[] = [];
  for (const raw of lines.slice(start + 1)) {
    if (!raw.trim()) continue;
    if (raw.length - raw.trimStart().length <= indent) break;
    block.push(raw);
  }
  return block;
}

/** Every colour a declaration names that is neither a metal nor a neutral. */
function strangersIn(value: string): string[] {
  const strangers: string[] = [];
  let rest = value;

  rest = rest.replace(/var\((--[\w-]+)/g, (_, token: string) => {
    if (!ALLOWED_TOKENS.has(token)) strangers.push(`var(${token})`);
    return " ";
  });
  rest = rest.replace(/\$[\w-]+/g, (name) => {
    if (!NEUTRAL_SASS.has(name)) strangers.push(name);
    return " ";
  });
  rest = rest.replace(/(rgba?|hsla?)\([^)]*\)/g, (literal) => {
    if (!WHITE_WASH.test(literal)) strangers.push(literal);
    return " ";
  });
  rest = rest.replace(/#[0-9a-fA-F]{3,8}\b/g, (literal) => {
    strangers.push(literal);
    return " ";
  });
  for (const word of rest.match(/[a-zA-Z][a-zA-Z-]*/g) ?? []) {
    if (!PLAIN_WORDS.has(word)) strangers.push(word);
  }
  return strangers;
}

function offendersIn(file: string, lines: string[]): string[] {
  const offenders: string[] = [];
  for (const line of lines) {
    const declared = COLOUR_PROPERTY.exec(line.trim());
    if (!declared) continue;
    for (const stranger of strangersIn(declared[5])) {
      offenders.push(`${file}: ${line.trim()} — ${stranger}`);
    }
  }
  return offenders;
}

/** A token's value in one theme block of ThemeVariables.css. */
function tokenIn(theme: "Light" | "Dark", token: string): string | null {
  const block = new RegExp(
    `html\\.theme_${theme}\\s*\\{([\\s\\S]*?)\\n\\}`,
  ).exec(sourceOf(THEME_VARIABLES))?.[1];
  if (!block) return null;
  return (
    new RegExp(`^\\s*${token}:\\s*([^;]+);`, "m").exec(block)?.[1].trim() ??
    null
  );
}

function luminance(hex: string): number {
  const digits = hex.slice(1);
  const full =
    digits.length === 3 ? digits.replace(/./g, (one) => one + one) : digits;
  const [r, g, b] = [0, 2, 4]
    .map((at) => parseInt(full.slice(at, at + 2), 16) / 255)
    .map((c) =>
      c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4),
    );
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

describe("the four metals of awards and achievements", () => {
  it("are four tokens, declared alike in both themes", () => {
    expect(Object.keys(TIER_METALS)).toEqual([
      "bronze",
      "silver",
      "gold",
      "platinum",
    ]);
    for (const token of Object.values(TIER_METALS)) {
      const light = tokenIn("Light", token);
      expect(light, `${token} is not declared in the light theme`).toMatch(
        /^#[0-9a-fA-F]{6}$/,
      );
      expect(tokenIn("Dark", token), `${token} differs by theme`).toBe(light);
    }
  });

  it("climb bronze to platinum over the tiers of an achievement", () => {
    expect([1, 2, 3, 4].map(achievementMetal)).toEqual([
      "bronze",
      "silver",
      "gold",
      "platinum",
    ]);
    expect(achievementMetal(null)).toBeNull();
  });

  it("give an award its place, and the place-less honour platinum", () => {
    expect([1, 2, 3, 5].map(awardMetal)).toEqual([
      "gold",
      "silver",
      "bronze",
      "platinum",
    ]);
    expect(awardMetal(4)).toBeNull();
    expect(awardMetal(null)).toBeNull();
  });

  it("reach a tile only as a metal token", () => {
    expect(metalStyle("gold")).toEqual({ "--tier-metal": "var(--award-gold)" });
    expect(metalStyle(null)).toEqual({});
  });
});

describe("a tile of an award or an achievement", () => {
  it("names no colour but a metal and the neutrals", () => {
    const offenders = [
      ...WHOLE_FILES.flatMap((file) =>
        offendersIn(file, styleLinesOf(sourceOf(file))),
      ),
      ...ICON_BLOCKS.flatMap(([file, selector]) => {
        const block = blockOf(sourceOf(file), `.${selector}`);
        expect(block, `${file} has no .${selector} block`).not.toEqual([]);
        return offendersIn(file, block);
      }),
    ];

    expect(offenders).toEqual([]);
  });

  it.each(METAL_ICONS)("%s colours .%s with its metal", (file, className) => {
    // Asked for by name, because the defect is a declaration that is not
    // there: without it the icon inherits the tile's $text, and every tile,
    // with a metal or without, shows a dark glyph on the light page.
    const colours = rulesAt(file, className)
      .filter(({ selector }) => !LOCKED.test(selector))
      .flatMap(({ selector, declarations }) =>
        declarations.color === undefined
          ? []
          : [[selector, declarations.color] as const],
      );

    expect(
      colours,
      `${file} gives .${className} no colour of its own`,
    ).not.toEqual([]);
    for (const [selector, colour] of colours) {
      expect(colour, `${file}: ${selector}`).toMatch(/^var\(--tier-metal,/);
    }
  });

  it("falls back to the muted neutral where a tier has no metal", () => {
    // Steel, a record without a tier and an achievement not yet earned set no
    // --tier-metal, and show the fallback. Anything but the muted neutral is a
    // tile unlike its row: $text is the #333 of the light theme, and no
    // fallback at all leaves an icon inheriting it and a badge unfilled under
    // its white numeral.
    const neutral = ruleMap(compileSass(".probe\n  color: $text-muted")).get(
      ".probe",
    )?.color;
    expect(neutral, "$text-muted compiles to no colour").toBeDefined();

    const fallbacks = TILE_FILES.flatMap((file) =>
      metalFallbacks(rulesOf(file)).map(
        ([where, fallback]) => [`${file}: ${where}`, fallback] as const,
      ),
    );
    // A parse that found no metal would pass the check below by skipping it.
    expect(fallbacks.length).toBeGreaterThan(0);

    const strays = fallbacks
      .filter(([, fallback]) => fallback !== neutral)
      .map(([where, fallback]) => `${where}: falls back to ${fallback}`);
    expect(strays).toEqual([]);
  });

  it("is handed its metal by the one mapping", () => {
    // A tier-to-class table of its own is how a file ends up with a fifth
    // colour: each of these files had one, and no two of them agreed.
    const binds: Array<[string, string]> = [
      [AWARDS, "metalStyle(awardMetal("],
      [ACHIEVEMENTS, "metalStyle(achievementMetal("],
      [AWARD_TYPES, "metalStyle(awardMetal("],
      [AWARD_SERIES, "metalStyle(awardMetal("],
    ];
    for (const [file, call] of binds) {
      expect(
        sourceOf(file),
        `${file} draws its icons without the metal`,
      ).toContain(call);
    }
  });
});

describe("a tier badge", () => {
  /** The paint of a badge: the BADGE_PAINT declarations that land on it. */
  const paintOf = (file: string, className: string): Declarations => {
    const declared = declaredAt(file, className);
    return Object.fromEntries(
      BADGE_PAINT.filter((property) => property in declared).map((property) => [
        property,
        declared[property],
      ]),
    );
  };

  it.each(BADGES)(
    "%s .%s is filled with its metal under the light ink",
    (file, className) => {
      const paint = paintOf(file, className);

      expect(
        rulesAt(file, className),
        `${file} has no .${className} rule`,
      ).not.toEqual([]);
      expect(paint.color, `${file} .${className} ink`).toBe(
        "var(--tier-badge-text)",
      );
      expect(paint["background-color"], `${file} .${className} fill`).toMatch(
        /^var\(--tier-metal,/,
      );
    },
  );

  it("is painted alike wherever it stands", () => {
    // One badge read three times. Painted its own way in one place, it stops
    // reading as the same mark, and the award and achievement badges have
    // come apart like that before.
    const [first, ...others] = BADGES.map(([file, className]) => ({
      where: `${file} .${className}`,
      paint: paintOf(file, className),
    }));
    for (const other of others) {
      expect(
        other.paint,
        `${other.where} is painted unlike ${first.where}`,
      ).toEqual(first.paint);
    }
  });

  it.each(BADGES)("%s .%s wears no ring", (file, className) => {
    // A tile badge is lifted off the icon it overlaps by one border in the
    // page colour. A hairline in the metal on top of it drew outside that
    // border, so the badge read as a pill with a white gap and a stray ring,
    // and it was drawn on one of the two badges only.
    const rings = rulesAt(file, className).flatMap(
      ({ selector, declarations }) =>
        Object.entries(declarations)
          .filter(([property]) => RING.test(property))
          .map(([property, value]) => `${selector} { ${property}: ${value} }`),
    );

    expect(rings, `${file} .${className}`).toEqual([]);
  });

  it("carries a light numeral in both themes", () => {
    // A dark numeral on the metal read as black; the badge ink stays light
    // whatever the page under the tile is.
    for (const theme of ["Light", "Dark"] as const) {
      const ink = tokenIn(theme, "--tier-badge-text");
      expect(
        ink,
        `--tier-badge-text is not a hex in the ${theme} theme`,
      ).toMatch(/^#[0-9a-fA-F]{3,6}$/);
      expect(luminance(ink!)).toBeGreaterThan(0.8);
    }
  });
});

/** The caption under a tile icon, by the class that draws it. */
const CAPTIONS: Array<[string, string]> = [
  [AWARDS, "award-title"],
  [ACHIEVEMENTS, "chain-title"],
];

/** `font-weight` of a block, read as the numeric weight it resolves to. */
function captionWeight(source: string, selector: string): number | null {
  for (const line of blockOf(source, `.${selector}`)) {
    const declared = /^font-weight:\s*(\S+)$/.exec(line.trim());
    if (!declared) continue;
    return declared[1] === "bold" ? 700 : Number(declared[1]);
  }
  return null;
}

describe("captions of a profile tile", () => {
  // They were the one place on the profile where the name of an entity was set
  // lighter than 600, which is why they read as captions of the pictures above
  // them rather than as names.
  it.each(CAPTIONS)("sets %s .%s at the weight of a name", (file, selector) => {
    const weight = captionWeight(sourceOf(file), selector);

    expect(weight, `.${selector} declares no font-weight`).not.toBeNull();
    expect(weight).toBeGreaterThanOrEqual(600);
  });
});
