// Pulse posts store (rated posts for /pulse page)

import { defineStore } from "pinia";
import { ref } from "vue";
import type { Post } from "./types";
import type { ListEnvelope, Paging } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";

// Re-export for entities/game public API consumers — the implementation
// moved to the shared datetime utils (SSOT).
export { getWeekStartUtc };

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

type RatedPostsApiParams = NonNullable<
  Parameters<typeof gameApi.getRatedPosts>[0]
>;

/**
 * Build API params for the rated-posts request.
 * Invalid date strings (e.g. hand-edited URL params) are ignored
 * instead of producing an Invalid Date that throws on toISOString().
 */
function buildApiParams(params: PulseSearchParams): RatedPostsApiParams {
  const apiParams: RatedPostsApiParams = {
    sortBy: params.sortBy || "lastreview",
    sortOrder: params.sortOrder || "desc",
    hasReviews: true,
    lastReviewedAfter: getWeekStartUtc(),
  };

  if (params.search) apiParams.search = params.search;
  if (params.minRating !== undefined && params.minRating !== null)
    apiParams.minRating = params.minRating;
  if (params.maxRating !== undefined && params.maxRating !== null)
    apiParams.maxRating = params.maxRating;
  if (params.authorUsernames)
    apiParams.authorUsernames = params.authorUsernames;
  if (params.createdFrom) {
    const createdAfter = new Date(params.createdFrom + "T00:00:00Z");
    if (!isNaN(createdAfter.getTime()))
      apiParams.createdAfter = createdAfter.toISOString();
  }
  if (params.createdTo) {
    const createdBefore = new Date(params.createdTo + "T23:59:59.999Z");
    if (!isNaN(createdBefore.getTime()))
      apiParams.createdBefore = createdBefore.toISOString();
  }
  if (params.gameId) apiParams.gameId = params.gameId;

  // Pagination
  const pageSize = params.size || 20;
  apiParams.take = pageSize;
  if (params.number && params.number > 1) {
    apiParams.skip = (params.number - 1) * pageSize;
  }

  return apiParams;
}

export const usePulseStore = defineStore("pulse", () => {
  const posts = ref<Post[]>([]);
  const paging = ref<Paging | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Track current request to dedupe
  let currentRequestKey: string | null = null;

  // Last requested params — used by prefetchPage to derive the next page
  let lastParams: PulseSearchParams | null = null;

  // Page cache: key → { data, timestamp } (same pattern as games store)
  const CACHE_TTL = 30_000; // 30 seconds
  const pageCache = new Map<
    string,
    { data: ListEnvelope<Post>; timestamp: number }
  >();

  function cachePage(key: string, data: ListEnvelope<Post>): void {
    pageCache.set(key, { data, timestamp: Date.now() });
    // Clean old entries (keep last 20)
    if (pageCache.size > 20) {
      const firstKey = pageCache.keys().next().value;
      if (firstKey) pageCache.delete(firstKey);
    }
  }

  /**
   * Fetch rated posts for the current week
   */
  async function fetchPosts(params: PulseSearchParams) {
    const apiParams = buildApiParams(params);
    const requestKey = JSON.stringify(apiParams);
    lastParams = params;

    // Serve from cache when fresh (warmed by prefetchPage)
    const cached = pageCache.get(requestKey);
    if (cached && Date.now() - cached.timestamp < CACHE_TTL) {
      currentRequestKey = requestKey;
      posts.value = cached.data.resources ?? [];
      paging.value = cached.data.paging ?? null;
      error.value = null;
      loading.value = false;
      return;
    }

    // Skip duplicate in-flight request
    if (requestKey === currentRequestKey && loading.value) {
      return;
    }
    currentRequestKey = requestKey;

    loading.value = true;
    error.value = null;

    const { data, error: requestError } =
      await gameApi.getRatedPosts(apiParams);

    // Only update if this is still the current request
    if (requestKey !== currentRequestKey) return;

    if (requestError) {
      // Keep previously loaded posts; the page renders the error state
      error.value = "Не удалось загрузить посты";
    } else {
      posts.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
      if (data) cachePage(requestKey, data);
    }

    loading.value = false;
  }

  /**
   * Prefetch a page in background (for next page optimization).
   * Does not update visible results, only warms the cache.
   */
  async function prefetchPage(page: number): Promise<void> {
    if (!lastParams) return;

    const apiParams = buildApiParams({ ...lastParams, number: page });
    const requestKey = JSON.stringify(apiParams);

    // Skip if already cached
    if (pageCache.has(requestKey)) return;

    // Fetch in background without updating UI
    const { data } = await gameApi.getRatedPosts(apiParams);
    if (data) cachePage(requestKey, data);
  }

  /**
   * Clear store state
   */
  function clear() {
    posts.value = [];
    paging.value = null;
    error.value = null;
    currentRequestKey = null;
    lastParams = null;
    pageCache.clear();
  }

  return {
    posts,
    paging,
    loading,
    error,
    fetchPosts,
    prefetchPage,
    clear,
  };
});
