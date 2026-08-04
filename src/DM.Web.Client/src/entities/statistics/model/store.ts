import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { LiveStats } from "@/shared/api/models/community";
import { unwrapResource } from "@/shared/api";
import { statisticsApi } from "../api";

/**
 * Default polling interval: 60 seconds.
 *
 * SiteStatistics shows approximate community counters (total users,
 * online, games, posts, etc.). There is no functional benefit to
 * sub-minute precision — the backend caches the aggregated result for
 * two minutes, so faster polling just produces redundant round-trips.
 * 60s keeps the header numbers fresh enough while halving the bandwidth
 * budget compared to the previous 30s default.
 */
const DEFAULT_POLL_INTERVAL = 60_000;

export const useStatisticsStore = defineStore("statistics", () => {
  const stats = ref<LiveStats | null>(null);
  const loading = ref(false);
  const loaded = ref(false);
  const error = ref<string | null>(null);

  let pollIntervalId: ReturnType<typeof setInterval> | null = null;

  // Computed getters for convenient access
  const online = computed(() => stats.value?.online ?? 0);
  const users = computed(
    () => stats.value?.totals?.users ?? { value: 0, todayDelta: 0 },
  );
  const characters = computed(
    () => stats.value?.totals?.characters ?? { value: 0, todayDelta: 0 },
  );
  const games = computed(
    () => stats.value?.totals?.games ?? { value: 0, todayDelta: 0 },
  );
  const posts = computed(
    () => stats.value?.totals?.gamePosts ?? { value: 0, todayDelta: 0 },
  );
  const blogs = computed(
    () => stats.value?.totals?.blogs ?? { value: 0, todayDelta: 0 },
  );
  const publications = computed(
    () => stats.value?.totals?.publications ?? { value: 0, todayDelta: 0 },
  );

  async function fetch() {
    if (loading.value) return;
    loading.value = true;
    error.value = null;

    const response = await statisticsApi.getLiveStats();
    if (response.error) {
      // Keep previously loaded stats (if any) — the next poll may recover.
      error.value = "Не удалось загрузить статистику";
    } else if (response.data) {
      stats.value = unwrapResource<LiveStats>(response.data);
    }
    loading.value = false;
    loaded.value = true;
  }

  function startPolling(interval = DEFAULT_POLL_INTERVAL) {
    stopPolling();
    fetch(); // Fetch immediately
    pollIntervalId = setInterval(fetch, interval);
  }

  function stopPolling() {
    if (pollIntervalId) {
      clearInterval(pollIntervalId);
      pollIntervalId = null;
    }
  }

  return {
    // State
    stats,
    loading,
    loaded,
    error,
    // Getters
    online,
    users,
    characters,
    games,
    posts,
    blogs,
    publications,
    // Actions
    fetch,
    startPolling,
    stopPolling,
  };
});
