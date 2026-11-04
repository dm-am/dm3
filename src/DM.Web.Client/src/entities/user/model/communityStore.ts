import { defineStore } from "pinia";
import { ref } from "vue";
import type { GeneralError, ListEnvelope } from "@/shared/api/models/common";
import type { User, Username, UserPicture } from "./types";
import { UserActivityFilter } from "./types";
import { unwrapResource } from "@/shared/api";
import { userApi } from "../api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { pageSkip } from "@/shared/api/pageSkip";

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

// Sent under their own names; a zero and a false are values.
const AS_IS = [
  "isNewbie",
  "minRating",
  "maxRating",
  "minGamesHosting",
  "maxGamesHosting",
  "minGamesPlaying",
  "maxGamesPlaying",
  "minBlogsHosting",
  "maxBlogsHosting",
] as const;

// One mapping for search and prefetch, which share the cache keys.
function buildApiParams(
  params: UsersSearchParams,
): Record<string, string | number | boolean | undefined> {
  const pageSize = params.size || 20;
  const pageNumber = params.number || 1;
  const apiParams: Record<string, string | number | boolean | undefined> = {
    take: pageSize,
  };

  apiParams.skip = pageSkip(pageNumber, pageSize);

  if (params.search) apiParams.search = params.search;

  if (params.activity) {
    apiParams.activity =
      ACTIVITY_MAP[params.activity] ?? UserActivityFilter.Active;
  }

  if (params.role && params.role !== "all") apiParams.role = params.role;
  if (params.isOnline === true) apiParams.isOnline = true;
  for (const key of AS_IS) {
    if (params[key] !== undefined) apiParams[key] = params[key];
  }
  if (params.registeredFromUtc)
    apiParams.registeredFromUtc = params.registeredFromUtc;
  if (params.registeredToUtc)
    apiParams.registeredToUtc = params.registeredToUtc;

  if (params.sortBy && SORT_MAP[params.sortBy]) {
    apiParams.sortBy = SORT_MAP[params.sortBy];
  }
  if (params.sortOrder) apiParams.sortOrder = params.sortOrder;

  return apiParams;
}

export const useCommunityStore = defineStore("community", () => {
  const searchResult = ref<ListEnvelope<User> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<UsersSearchParams | null>(null);

  // Inside the store, so a fresh pinia starts with an empty cache.
  const searchCache = createKeyedCache<ListEnvelope<User>>({ ttlMs: 30_000 });

  const requestGuard = createRequestGuard();

  async function searchUsers(params: UsersSearchParams): Promise<void> {
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

    const { data, error } = await userApi.getUsers(buildApiParams(params));

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

  function clearSearchCache(): void {
    searchCache.clear();
  }

  async function prefetchPage(page: number): Promise<void> {
    if (!lastSearchParams.value) return;

    const params = { ...lastSearchParams.value, number: page };
    const cacheKey = stableCacheKey(params);

    // Replacing a stale entry is the point of a prefetch.
    if (searchCache.get(cacheKey)) return;

    const { data } = await userApi.getUsers(buildApiParams(params));
    if (data) {
      searchCache.set(cacheKey, data);
    }
  }

  const selectedUser = ref<User | null>(null);
  const loadingProfile = ref(false);

  // The answer for the name the reader already left must not land on the newer one.
  const profileGuard = createRequestGuard();

  /** The error, so the caller tells "no such user" from "server is down". */
  async function trySelectProfile(
    username: Username,
  ): Promise<GeneralError | null> {
    const requestId = profileGuard.next();
    loadingProfile.value = true;
    selectedUser.value = null;

    // The profile, not the short user of the lists.
    const { data, error } = await userApi.getUserProfile(username);

    if (!profileGuard.isCurrent(requestId)) return null;

    loadingProfile.value = false;

    if (error) return error;

    selectedUser.value = unwrapResource<User>(data);
    return null;
  }

  // By id: an upload can outlive the profile it started on.
  function applySelectedUserPicture(
    userId: string,
    picture: UserPicture,
  ): void {
    const current = selectedUser.value;
    if (!current || current.id !== userId) return;
    selectedUser.value = { ...current, picture };
  }

  return {
    selectedUser,
    loadingProfile,
    trySelectProfile,
    applySelectedUserPicture,
    searchResult,
    searchLoading,
    searchError,
    searchUsers,
    prefetchPage,
    clearSearchCache,
  };
});
