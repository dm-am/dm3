// Rated posts store (for homepage highlights)

import { defineStore } from "pinia";
import { ref } from "vue";
import type { Post } from "./types";
import gameApi from "../api/gameApi";

/** Cache TTL: 5 minutes (rated posts don't change often) */
const CACHE_TTL = 300_000;

/**
 * Get the start of the current calendar week (Monday 00:00:00 UTC)
 * Week runs Monday to Sunday
 */
function getWeekStartUtc(): Date {
  const now = new Date();
  const dayOfWeek = now.getUTCDay(); // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
  // Days since Monday: Sunday goes back 6 days, Monday = 0, Tuesday = 1, etc.
  const daysSinceMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const monday = new Date(now);
  monday.setUTCDate(now.getUTCDate() - daysSinceMonday);
  monday.setUTCHours(0, 0, 0, 0);
  return monday;
}

export const useFeaturedPostsStore = defineStore("featuredPosts", () => {
  const bestOfWeek = ref<Post | null>(null);
  const lastWithPlus = ref<Post | null>(null);
  const loadingBest = ref(false);
  const loadingLatest = ref(false);
  const loaded = ref(false);

  // Cache timestamps
  let lastFetchBest = 0;
  let lastFetchLatest = 0;

  async function fetchBestOfWeek(force = false) {
    const now = Date.now();

    // Skip if cached and not forced
    if (!force && bestOfWeek.value !== null && now - lastFetchBest < CACHE_TTL) {
      return;
    }

    if (loadingBest.value) return;
    loadingBest.value = true;
    try {
      const weekStart = getWeekStartUtc();

      const response = await gameApi.getRatedPosts({
        sortBy: "rating",
        hasReviews: true,
        reviewedAfter: weekStart.toISOString(),
        take: 1,
      });
      bestOfWeek.value = response.data?.resources?.[0] ?? null;
      lastFetchBest = now;
    } finally {
      loadingBest.value = false;
      loaded.value = true;
    }
  }

  async function fetchLatestFeatured(force = false) {
    const now = Date.now();

    // Skip if cached and not forced
    if (!force && lastWithPlus.value !== null && now - lastFetchLatest < CACHE_TTL) {
      return;
    }

    if (loadingLatest.value) return;
    loadingLatest.value = true;
    try {
      const response = await gameApi.getRatedPosts({
        sortBy: "lastreview",
        hasReviews: true,
        take: 1,
      });
      lastWithPlus.value = response.data?.resources?.[0] ?? null;
      lastFetchLatest = now;
    } finally {
      loadingLatest.value = false;
      loaded.value = true;
    }
  }

  return {
    bestOfWeek,
    lastWithPlus,
    loadingBest,
    loadingLatest,
    loaded,
    fetchBestOfWeek,
    fetchLatestFeatured,
  };
});
