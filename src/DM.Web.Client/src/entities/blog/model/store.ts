// Store for the blog lists (sidebar, search page).
//
// A single blog and everything hanging off it lives in detailsStore.ts.

import { defineStore } from "pinia";
import { ref } from "vue";
import type { GeneralError, ListEnvelope } from "@/shared/api/models/common";
import type { Blog, BlogRef, BlogStatus } from "./types";
import blogApi from "../api/blogApi";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { Api } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { describeFailure, describeRateLimit } from "@/shared/lib/errors";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";

/**
 * Search parameters for blogs query (frontend model)
 */
export interface BlogsSearchParams {
  search?: string;
  /** One status, the way the filter offers it; the wire takes a list. */
  status?: BlogStatus;
  /** Hosts filter - author (owner) OR assistant (OR logic) */
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

/**
 * Map frontend search params to backend API query params. Single source of
 * truth used by both searchBlogs and prefetchPage: the cache key is built from
 * the whole params object, so a filter added to one of them and not the other
 * files an unfiltered page under a filtered key.
 */
function buildApiParams(params: BlogsSearchParams): BlogsApiParams {
  const pageSize = params.size || 20;
  const pageNumber = params.number || 1;
  const apiParams: BlogsApiParams = { take: pageSize };

  // Convert page number to skip (number is 1-indexed page)
  if (pageNumber > 1) {
    apiParams.skip = (pageNumber - 1) * pageSize;
  }

  if (params.search) apiParams.search = params.search;
  if (params.status) apiParams.statuses = [params.status];

  // Hosts (author OR assistant)
  if (params.hostUsernames && params.hostUsernames.length > 0) {
    apiParams.hostUsernames = params.hostUsernames;
  }

  if (params.createdFromUtc) apiParams.createdFromUtc = params.createdFromUtc;
  if (params.createdToUtc) apiParams.createdToUtc = params.createdToUtc;
  if (params.activatedFromUtc)
    apiParams.activatedFromUtc = params.activatedFromUtc;
  if (params.activatedToUtc) apiParams.activatedToUtc = params.activatedToUtc;
  if (params.closedFromUtc) apiParams.closedFromUtc = params.closedFromUtc;
  if (params.closedToUtc) apiParams.closedToUtc = params.closedToUtc;

  if (params.sortBy) apiParams.sortBy = params.sortBy;
  if (params.sortOrder) apiParams.sortOrder = params.sortOrder;

  return apiParams;
}

/**
 * One sentence for both failure paths of the blogs list. A refusal the API
 * named goes through describeFailure, which prefers the server's own title (a
 * wait is the one exception, worded by searchFailure below); a request that got
 * no response carries no problem document to read, and neither does a throw, so
 * both fall back to this.
 */
const LOAD_FAILURE = "Не удалось загрузить блоги";

/**
 * The search is the only carrier of its own failures.
 *
 * BlogsDataTable stands a strip where the rows would be, so the sentence is
 * already on screen and in the place the reader is looking. The response
 * interceptor used to put the same words in a plate over it on a 403, a 429 and
 * a 5xx; with both flags it keeps quiet about this request and the strip
 * answers alone.
 *
 * An expired session and a dead connection are deliberately not in here. The
 * interceptor speaks for both, because neither is about this list — one is
 * about the reader's session, the other about the road to the site, and only
 * the interceptor can offer the other address. The strip beside them says a
 * different thing: that there are no rows.
 */
const SEARCH_OWNS_REFUSAL = {
  ownsRefusal: true,
  ownsRateLimit: true,
} as const;

/**
 * What the strip says about a failed search.
 *
 * A wait keeps the shared wording instead of the title the API sent with it:
 * that title names the refusal without the number, and the number is the part a
 * reader acts on. It is also the sentence the interceptor would have raised, so
 * taking the refusal over costs the reader nothing. Everything else the API
 * named is relayed as it stands.
 */
function searchFailure(error: GeneralError): string {
  return error.status === 429
    ? describeRateLimit(error.retryAfterSeconds)
    : describeFailure(error, LOAD_FAILURE);
}

export const useBlogsStore = defineStore("blogs", () => {
  // Sidebar lists with caching (60s TTL by default) - use lightweight BlogRef
  const active = useApiList<BlogRef>(() => blogApi.getActiveBlogs());
  const popular = useApiList<BlogRef>(() => blogApi.getPopularBlogs());

  // User-specific lists (still cached, but reset on logout) - use lightweight BlogRef
  // participating = blogs where user is author, assistant, or subscriber
  const participating = useApiList<BlogRef>(() =>
    blogApi.getParticipatingBlogs(),
  );

  // Search state for blogs page
  const searchResult = ref<ListEnvelope<Blog> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<BlogsSearchParams | null>(null);

  // Inside the store and not beside it, the way every other list store here
  // holds its cache. In the browser the two placements have one lifetime — a
  // single pinia, created once and never disposed — so no session behaves
  // differently; what changes is that the cache dies with the store instance,
  // so a fresh pinia starts empty instead of being cleared by hand.
  const searchCache = createKeyedCache<ListEnvelope<Blog>>({ ttlMs: 30_000 });

  // Request guard to discard stale out-of-order responses
  const requestGuard = createRequestGuard();

  /**
   * Search blogs with caching (stale-while-revalidate)
   */
  async function searchBlogs(params: BlogsSearchParams): Promise<void> {
    const requestId = requestGuard.next();

    // Reset error before the cache lookup so a stale error never survives
    // a cache-hit navigation.
    searchError.value = null;

    lastSearchParams.value = params;
    const cacheKey = stableCacheKey(params);
    const fresh = searchCache.get(cacheKey);

    if (fresh) {
      searchResult.value = fresh;
      searchLoading.value = false;
      return;
    }

    // Show cached data while revalidating (stale-while-revalidate)
    const stale = searchCache.getStale(cacheKey);
    if (stale) {
      searchResult.value = stale;
    }

    searchLoading.value = true;

    try {
      // The flags are declared above, with the reason the strip keeps the
      // refusal. The audience is written out only because they sit behind it in
      // the signature; it is the value this call sent implicitly before.
      const { data, error } = await Api.get<ListEnvelope<Blog>>(
        "blogs",
        buildApiParams(params),
        RENDER_AUDIENCE.Display,
        SEARCH_OWNS_REFUSAL,
      );

      // Ignore stale responses
      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      if (error) {
        searchError.value = searchFailure(error);
        return;
      }

      if (data) {
        searchResult.value = data;
        searchCache.set(cacheKey, data);
      }
    } catch {
      if (requestGuard.isCurrent(requestId)) {
        searchError.value = LOAD_FAILURE;
      }
    } finally {
      // Only set loading false if this is the current request
      if (requestGuard.isCurrent(requestId)) {
        searchLoading.value = false;
      }
    }
  }

  /**
   * Clear search cache
   */
  function clearSearchCache(): void {
    searchCache.clear();
  }

  /**
   * Prefetch a page in background (for next page optimization)
   */
  async function prefetchPage(page: number): Promise<void> {
    if (!lastSearchParams.value) return;

    const params = { ...lastSearchParams.value, number: page };
    const cacheKey = stableCacheKey(params);

    // Skip only while the entry is fresh: replacing a stale one is the point of
    // a prefetch.
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
    // Data
    activeBlogs: active.data,
    popularBlogs: popular.data,
    participatingBlogs: participating.data,

    // Loading states
    activeBlogsLoading: active.loading,
    popularBlogsLoading: popular.loading,
    participatingBlogsLoading: participating.loading,

    // Error states
    activeBlogsError: active.error,
    popularBlogsError: popular.error,
    participatingBlogsError: participating.error,

    // Fetch functions
    fetchActiveBlogs: active.fetch,
    fetchPopularBlogs: popular.fetch,
    fetchParticipatingBlogs: participating.fetch,

    // Reset functions (for logout)
    resetParticipatingBlogs: participating.reset,

    /**
     * After a mutation changed which blogs exist. Distinct from the reset
     * above, which blanks the lists: that is right for logout and wrong here,
     * because the sidebar blocks fetch on mount and the shell mounts once per
     * session, so a blanked list stays blank until a reload.
     */
    invalidateBlogLists: async () => {
      clearSearchCache();
      await Promise.all([
        active.invalidate(),
        popular.invalidate(),
        participating.invalidate(),
      ]);
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
