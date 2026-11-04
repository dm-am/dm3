// Copies of server bounds the browser cannot read; ClientFieldLimitsShould compares the numbers.

/** CatalogFieldLimits: the text fields of an award type, an achievement category and a tier. */
export const CATALOG_CODE_MAX_LENGTH = 80;
export const CATALOG_TITLE_MAX_LENGTH = 200;
export const CATALOG_DESCRIPTION_MAX_LENGTH = 1000;
export const CATALOG_ICON_NAME_MAX_LENGTH = 80;

/** TierScale.MinTier and MaxTier: the one scale of awards and achievements. */
export const MIN_TIER = 1;
export const MAX_TIER = 5;

/** The scale in words, under the tier field of the moderation windows. */
export const TIER_SCALE_HINT = "1 золото, 2 серебро, 3 бронза, 5 платина";

/** ContestSeries.MinYear and YearsAhead: from the opening of the site to a year ahead. */
export const CONTEST_SERIES_MIN_YEAR = 2007;
export const CONTEST_SERIES_YEARS_AHEAD = 1;
