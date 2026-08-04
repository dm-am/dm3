// Pulse posts store (rated posts for /pulse page)

import { defineStore } from "pinia";
import { ref } from "vue";
import type { Post } from "./types";
import type { ListEnvelope, PagingInfo } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
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
    lastReviewedFromUtc: getWeekStartUtc(),
  };
}

export const usePulseStore = defineStore("pulse", () => {
  const posts = ref<Post[]>([]);
  const paging = ref<PagingInfo | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // The one race guard, not a comparison of request keys of its own. The key
  // said "the answer I am holding was asked for under the parameters that are
  // current", which is not the same question: leave a filter and come back to it
  // while the first answer is still on the wire, and the key matches again, so
  // the abandoned answer repaints the page it was asked for two requests ago.
  const guard = createRequestGuard();

  // Kept, but only for what a key can answer: whether the very same request is
  // already on the wire, so a repeat does not put a second one there.
  let inFlightKey: string | null = null;

  // Last requested params — used by prefetchPage to derive the next page
  let lastParams: PulseSearchParams | null = null;

  // Keyed on the reader's filter, not on the request built from it. The request
  // carries `lastReviewedFromUtc: getWeekStartUtc()`, a millisecond timestamp
  // computed at call time, so a key taken off it was a different string on every
  // call: nothing was ever read back, and prefetchPage warmed entries under keys
  // nobody would ask for again. The filter is what the reader means by "this
  // page"; the seven-day window is derived from it and identical inside the TTL.
  const pageCache = createKeyedCache<ListEnvelope<Post>>({ ttlMs: 30_000 });

  /**
   * Fetch rated posts for the current week
   */
  async function fetchPosts(params: PulseSearchParams) {
    const apiParams = buildApiParams(params);
    const requestKey = stableCacheKey(params);
    lastParams = params;

    // Serve from cache when fresh (warmed by prefetchPage)
    const cached = pageCache.get(requestKey);
    if (cached) {
      // Painting from the cache is an answer like any other: whatever is still
      // on the wire is now the older one and must not land on top of it.
      guard.next();
      inFlightKey = null;
      posts.value = cached.resources ?? [];
      paging.value = cached.paging ?? null;
      error.value = null;
      loading.value = false;
      return;
    }

    // Skip duplicate in-flight request
    if (requestKey === inFlightKey && loading.value) {
      return;
    }
    inFlightKey = requestKey;

    const requestId = guard.next();
    loading.value = true;
    error.value = null;

    const { data, error: requestError } =
      await gameApi.getRatedPosts(apiParams);

    // Only update if this is still the current request
    if (!guard.isCurrent(requestId)) return;
    inFlightKey = null;

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

    const nextParams = { ...lastParams, number: page };
    const apiParams = buildApiParams(nextParams);
    const requestKey = stableCacheKey(nextParams);

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
    // Bumped, not blanked: an answer already on the wire has to be dropped, and
    // a key set back to null matched again as soon as the same request was made.
    guard.next();
    inFlightKey = null;
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
