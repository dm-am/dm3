import { defineStore } from "pinia";
import { ref, computed, onUnmounted } from "vue";
import type { LiveStats } from "@/api/models/community";
import communityApi from "@/api/requests/communityApi";

/** Default polling interval: 30 seconds */
const DEFAULT_POLL_INTERVAL = 30_000;

export const useStatisticsStore = defineStore("statistics", () => {
  const stats = ref<LiveStats | null>(null);
  const loading = ref(false);
  const loaded = ref(false);
  const error = ref<Error | null>(null);

  let pollIntervalId: ReturnType<typeof setInterval> | null = null;

  // Computed getters for convenient access
  const online = computed(() => stats.value?.online ?? 0);
  const users = computed(() => stats.value?.totals.users ?? { value: 0, todayDelta: 0 });
  const characters = computed(() => stats.value?.totals.characters ?? { value: 0, todayDelta: 0 });
  const games = computed(() => stats.value?.totals.games ?? { value: 0, todayDelta: 0 });
  const posts = computed(() => stats.value?.totals.gamePosts ?? { value: 0, todayDelta: 0 });

  async function fetch() {
    if (loading.value) return;
    loading.value = true;
    error.value = null;

    try {
      const { data } = await communityApi.getLiveStats();
      if (data?.resource) {
        stats.value = data.resource;
      }
    } catch (e) {
      error.value = e as Error;
      console.error("[Statistics] Failed to fetch:", e);
    } finally {
      loading.value = false;
      loaded.value = true;
    }
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
    // Actions
    fetch,
    startPolling,
    stopPolling,
  };
});
