// Pulse posts store (rated posts for /pulse page)

import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { Post } from "./types";
import type { ListEnvelope, Paging } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";

/**
 * Get the start of the current calendar week (Monday 00:00:00 UTC)
 * Week runs Monday to Sunday
 */
export function getWeekStartUtc(): Date {
  const now = new Date();
  const dayOfWeek = now.getUTCDay(); // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
  // Days since Monday: Sunday goes back 6 days, Monday = 0, Tuesday = 1, etc.
  const daysSinceMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const monday = new Date(now);
  monday.setUTCDate(now.getUTCDate() - daysSinceMonday);
  monday.setUTCHours(0, 0, 0, 0);
  return monday;
}

export interface PulseSearchParams {
  sortBy?: "rating" | "lastreview" | "reviewcount" | "created";
  sortOrder?: "asc" | "desc";
  search?: string;
  minRating?: number;
  maxRating?: number;
  authorUsernames?: string;
  createdFrom?: string;
  createdTo?: string;
  gameId?: string;
  number?: number;
  size?: number;
}

export const usePulseStore = defineStore("pulse", () => {
  const posts = ref<Post[]>([]);
  const paging = ref<Paging | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Track current request to dedupe
  let currentRequestKey: string | null = null;

  /**
   * Fetch rated posts for the current week
   */
  async function fetchPosts(params: PulseSearchParams) {
    const weekStart = getWeekStartUtc();

    // Build API params
    const apiParams: Parameters<typeof gameApi.getRatedPosts>[0] = {
      sortBy: params.sortBy || "lastreview",
      sortOrder: params.sortOrder || "desc",
      hasReviews: true,
      lastReviewedAfter: weekStart.toISOString(),
    };

    if (params.search) apiParams.search = params.search;
    if (params.minRating !== undefined && params.minRating !== null) apiParams.minRating = params.minRating;
    if (params.maxRating !== undefined && params.maxRating !== null) apiParams.maxRating = params.maxRating;
    if (params.authorUsernames) apiParams.authorUsernames = params.authorUsernames;
    if (params.createdFrom) apiParams.createdAfter = new Date(params.createdFrom + "T00:00:00Z").toISOString();
    if (params.createdTo) apiParams.createdBefore = new Date(params.createdTo + "T23:59:59.999Z").toISOString();
    if (params.gameId) apiParams.gameId = params.gameId;

    // Pagination
    const pageSize = params.size || 20;
    apiParams.take = pageSize;
    if (params.number && params.number > 1) {
      apiParams.skip = (params.number - 1) * pageSize;
    }

    // Create request key for deduplication
    const requestKey = JSON.stringify(apiParams);
    if (requestKey === currentRequestKey && loading.value) {
      return; // Skip duplicate request
    }
    currentRequestKey = requestKey;

    loading.value = true;
    error.value = null;

    try {
      const response = await gameApi.getRatedPosts(apiParams);
      const data = response.data as ListEnvelope<Post>;

      // Only update if this is still the current request
      if (requestKey === currentRequestKey) {
        posts.value = data?.resources ?? [];
        paging.value = data?.paging ?? null;
      }
    } catch (err) {
      if (requestKey === currentRequestKey) {
        error.value = "Failed to load posts";
        console.error("Pulse fetch error:", err);
      }
    } finally {
      if (requestKey === currentRequestKey) {
        loading.value = false;
      }
    }
  }

  /**
   * Clear store state
   */
  function clear() {
    posts.value = [];
    paging.value = null;
    error.value = null;
    currentRequestKey = null;
  }

  return {
    posts,
    paging,
    loading,
    error,
    fetchPosts,
    clear,
  };
});
