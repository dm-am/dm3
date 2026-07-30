import { ref, type Ref } from "vue";
import { achievementApi } from "../api";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";

/**
 * Singleton catalog cache: loaded once and reused
 * between the profile and admin pages. This is the admin catalog of 13 categories
 * + 52 tiers — the data is stable, cached at module scope.
 *
 * Returns reactive refs. `load()` is idempotent: a repeated call
 * during loading or after a successful load does nothing.
 * `reload()` forces a fresh fetch (for admin edits).
 */
const categories: Ref<AchievementCategory[] | null> = ref(null);
const types: Ref<AchievementType[] | null> = ref(null);
const loading = ref(false);
let pending: Promise<void> | null = null;

export function useAchievementCatalog() {
  async function load(): Promise<void> {
    if (categories.value !== null && types.value !== null) return;
    if (pending !== null) return pending;
    pending = (async () => {
      loading.value = true;
      try {
        const [c, t] = await Promise.all([
          achievementApi.getAchievementCategories(),
          achievementApi.getAchievementTypes(),
        ]);
        categories.value = c.data?.resources ?? [];
        types.value = t.data?.resources ?? [];
      } finally {
        loading.value = false;
        pending = null;
      }
    })();
    return pending;
  }

  async function reload(): Promise<void> {
    categories.value = null;
    types.value = null;
    pending = null;
    return load();
  }

  return { categories, types, loading, load, reload };
}
