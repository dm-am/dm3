import type { CSSProperties } from "vue";

/** The four metals of awards and achievements alike, as tokens that keep one value in both themes. */
export const TIER_METALS = {
  bronze: "--award-bronze",
  silver: "--award-silver",
  gold: "--award-gold",
  platinum: "--achievement-platinum",
} as const;

export type TierMetal = keyof typeof TIER_METALS;

/**
 * The metal of a tier, on the one scale of awards and achievements (TierScale on the
 * server): 1 to 3 are the places of a contest, 5 is platinum, the honour without a
 * place and the top of an achievement chain. 4, steel, is drawn without a metal.
 */
export function awardMetal(tier: number | null | undefined): TierMetal | null {
  switch (tier) {
    case 1:
      return "gold";
    case 2:
      return "silver";
    case 3:
      return "bronze";
    case 5:
      return "platinum";
    default:
      return null;
  }
}

/** The metal as `--tier-metal`; without one the tile's neutral fallback applies. */
export function metalStyle(metal: TierMetal | null): CSSProperties {
  return metal ? { "--tier-metal": `var(${TIER_METALS[metal]})` } : {};
}

/** Every tier of the scale in a word, from tier 1 up, as the moderation tables print it. */
const TIER_WORDS: readonly string[] = [
  "Золото",
  "Серебро",
  "Бронза",
  "Сталь",
  "Платина",
];

/** The tier in a word; null for no tier or one off the scale. */
export function tierWord(tier: number | null | undefined): string | null {
  return tier == null ? null : (TIER_WORDS[tier - 1] ?? null);
}

/** The metals an achievement chain climbs by threshold: bronze, silver, gold, platinum. */
const CHAIN_CLIMB = [3, 2, 1, 5] as const;

/**
 * The tier a new top of a chain takes: the metal after the highest one the chain
 * has, bronze for an empty chain, and none past platinum. Steel is off the climb.
 */
export function nextChainTier(
  tiers: readonly (number | null | undefined)[],
): number | null {
  const top = Math.max(
    -1,
    ...tiers.map((t) => CHAIN_CLIMB.indexOf(t as (typeof CHAIN_CLIMB)[number])),
  );
  return CHAIN_CLIMB[top + 1] ?? null;
}
