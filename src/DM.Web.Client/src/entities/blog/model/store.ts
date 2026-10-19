// The blog lists; a single blog lives in detailsStore.ts.

import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { Blog, BlogStatus } from "./types";
import { createBlogLists } from "./blogLists";
import { Api } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { describeFailureOrWait } from "@/shared/lib/errors";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { pageSkip } from "@/shared/api/pageSkip";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";

export interface BlogsSearchParams {
  search?: string;
  /** One status, the way the filter offers it; the wire takes a list. */
  status?: BlogStatus;
  /** The author or an assistant. */
  hostUsernames?: string[];
  createdFromUtc?: string;
  createdToUtc?: string;
  activatedFromUtc?: string;
  activatedToUtc?: string;
  closedFromUtc?: string;
  closedToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
  number?: number;
  size?: number;
}

type BlogsApiParams = Record<
  string,
  string | number | boolean | string[] | number[] | undefined
>;

// Sent under their own names when they are set.
const AS_IS = [
  "search",
  "createdFromUtc",
  "createdToUtc",
  "activatedFromUtc",
  "activatedToUtc",
  "closedFromUtc",
  "closedToUtc",
  "sortBy",
  "sortOrder",
] as const;

// One mapping for search and prefetch, which share the cache keys.
function buildApiParams(params: BlogsSearchParams): BlogsApiParams {
  const pageSize = params.size || DEFAULT_PAGE_SIZE;
  const pageNumber = params.number || 1;
  const apiParams: BlogsApiParams = { take: pageSize };

  apiParams.skip = pageSkip(pageNumber, pageSize);

  for (const key of AS_IS) {
    if (params[key]) apiParams[key] = params[key];
  }
  if (params.status) apiParams.statuses = [params.status];

  if (params.hostUsernames && params.hostUsernames.length > 0) {
    apiParams.hostUsernames = params.hostUsernames;
  }

  return apiParams;
}

// For a failure that carries no problem document to read.
const LOAD_FAILURE = "Не удалось загрузить блоги";

export const useBlogsStore = defineStore("blogs", () => {
  const { lists, invalidateLists } = createBlogLists();

  const searchResult = ref<ListEnvelope<Blog> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<BlogsSearchParams | null>(null);

  // Inside the store, so a fresh pinia starts with an empty cache.
  const searchCache = createKeyedCache<ListEnvelope<Blog>>({ ttlMs: 30_000 });

  const requestGuard = createRequestGuard();

  async function searchBlogs(params: BlogsSearchParams): Promise<void> {
    const requestId = requestGuard.next();

    // Before the cache lookup, so an old error never survives a cache hit.
    searchError.value = null;

    lastSearchParams.value = params;
    const cacheKey = stableCacheKey(params);
    const fresh = searchCache.get(cacheKey);

    if (fresh) {
      searchResult.value = fresh;
      searchLoading.value = false;
      return;
    }

    const stale = searchCache.getStale(cacheKey);
    if (stale) {
      searchResult.value = stale;
    }

    searchLoading.value = true;

    try {
      // Display written out for the flags: the strip says the refusal in the rows.
      const { data, error } = await Api.get<ListEnvelope<Blog>>(
        "blogs",
        buildApiParams(params),
        RENDER_AUDIENCE.Display,
        OWNS_REFUSAL,
      );

      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      if (error) {
        searchError.value = describeFailureOrWait(error, LOAD_FAILURE);
        return;
      }

      if (data) {
        searchResult.value = data;
        searchCache.set(cacheKey, data);
      }
    } finally {
      if (requestGuard.isCurrent(requestId)) {
        searchLoading.value = false;
      }
    }
  }

  function clearSearchCache(): void {
    searchCache.clear();
  }

  async function prefetchPage(page: number): Promise<void> {
    if (!lastSearchParams.value) return;

    const params = { ...lastSearchParams.value, number: page };
    const cacheKey = stableCacheKey(params);

    // Replacing a stale entry is the point of a prefetch.
    if (searchCache.get(cacheKey)) return;

    const { data } = await Api.get<ListEnvelope<Blog>>(
      "blogs",
      buildApiParams(params),
    );
    if (data) {
      searchCache.set(cacheKey, data);
    }
  }

  return {
    ...lists,

    // Refetches rather than blanks: the sidebar mounts once per session.
    invalidateBlogLists: async () => {
      clearSearchCache();
      await invalidateLists();
    },

    // Search API
    searchResult,
    searchLoading,
    searchError,
    searchBlogs,
    prefetchPage,
    clearSearchCache,
  };
});
