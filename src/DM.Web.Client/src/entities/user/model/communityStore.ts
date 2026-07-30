// Community store

import { defineStore } from "pinia";
import { ref } from "vue";
import type { GeneralError, ListEnvelope } from "@/shared/api/models/common";
import type { User, Username } from "./types";
import { UserActivityFilter } from "./types";
import { unwrapResource } from "@/shared/api";
import { userApi } from "../api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";

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

const searchCache = createKeyedCache<ListEnvelope<User>>({ ttlMs: 30_000 });

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

  // Request guard to discard stale out-of-order responses
  const requestGuard = createRequestGuard();

  /**
   * Search users with caching (stale-while-revalidate)
   */
  async function searchUsers(params: UsersSearchParams): Promise<void> {
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

    const { data, error } = await userApi.getUsers(buildApiParams(params));

    // Ignore stale responses
    if (!requestGuard.isCurrent(requestId)) {
      return;
    }

    searchLoading.value = false;

    if (error) {
      searchError.value = "Не удалось загрузить пользователей";
      return;
    }

    if (data) {
      searchResult.value = data;
      searchCache.set(cacheKey, data);
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

    const { data } = await userApi.getUsers(buildApiParams(params));
    if (data) {
      searchCache.set(cacheKey, data);
    }
  }

  const selectedUser = ref<User | null>(null);
  const loadingProfile = ref(false);

  /**
   * Loads the profile. Returns the error instead of a boolean: the caller
   * needs the status to tell "no such user" from "server is down", and a
   * boolean forced the profile page into a second request for exactly that.
   */
  async function trySelectProfile(
    username: Username,
  ): Promise<GeneralError | null> {
    loadingProfile.value = true;
    selectedUser.value = null;

    // Profile page needs the rich UserProfile DTO (status, name, gender,
    // birthday, location, contacts, info, mediumUrl picture) — not the
    // truncated User DTO from /v1/users/{username} which is meant for lists.
    const { data, error } = await userApi.getUserProfile(username);
    loadingProfile.value = false;

    if (error) return error;

    // Backend returns a `{ resource: UserProfile }` envelope. The API client
    // doesn't unwrap automatically (typed lie), so we extract here. Fall
    // through to `data` if the response is already unwrapped (defensive
    // against API shape divergence between endpoints). UserProfile is a
    // structural superset of User, so the User read is safe even when
    // /profile returns the richer DTO.
    selectedUser.value = unwrapResource<User>(data);
    return null;
  }

  return {
    selectedUser,
    loadingProfile,
    trySelectProfile,
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
