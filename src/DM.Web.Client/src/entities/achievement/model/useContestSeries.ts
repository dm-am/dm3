import { ref, type Ref } from "vue";
import { achievementApi } from "../api";
import type {
  AwardType,
  ContestSeries,
} from "@/shared/api/models/achievements";

/**
 * Singleton cache of the award catalog + contest series: load() is lazy,
 * reload() is for admin edits (after create/edit/deactivate).
 * Shared between the profile awards section and the admin pages.
 */
const awardTypes: Ref<AwardType[] | null> = ref(null);
const series: Ref<ContestSeries[] | null> = ref(null);
const loading = ref(false);
let pending: Promise<void> | null = null;

// The admin slice: same catalogs WITH inactive records. Kept apart from the
// public refs above so the profile never renders a hidden series, while the
// admin list always has something to hang its "Вернуть" button on.
const adminAwardTypes: Ref<AwardType[] | null> = ref(null);
const adminSeries: Ref<ContestSeries[] | null> = ref(null);
let adminPending: Promise<void> | null = null;

export function useContestSeries() {
  /**
   * @param fresh Ask the origin rather than either cache. The catalogue answers
   * `Cache-Control: public, max-age=300`, so a reload right after an edit would
   * otherwise be served the browser's pre-edit copy.
   */
  async function load(fresh = false): Promise<void> {
    if (!fresh && awardTypes.value !== null && series.value !== null) return;
    if (pending !== null) return pending;
    pending = (async () => {
      loading.value = true;
      try {
        const [a, s] = await Promise.all([
          achievementApi.getAwardTypes(fresh),
          achievementApi.getContestSeries(fresh),
        ]);
        awardTypes.value = a.data?.resources ?? [];
        series.value = s.data?.resources ?? [];
      } finally {
        loading.value = false;
        pending = null;
      }
    })();
    return pending;
  }

  /** The admin catalogs, inactive included. Lazy like load(). */
  async function loadAdmin(): Promise<void> {
    if (adminAwardTypes.value !== null && adminSeries.value !== null) return;
    if (adminPending !== null) return adminPending;
    adminPending = (async () => {
      loading.value = true;
      try {
        const [a, s] = await Promise.all([
          achievementApi.getModerationAwardTypes(),
          achievementApi.getModerationContestSeries(),
        ]);
        adminAwardTypes.value = a.data?.resources ?? [];
        adminSeries.value = s.data?.resources ?? [];
      } finally {
        loading.value = false;
        adminPending = null;
      }
    })();
    return adminPending;
  }

  /**
   * After an admin edit both slices are stale: the profile must see the
   * change and the admin list must keep showing the hidden record. The
   * admin slice reloads only if something ever loaded it.
   */
  async function reload(): Promise<void> {
    awardTypes.value = null;
    series.value = null;
    pending = null;
    const wasAdmin =
      adminAwardTypes.value !== null || adminSeries.value !== null;
    adminAwardTypes.value = null;
    adminSeries.value = null;
    adminPending = null;
    await load(true);
    if (wasAdmin) await loadAdmin();
  }

  return {
    awardTypes,
    series,
    adminAwardTypes,
    adminSeries,
    loading,
    load,
    loadAdmin,
    reload,
  };
}
