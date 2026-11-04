import { ref, type Ref } from "vue";
import { achievementApi } from "../api";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";

const categories: Ref<AchievementCategory[] | null> = ref(null);
const types: Ref<AchievementType[] | null> = ref(null);
const loading = ref(false);
let pending: Promise<void> | null = null;

// With inactive records, so a deactivated category can be restored.
const adminCategories: Ref<AchievementCategory[] | null> = ref(null);
const adminTypes: Ref<AchievementType[] | null> = ref(null);
let adminPending: Promise<void> | null = null;

export function useAchievementCatalog() {
  /** @param fresh Ask the origin, for a reload after an edit. */
  async function load(fresh = false): Promise<void> {
    if (!fresh && categories.value !== null && types.value !== null) return;
    if (pending !== null) return pending;
    pending = (async () => {
      loading.value = true;
      try {
        const [c, t] = await Promise.all([
          achievementApi.getAchievementCategories(fresh),
          achievementApi.getAchievementTypes(fresh),
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

  /** The admin catalog, inactive included. Lazy like load(). */
  async function loadAdmin(): Promise<void> {
    if (adminCategories.value !== null && adminTypes.value !== null) return;
    if (adminPending !== null) return adminPending;
    adminPending = (async () => {
      loading.value = true;
      try {
        const [c, t] = await Promise.all([
          achievementApi.getModerationAchievementCategories(),
          achievementApi.getModerationAchievementTypes(),
        ]);
        adminCategories.value = c.data?.resources ?? [];
        adminTypes.value = t.data?.resources ?? [];
      } finally {
        loading.value = false;
        adminPending = null;
      }
    })();
    return adminPending;
  }

  /** Both slices go stale on an admin edit; see useContestSeries.reload. */
  async function reload(): Promise<void> {
    categories.value = null;
    types.value = null;
    pending = null;
    const wasAdmin =
      adminCategories.value !== null || adminTypes.value !== null;
    adminCategories.value = null;
    adminTypes.value = null;
    adminPending = null;
    await load(true);
    if (wasAdmin) await loadAdmin();
  }

  return {
    categories,
    types,
    adminCategories,
    adminTypes,
    loading,
    load,
    loadAdmin,
    reload,
  };
}
