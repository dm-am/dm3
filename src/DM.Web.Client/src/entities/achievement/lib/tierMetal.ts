import type { CSSProperties } from "vue";

/**
 * The four metals an award or an achievement is drawn in, and the only colours
 * a tile of either may carry besides neutral text and the page.
 *
 * A tile paints its icon and its tier badge in the metal of its tier; the
 * caption, the numbers and the progress bar under it stay neutral. Awards and
 * achievements share the four, so a gold trophy and a gold achievement are the
 * same gold, and the moderation catalogues draw each record in the metal the
 * profile will show it in.
 *
 * The values are token names, declared with one value in both theme blocks of
 * ThemeVariables.css: a metal does not change with the theme.
 */
export const TIER_METALS = {
  bronze: "--award-bronze",
  silver: "--award-silver",
  gold: "--award-gold",
  platinum: "--achievement-platinum",
} as const;

export type TierMetal = keyof typeof TIER_METALS;

/**
 * Metal of an award tier. 1, 2 and 3 are the places of a contest, gold down to
 * bronze; 5 is the place-less honour, in platinum so it never reads as a place.
 * 4 ("steel") and a record without a tier have no metal of their own and are
 * drawn in neutral text.
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

/** Metal of an achievement tier: I to IV climb bronze, silver, gold, platinum. */
export function achievementMetal(
  tier: number | null | undefined,
): TierMetal | null {
  switch (tier) {
    case 1:
      return "bronze";
    case 2:
      return "silver";
    case 3:
      return "gold";
    case 4:
      return "platinum";
    default:
      return null;
  }
}

/**
 * Hands a metal to the tile's stylesheet as `--tier-metal`, which the icon and
 * the badge read with a neutral fallback. No metal sets nothing, so the
 * fallback applies.
 */
export function metalStyle(metal: TierMetal | null): CSSProperties {
  return metal ? { "--tier-metal": `var(${TIER_METALS[metal]})` } : {};
}
