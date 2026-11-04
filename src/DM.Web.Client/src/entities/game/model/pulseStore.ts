import { defineStore } from "pinia";
import { ref } from "vue";
import type { Post, RatedPostsQuery } from "./postTypes";
import type { ListEnvelope, PagingInfo } from "@/shared/api/models/common";
import postApi from "../api/postApi";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  buildRatedPostsParams,
  type PulseSearchParams,
} from "./ratedPostsParams";

// The pulse is the shared rated-posts request narrowed to the reviews of the week.
function buildApiParams(params: PulseSearchParams): RatedPostsQuery {
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

  // A guard and not a key: leaving a filter and coming back makes the key match again.
  const guard = createRequestGuard();

  // Only so that a repeat of the request on the wire sends no second one.
  let inFlightKey: string | null = null;

  let lastParams: PulseSearchParams | null = null;

  // Keyed on the filter: the request carries a timestamp taken at call time.
  const pageCache = createKeyedCache<ListEnvelope<Post>>({ ttlMs: 30_000 });

  async function fetchPosts(params: PulseSearchParams) {
    const apiParams = buildApiParams(params);
    const requestKey = stableCacheKey(params);
    lastParams = params;

    const cached = pageCache.get(requestKey);
    if (cached) {
      // An answer like any other: what is still on the wire is now the older one.
      guard.next();
      inFlightKey = null;
      posts.value = cached.resources;
      paging.value = cached.paging;
      error.value = null;
      loading.value = false;
      return;
    }

    if (requestKey === inFlightKey && loading.value) {
      return;
    }
    inFlightKey = requestKey;

    const requestId = guard.next();
    loading.value = true;
    error.value = null;

    const { data, error: requestError } =
      await postApi.getRatedPosts(apiParams);

    if (!guard.isCurrent(requestId)) return;
    inFlightKey = null;

    if (requestError) {
      // The posts already loaded stay.
      error.value = "Не удалось загрузить оцененные посты";
    } else {
      posts.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
      if (data) pageCache.set(requestKey, data);
    }

    loading.value = false;
  }

  async function prefetchPage(page: number): Promise<void> {
    if (!lastParams) return;

    const nextParams = { ...lastParams, number: page };
    const apiParams = buildApiParams(nextParams);
    const requestKey = stableCacheKey(nextParams);

    // Replacing a stale entry is the point of a prefetch.
    if (pageCache.get(requestKey)) return;

    const { data } = await postApi.getRatedPosts(apiParams);
    if (data) pageCache.set(requestKey, data);
  }

  function clear() {
    posts.value = [];
    paging.value = null;
    error.value = null;
    // An answer already on the wire has to be dropped.
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
