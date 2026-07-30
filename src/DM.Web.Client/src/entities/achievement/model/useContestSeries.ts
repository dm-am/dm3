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

export function useContestSeries() {
  async function load(): Promise<void> {
    if (awardTypes.value !== null && series.value !== null) return;
    if (pending !== null) return pending;
    pending = (async () => {
      loading.value = true;
      try {
        const [a, s] = await Promise.all([
          achievementApi.getAwardTypes(),
          achievementApi.getContestSeries(),
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

  async function reload(): Promise<void> {
    awardTypes.value = null;
    series.value = null;
    pending = null;
    return load();
  }

  return { awardTypes, series, loading, load, reload };
}
