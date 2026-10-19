import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { LiveStats } from "@/shared/api/models/community";
import { unwrapResource } from "@/shared/api";
import { statisticsApi } from "../api";

// The server caches the counters for two minutes, so faster polling only repeats them.
const DEFAULT_POLL_INTERVAL = 60_000;

export const useStatisticsStore = defineStore("statistics", () => {
  const stats = ref<LiveStats | null>(null);
  const loading = ref(false);
  const loaded = ref(false);
  const error = ref<string | null>(null);

  let pollIntervalId: ReturnType<typeof setInterval> | null = null;

  const online = computed(() => stats.value?.online ?? 0);
  const total = (key: keyof LiveStats["totals"]) =>
    computed(() => stats.value?.totals[key] ?? { value: 0, todayDelta: 0 });
  const users = total("users");
  const characters = total("characters");
  const games = total("games");
  const posts = total("gamePosts");
  const blogs = total("blogs");
  const publications = total("publications");

  async function fetchStats() {
    if (loading.value) return;
    loading.value = true;
    error.value = null;

    const response = await statisticsApi.getLiveStats();
    if (response.error) {
      // The stats already loaded stay; the next poll may recover.
      error.value = "Не удалось загрузить статистику";
    } else if (response.data) {
      stats.value = unwrapResource<LiveStats>(response.data);
    }
    loading.value = false;
    loaded.value = true;
  }

  // A set and not a count: a block in a hidden tab starts twice and stops once.
  const pollers = new Set<object>();

  function startPolling(poller: object, interval = DEFAULT_POLL_INTERVAL) {
    pollers.add(poller);
    if (pollIntervalId) return;
    fetchStats();
    pollIntervalId = setInterval(fetchStats, interval);
  }

  function stopPolling(poller: object) {
    pollers.delete(poller);
    if (pollers.size > 0 || !pollIntervalId) return;
    clearInterval(pollIntervalId);
    pollIntervalId = null;
  }

  return {
    stats,
    loading,
    loaded,
    error,
    online,
    users,
    characters,
    games,
    posts,
    blogs,
    publications,
    fetchStats,
    startPolling,
    stopPolling,
  };
});
