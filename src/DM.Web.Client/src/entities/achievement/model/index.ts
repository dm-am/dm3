// Singleton catalog caches. Both are module-scoped on purpose: the catalogs are
// admin-edited reference data, so the profile and the moderation pages share
// one copy instead of refetching per view.
export { useAchievementCatalog } from "./useAchievementCatalog";
export { useContestSeries } from "./useContestSeries";
