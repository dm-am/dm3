// Community store
// Migrated from shared/stores/community.ts

import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { User, Username } from "./types";
import { UserActivityFilter } from "./types";
import { CommunityApi } from "@/shared/api";

/**
 * Search parameters for users query (frontend model).
 *
 * Canonical definition — lives in the entity layer so both the store and the
 * `user-filter` feature share a single type (FSD: feature imports from entity,
 * never the reverse).
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
const searchCache = new Map<
  string,
  { data: ListEnvelope<User>; timestamp: number }
>();

/**
 * Stable cache key covering every filter param. Also reused by the widget to
 * trigger refetches, so it is exported as the single source of truth.
 */
export function createCacheKey(params: UsersSearchParams): string {
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

const SORT_MAP: Record<string, string> = {
  username: "Name",
  rating: "Rating",
  lastActivity: "LastActivity",
  registered: "Registered",
  gamesHosting: "GamesHosting",
  gamesPlaying: "GamesPlaying",
  popularity: "Popularity",
  blogsHosting: "BlogsHosting",
};

const ACTIVITY_MAP: Record<string, UserActivityFilter> = {
  active: UserActivityFilter.Active,
  inactive: UserActivityFilter.Inactive,
  all: UserActivityFilter.All,
};

/**
 * Map frontend search params to backend API query params. Single source of
 * truth used by both searchUsers and prefetchPage so the forwarded param set
 * never drifts between them.
 */
function buildApiParams(
  params: UsersSearchParams,
): Record<string, string | number | boolean | undefined> {
  const pageSize = params.size || 20;
  const pageNumber = params.number || 1;
  const apiParams: Record<string, string | number | boolean | undefined> = {
    take: pageSize,
  };

  // Convert 1-indexed page number to skip
  if (pageNumber > 1) {
    apiParams.skip = (pageNumber - 1) * pageSize;
  }

  if (params.search) apiParams.q = params.search;

  if (params.activity) {
    apiParams.activity =
      ACTIVITY_MAP[params.activity] ?? UserActivityFilter.Active;
  }

  if (params.role && params.role !== "all") apiParams.role = params.role;
  if (params.isOnline === true) apiParams.isOnline = true;
  if (params.isHonorary !== undefined) apiParams.isHonorary = params.isHonorary;
  if (params.isNewbie !== undefined) apiParams.isNewbie = params.isNewbie;
  if (params.minRating !== undefined) apiParams.minRating = params.minRating;
  if (params.maxRating !== undefined) apiParams.maxRating = params.maxRating;
  if (params.minGamesHosting !== undefined)
    apiParams.minGamesHosting = params.minGamesHosting;
  if (params.maxGamesHosting !== undefined)
    apiParams.maxGamesHosting = params.maxGamesHosting;
  if (params.minGamesPlaying !== undefined)
    apiParams.minGamesPlaying = params.minGamesPlaying;
  if (params.maxGamesPlaying !== undefined)
    apiParams.maxGamesPlaying = params.maxGamesPlaying;
  if (params.minBlogsHosting !== undefined)
    apiParams.minBlogsHosting = params.minBlogsHosting;
  if (params.maxBlogsHosting !== undefined)
    apiParams.maxBlogsHosting = params.maxBlogsHosting;
  if (params.registeredFromUtc)
    apiParams.registeredFromUtc = params.registeredFromUtc;
  if (params.registeredToUtc)
    apiParams.registeredToUtc = params.registeredToUtc;

  if (params.sortBy && SORT_MAP[params.sortBy]) {
    apiParams.sort = SORT_MAP[params.sortBy];
  }
  if (params.sortOrder) apiParams.sortOrder = params.sortOrder;

  return apiParams;
}

export const useCommunityStore = defineStore("community", () => {
  // Search state
  const searchResult = ref<ListEnvelope<User> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<UsersSearchParams | null>(null);

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

    const { data, error } = await CommunityApi.getUsers(buildApiParams(params));

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

    const { data } = await CommunityApi.getUsers(buildApiParams(params));
    if (data) {
      searchCache.set(cacheKey, { data, timestamp: Date.now() });
    }
  }

  const selectedUser = ref<User | null>(null);
  const loadingProfile = ref(false);

  async function trySelectProfile(username: Username) {
    loadingProfile.value = true;
    selectedUser.value = null;

    // Profile page needs the rich UserProfile DTO (status, name, gender,
    // birthday, location, contacts, info, mediumUrl picture) — not the
    // truncated User DTO from /v1/users/{username} which is meant for lists.
    const { data, error } = await CommunityApi.getUserProfile(username);
    loadingProfile.value = false;

    if (error) return false;

    // Backend returns a `{ resource: UserProfile }` envelope. The API client
    // doesn't unwrap automatically (typed lie), so we extract here. Fall
    // through to `data` if the response is already unwrapped (defensive
    // against API shape divergence between endpoints).
    selectedUser.value = unwrapResource(data);
    return true;
  }

  const editableUser = ref<User | null>(null);

  async function fetchEditableUser(username: Username) {
    const { data } = await CommunityApi.getUserForUpdate(username);
    editableUser.value = unwrapResource(data);
  }

  function unwrapResource(payload: unknown): User | null {
    if (!payload || typeof payload !== "object") return null;
    // UserProfile is a structural superset of User, so the `as User` cast is
    // safe even when /profile returns the richer DTO.
    if ("resource" in payload) return (payload as { resource: User }).resource;
    return payload as User;
  }

  return {
    selectedUser,
    loadingProfile,
    trySelectProfile,
    editableUser,
    fetchEditableUser,
    // Search API
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
