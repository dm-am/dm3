/**
 * @vitest-environment node
 */

/**
 * The award tiles and the achievement tiles are one design read twice, and two
 * decisions of the owner live in them.
 *
 * The first is the tier badge. A white numeral on gold gave 1.78 against a
 * threshold of 4.5, so the numeral was darkened — and the badge turned into a
 * black glyph on a metal pill, which is what the owner saw and read as broken.
 * The pair is symmetric, so the answer costs no contrast at all: the metal goes
 * on the glyph and the one dark token becomes the fill. The rule is therefore
 * not "these four blocks look like this" but "a tier badge never fills with the
 * metal", and a fifth badge added the old way fails here on the day it appears.
 *
 * The second is the weight of the tile captions. They were the one place on the
 * profile where the name of an entity was set lighter than 600, which is why
 * they read as captions of the pictures above them rather than as names.
 *
 * Sources rather than a rendered profile: these declarations are scoped style
 * blocks, the badge needs a loaded profile with an earned tier to appear at
 * all, and the popover ones need a hovered tooltip on top of that.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const sourceOf = (file: string): string =>
  readFileSync(join(CLIENT_SRC, file), "utf8");

const AWARDS = "pages/profile/ProfileAwardsSection.vue";
const ACHIEVEMENTS = "pages/profile/ProfileAchievementsSection.vue";

/** The token that fills a tier badge. Its name says fill, and so must its use. */
const FILL = "--tier-badge-bg";
/** The token it replaced, back when the badge was filled with the metal. */
const RETIRED_FILL = "--tier-badge-text";

/** Every declaration line of a file, trimmed, comments dropped. */
const declarationsOf = (source: string): string[] =>
  source
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith("//"));

describe("tier badges of a profile tile", () => {
  const files = [AWARDS, ACHIEVEMENTS];

  it("fills with the dark token and never with a metal", () => {
    const offenders: string[] = [];

    for (const file of files) {
      for (const line of declarationsOf(sourceOf(file))) {
        if (!line.startsWith("background-color:")) continue;
        // A metal on a background is the inverted badge coming back: the tier
        // colour belongs on the glyph, where the contrast is the same and the
        // text is not black.
        if (/--(award-|achievement-|card-tier-color)/.test(line)) {
          offenders.push(`${file}: ${line}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("spells the fill by the token whose name means fill", () => {
    const offenders = files
      .filter((file) => sourceOf(file).includes(RETIRED_FILL))
      .map((file) => `${file}: ${RETIRED_FILL}`);

    expect(offenders).toEqual([]);
    for (const file of files) {
      expect(sourceOf(file), `${file} draws no tier badge`).toContain(FILL);
    }
  });

  it("puts no halo under the glyph of a badge", () => {
    // The halo outlined a white numeral. Under a metal glyph on a dark fill it
    // only smears it, and one of the two sections carried the stale dark one
    // for as long as the numeral had already been dark.
    const offenders = files
      .flatMap((file) =>
        declarationsOf(sourceOf(file)).map((line) => ({ file, line })),
      )
      .filter(({ line }) => line.startsWith("text-shadow:"))
      .map(({ file, line }) => `${file}: ${line}`);

    expect(offenders).toEqual([]);
  });
});

/** The caption under a tile icon, by the class that draws it. */
const CAPTIONS: Array<[string, string]> = [
  [AWARDS, "award-title"],
  [ACHIEVEMENTS, "chain-title"],
];

/** `font-weight` of a block, read as the numeric weight it resolves to. */
function captionWeight(source: string, selector: string): number | null {
  const lines = source.split("\n");
  const start = lines.findIndex((line) => line.trim() === `.${selector}`);
  if (start < 0) return null;
  const indent = lines[start].length - lines[start].trimStart().length;

  for (const raw of lines.slice(start + 1)) {
    const text = raw.trim();
    if (!text || text.startsWith("//")) continue;
    if (raw.length - raw.trimStart().length <= indent) break;
    const declared = /^font-weight:\s*(\S+)$/.exec(text);
    if (!declared) continue;
    return declared[1] === "bold" ? 700 : Number(declared[1]);
  }
  return null;
}

describe("captions of a profile tile", () => {
  it.each(CAPTIONS)("sets %s .%s at the weight of a name", (file, selector) => {
    const weight = captionWeight(sourceOf(file), selector);

    expect(weight, `.${selector} declares no font-weight`).not.toBeNull();
    expect(weight).toBeGreaterThanOrEqual(600);
  });
});
