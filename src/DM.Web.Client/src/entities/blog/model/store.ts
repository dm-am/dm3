import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { Blog, BlogRef } from "./types";
import blogApi from "../api/blogApi";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { Api } from "@/shared/api";

/**
 * Search parameters for blogs query (frontend model)
 */
export interface BlogsSearchParams {
  search?: string;
  status?: string;
  /** Hosts filter - author OR assistant (OR logic) */
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

// Cache configuration
const CACHE_TTL = 30_000; // 30 seconds
const searchCache = new Map<
  string,
  { data: ListEnvelope<Blog>; timestamp: number }
>();

function createCacheKey(params: BlogsSearchParams): string {
  return JSON.stringify({
    search: params.search || "",
    status: params.status || "",
    hostUsernames: params.hostUsernames?.slice().sort() || [],
    createdFromUtc: params.createdFromUtc || "",
    createdToUtc: params.createdToUtc || "",
    activatedFromUtc: params.activatedFromUtc || "",
    activatedToUtc: params.activatedToUtc || "",
    closedFromUtc: params.closedFromUtc || "",
    closedToUtc: params.closedToUtc || "",
    sortBy: params.sortBy || "created",
    sortOrder: params.sortOrder || "desc",
    number: params.number || 1,
    size: params.size || 20,
  });
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

  // Request counter to handle race conditions
  let currentRequestId = 0;

  /**
   * Search blogs with caching (stale-while-revalidate)
   */
  async function searchBlogs(params: BlogsSearchParams): Promise<void> {
    const requestId = ++currentRequestId;

    lastSearchParams.value = params;
    const cacheKey = createCacheKey(params);
    const cached = searchCache.get(cacheKey);
    const now = Date.now();

    // Return cached data immediately if fresh
    if (cached && now - cached.timestamp < CACHE_TTL) {
      searchResult.value = cached.data;
      searchLoading.value = false;
      return;
    }

    // Show cached data while revalidating (stale-while-revalidate)
    if (cached) {
      searchResult.value = cached.data;
    }

    searchLoading.value = true;
    searchError.value = null;

    // Map frontend params to backend API params
    const pageSize = params.size || 20;
    const pageNumber = params.number || 1;
    const apiParams: Record<
      string,
      string | number | boolean | string[] | number[] | undefined
    > = {
      take: pageSize,
    };

    // Convert page number to skip (number is 1-indexed page)
    if (pageNumber > 1) {
      apiParams.skip = (pageNumber - 1) * pageSize;
    }

    // Search
    if (params.search) {
      apiParams.search = params.search;
    }

    // Status
    if (params.status) {
      apiParams.status = params.status;
    }

    // Hosts (author OR assistant)
    if (params.hostUsernames && params.hostUsernames.length > 0) {
      apiParams.hostUsernames = params.hostUsernames;
    }

    // Date ranges
    if (params.createdFromUtc) {
      apiParams.createdFromUtc = params.createdFromUtc;
    }
    if (params.createdToUtc) {
      apiParams.createdToUtc = params.createdToUtc;
    }
    if (params.activatedFromUtc) {
      apiParams.activatedFromUtc = params.activatedFromUtc;
    }
    if (params.activatedToUtc) {
      apiParams.activatedToUtc = params.activatedToUtc;
    }
    if (params.closedFromUtc) {
      apiParams.closedFromUtc = params.closedFromUtc;
    }
    if (params.closedToUtc) {
      apiParams.closedToUtc = params.closedToUtc;
    }

    // Sort
    if (params.sortBy) {
      apiParams.sortBy = params.sortBy;
    }
    if (params.sortOrder) {
      apiParams.sortOrder = params.sortOrder;
    }

    try {
      const { data, error } = await Api.get<ListEnvelope<Blog>>(
        "blogs",
        apiParams,
      );

      // Ignore stale responses
      if (requestId !== currentRequestId) {
        return;
      }

      if (error) {
        searchError.value = "Ошибка загрузки данных";
        return;
      }

      if (data) {
        searchResult.value = data;
        searchCache.set(cacheKey, { data, timestamp: now });
        // Clean old entries (keep last 20)
        if (searchCache.size > 20) {
          const firstKey = searchCache.keys().next().value;
          if (firstKey) searchCache.delete(firstKey);
        }
      }
    } catch {
      if (requestId === currentRequestId) {
        searchError.value = "Неожиданная ошибка";
      }
    } finally {
      // Only set loading false if this is the current request
      if (requestId === currentRequestId) {
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
    const cacheKey = createCacheKey(params);

    // Skip if already cached
    if (searchCache.has(cacheKey)) return;

    // Map params to API params (same as searchBlogs)
    const pageSize = params.size || 20;
    const apiParams: Record<
      string,
      string | number | boolean | string[] | number[] | undefined
    > = {
      take: pageSize,
    };

    // Convert page number to skip (number is 1-indexed page)
    if (page > 1) {
      apiParams.skip = (page - 1) * pageSize;
    }
    if (params.search) apiParams.search = params.search;
    if (params.status) apiParams.status = params.status;
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

    const { data } = await Api.get<ListEnvelope<Blog>>("blogs", apiParams);
    if (data) {
      searchCache.set(cacheKey, { data, timestamp: Date.now() });
      // Clean old entries (keep last 20)
      if (searchCache.size > 20) {
        const firstKey = searchCache.keys().next().value;
        if (firstKey) searchCache.delete(firstKey);
      }
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
    resetAllBlogs: () => {
      active.reset();
      popular.reset();
      participating.reset();
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
