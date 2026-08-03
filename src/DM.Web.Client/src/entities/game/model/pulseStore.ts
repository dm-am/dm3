// Pulse posts store (rated posts for /pulse page)

import { defineStore } from "pinia";
import { ref } from "vue";
import type { Post } from "./types";
import type { ListEnvelope, Paging } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";
import { createKeyedCache } from "@/shared/lib/utils/keyedCache";
import {
  buildRatedPostsParams,
  type PulseSearchParams,
  type RatedPostsApiParams,
} from "./ratedPostsParams";

// Re-export for entities/game public API consumers — the implementation
// moved to the shared datetime utils (SSOT).
export { getWeekStartUtc };

/**
 * The pulse is one week's slice of the shared rated-posts list: the request
 * every rated-posts surface builds, narrowed to the reviews of the last seven
 * days. The narrowing is the only thing this page adds, so it is the only
 * thing spelled here.
 */
function buildApiParams(params: PulseSearchParams): RatedPostsApiParams {
  return {
    ...buildRatedPostsParams(params),
    lastReviewedAfter: getWeekStartUtc(),
  };
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

  const pageCache = createKeyedCache<ListEnvelope<Post>>({ ttlMs: 30_000 });

  /**
   * Fetch rated posts for the current week
   */
  async function fetchPosts(params: PulseSearchParams) {
    const apiParams = buildApiParams(params);
    const requestKey = JSON.stringify(apiParams);
    lastParams = params;

    // Serve from cache when fresh (warmed by prefetchPage)
    const cached = pageCache.get(requestKey);
    if (cached) {
      currentRequestKey = requestKey;
      posts.value = cached.resources ?? [];
      paging.value = cached.paging ?? null;
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
      if (data) pageCache.set(requestKey, data);
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
    // Fresh only: a stale entry is exactly what the prefetch should replace.
    if (pageCache.get(requestKey)) return;

    // Fetch in background without updating UI
    const { data } = await gameApi.getRatedPosts(apiParams);
    if (data) pageCache.set(requestKey, data);
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
