// Community store
// Migrated from shared/stores/community.ts

import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { User, Username } from "./types";
import { UserActivityFilter } from "./types";
import { CommunityApi } from "@/shared/api";
import { useUserStore } from "./store";

/**
 * Search parameters for users query (frontend model)
 */
export interface UsersSearchParams {
  search?: string;
  activity?: "active" | "inactive" | "all";
  isOnline?: boolean;
  role?: string;
  isHonorary?: boolean;
  isNewbie?: boolean;
  minRating?: number;
  maxRating?: number;
  minGamesHosting?: number;
  maxGamesHosting?: number;
  minGamesPlaying?: number;
  maxGamesPlaying?: number;
  minBlogsHosting?: number;
  maxBlogsHosting?: number;
  registeredFromUtc?: string;
  registeredToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
  number?: number;
  size?: number;
}

// Cache configuration
const CACHE_TTL = 30_000; // 30 seconds
const searchCache = new Map<string, { data: ListEnvelope<User>; timestamp: number }>();

function createCacheKey(params: UsersSearchParams): string {
  return JSON.stringify({
    search: params.search || "",
    activity: params.activity || "active",
    isOnline: params.isOnline,
    role: params.role || "",
    isHonorary: params.isHonorary,
    isNewbie: params.isNewbie,
    minRating: params.minRating,
    maxRating: params.maxRating,
    minGamesHosting: params.minGamesHosting,
    maxGamesHosting: params.maxGamesHosting,
    minGamesPlaying: params.minGamesPlaying,
    maxGamesPlaying: params.maxGamesPlaying,
    minBlogsHosting: params.minBlogsHosting,
    maxBlogsHosting: params.maxBlogsHosting,
    registeredFromUtc: params.registeredFromUtc || "",
    registeredToUtc: params.registeredToUtc || "",
    sortBy: params.sortBy || "lastActivity",
    sortOrder: params.sortOrder || "desc",
    number: params.number || 1,
    size: params.size || 20,
  });
}

export const useCommunityStore = defineStore("community", () => {
  const { user: currentUser } = storeToRefs(useUserStore());
  const users = ref<ListEnvelope<User> | null>(null);
  const usersError = ref<number | null>(null);

  // New search state
  const searchResult = ref<ListEnvelope<User> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<UsersSearchParams | null>(null);

  async function fetchUsers(
    number: number,
    filter: UserActivityFilter = UserActivityFilter.Active,
  ) {
    const take = currentUser.value?.settings?.paging?.entitiesPerPage;
    const { data, error } = await CommunityApi.getUsers({
      number,
      take,
      filter,
    });

    if (error?.status === 403) {
      users.value = null;
      usersError.value = 403;
      return;
    }

    usersError.value = null;
    users.value = data;
  }

  /**
   * Search users with caching (stale-while-revalidate)
   */
  async function searchUsers(params: UsersSearchParams): Promise<void> {
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
    const apiParams: Record<string, unknown> = {
      take: pageSize,
    };

    // Convert page number to skip (number is 1-indexed page)
    if (pageNumber > 1) {
      apiParams.skip = (pageNumber - 1) * pageSize;
    }

    // Search
    if (params.search) {
      apiParams.q = params.search;
    }

    // Activity filter -> backend Activity enum (param name is 'activity', not 'filter')
    if (params.activity) {
      const activityMap: Record<string, UserActivityFilter> = {
        active: UserActivityFilter.Active,
        inactive: UserActivityFilter.Inactive,
        all: UserActivityFilter.All,
      };
      apiParams.activity = activityMap[params.activity] ?? UserActivityFilter.Active;
    }

    // Role
    if (params.role && params.role !== "all") {
      apiParams.role = params.role;
    }

    // Online filter
    if (params.isOnline === true) {
      apiParams.isOnline = true;
    }

    // Honorary filter (only when filtering by role)
    if (params.isHonorary !== undefined) {
      apiParams.isHonorary = params.isHonorary;
    }

    // Newbie filter
    if (params.isNewbie !== undefined) {
      apiParams.isNewbie = params.isNewbie;
    }

    // Rating range filters
    if (params.minRating !== undefined) {
      apiParams.minRating = params.minRating;
    }
    if (params.maxRating !== undefined) {
      apiParams.maxRating = params.maxRating;
    }

    // Games hosting range filters
    if (params.minGamesHosting !== undefined) {
      apiParams.minGamesHosting = params.minGamesHosting;
    }
    if (params.maxGamesHosting !== undefined) {
      apiParams.maxGamesHosting = params.maxGamesHosting;
    }

    // Games playing range filters
    if (params.minGamesPlaying !== undefined) {
      apiParams.minGamesPlaying = params.minGamesPlaying;
    }
    if (params.maxGamesPlaying !== undefined) {
      apiParams.maxGamesPlaying = params.maxGamesPlaying;
    }

    // Blogs hosting range filters
    if (params.minBlogsHosting !== undefined) {
      apiParams.minBlogsHosting = params.minBlogsHosting;
    }
    if (params.maxBlogsHosting !== undefined) {
      apiParams.maxBlogsHosting = params.maxBlogsHosting;
    }

    // Registration date range filters
    if (params.registeredFromUtc) {
      apiParams.registeredFromUtc = params.registeredFromUtc;
    }
    if (params.registeredToUtc) {
      apiParams.registeredToUtc = params.registeredToUtc;
    }

    // Sort mapping: frontend -> backend UserSort enum
    const sortMap: Record<string, string> = {
      username: "Name",
      rating: "Rating",
      lastActivity: "LastActivity",
      registered: "Registered",
      gamesHosting: "GamesHosting",
      gamesPlaying: "GamesPlaying",
      popularity: "Popularity",
      blogsHosting: "BlogsHosting",
    };
    if (params.sortBy && sortMap[params.sortBy]) {
      apiParams.sort = sortMap[params.sortBy];
    }

    // Sort order (asc/desc)
    if (params.sortOrder) {
      apiParams.sortOrder = params.sortOrder;
    }

    const { data, error } = await CommunityApi.getUsers(apiParams);

    searchLoading.value = false;

    if (error) {
      if (error.status === 403) {
        searchError.value = "Доступ запрещен";
      } else {
        searchError.value = "Ошибка загрузки данных";
      }
      return;
    }

    if (data) {
      searchResult.value = data;
      searchCache.set(cacheKey, { data, timestamp: now });
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

    // Map params to API params (same as searchUsers)
    const pageSize = params.size || 20;
    const apiParams: Record<string, unknown> = {
      take: pageSize,
    };

    // Convert page number to skip (number is 1-indexed page)
    if (page > 1) {
      apiParams.skip = (page - 1) * pageSize;
    }
    if (params.search) apiParams.q = params.search;
    if (params.activity) {
      const activityMap: Record<string, UserActivityFilter> = {
        active: UserActivityFilter.Active,
        inactive: UserActivityFilter.Inactive,
        all: UserActivityFilter.All,
      };
      apiParams.activity = activityMap[params.activity] ?? UserActivityFilter.Active;
    }
    if (params.role && params.role !== "all") apiParams.role = params.role;
    if (params.isOnline === true) apiParams.isOnline = true;
    if (params.isHonorary !== undefined) apiParams.isHonorary = params.isHonorary;
    if (params.isNewbie !== undefined) apiParams.isNewbie = params.isNewbie;
    if (params.minRating !== undefined) apiParams.minRating = params.minRating;
    if (params.maxRating !== undefined) apiParams.maxRating = params.maxRating;
    if (params.minGamesHosting !== undefined) apiParams.minGamesHosting = params.minGamesHosting;
    if (params.maxGamesHosting !== undefined) apiParams.maxGamesHosting = params.maxGamesHosting;
    if (params.minGamesPlaying !== undefined) apiParams.minGamesPlaying = params.minGamesPlaying;
    if (params.maxGamesPlaying !== undefined) apiParams.maxGamesPlaying = params.maxGamesPlaying;
    if (params.minBlogsHosting !== undefined) apiParams.minBlogsHosting = params.minBlogsHosting;
    if (params.maxBlogsHosting !== undefined) apiParams.maxBlogsHosting = params.maxBlogsHosting;
    if (params.registeredFromUtc) apiParams.registeredFromUtc = params.registeredFromUtc;
    if (params.registeredToUtc) apiParams.registeredToUtc = params.registeredToUtc;
    // Sort mapping: frontend -> backend UserSort enum
    const sortMap: Record<string, string> = {
      username: "Name",
      rating: "Rating",
      lastActivity: "LastActivity",
      registered: "Registered",
      gamesHosting: "GamesHosting",
      gamesPlaying: "GamesPlaying",
      popularity: "Popularity",
      blogsHosting: "BlogsHosting",
    };
    if (params.sortBy && sortMap[params.sortBy]) {
      apiParams.sort = sortMap[params.sortBy];
    }
    // Sort order (asc/desc)
    if (params.sortOrder) {
      apiParams.sortOrder = params.sortOrder;
    }

    const { data } = await CommunityApi.getUsers(apiParams);
    if (data) {
      searchCache.set(cacheKey, { data, timestamp: Date.now() });
    }
  }

  const selectedUser = ref<User | null>(null);
  const loadingProfile = ref(false);

  async function trySelectProfile(username: Username) {
    loadingProfile.value = true;
    selectedUser.value = null;

    const { data, error } = await CommunityApi.getUser(username);
    loadingProfile.value = false;

    if (error) return false;

    selectedUser.value = data ?? null;
    return true;
  }

  const editableUser = ref<User | null>(null);

  async function fetchEditableUser(username: Username) {
    const { data } = await CommunityApi.getUserForUpdate(username);
    editableUser.value = data ?? null;
  }

  return {
    users,
    usersError,
    fetchUsers,
    selectedUser,
    loadingProfile,
    trySelectProfile,
    editableUser,
    fetchEditableUser,
    // New search API
    searchResult,
    searchLoading,
    searchError,
    searchUsers,
    prefetchPage,
    clearSearchCache,
  };
});

// Re-export filter enum for convenience
export { UserActivityFilter };
