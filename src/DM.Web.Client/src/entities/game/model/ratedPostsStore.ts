// Rated posts store — cache for rated post widgets:
//   - "лучший пост недели" (homepage BestWeeklyPost)
//   - "последний оцененный пост" (homepage LatestRatedPost)
//   - "лучший пост за все время" per user (profile ProfileBestPost)
//
// All three call the same generic gameApi.getRatedPosts endpoint with
// different sort/filter/author params. The store caches each result so
// navigating back to the home page or profile doesn't refetch.
//
// `post.room.game` already arrives as a full sidebar-tier GameRef
// (master, assistants, activeCharacters, recruitment, subscribers)
// hydrated by PostRepository.GetRated's batched EnrichGamesAsync step,
// so GameLink / RoomLink render tooltips directly without a second
// network round-trip.

import { defineStore } from "pinia";
import { ref } from "vue";
import type { Post } from "./types";
import gameApi from "../api/gameApi";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";

/** Cache TTL: 5 minutes (rated posts don't change often) */
const CACHE_TTL = 300_000;

/** Per-user cache entry for "лучший пост за все время". */
interface UserBestPostEntry {
  post: Post | null;
  fetchedAt: number;
  loading: boolean;
  loaded: boolean;
  /** Russian error message, or null when the last fetch succeeded. */
  error: string | null;
}

export const useRatedPostsStore = defineStore("ratedPosts", () => {
  const bestOfWeek = ref<Post | null>(null);
  const latestRated = ref<Post | null>(null);
  const loadingBest = ref(false);
  const loadingLatest = ref(false);
  // Separate loaded flags so LatestRatedPost does not flip
  // to its empty state the moment BestWeeklyPost finishes.
  const bestLoaded = ref(false);
  const latestLoaded = ref(false);
  // Russian error messages so the widgets can render an error state
  // instead of a fake "no posts" empty state when the API fails.
  const bestError = ref<string | null>(null);
  const latestError = ref<string | null>(null);

  /**
   * Per-username cache for "лучший пост за все время". Reactive map so
   * getters below pick up mutations. Use .set + new entry to preserve
   * reactivity (direct property assignment on a plain map is not tracked).
   */
  const userBestPosts = ref<Map<string, UserBestPostEntry>>(new Map());

  let lastFetchBest = 0;
  let lastFetchLatest = 0;

  async function fetchBestOfWeek(force = false) {
    const now = Date.now();
    if (
      !force &&
      bestOfWeek.value !== null &&
      now - lastFetchBest < CACHE_TTL
    ) {
      return;
    }
    if (loadingBest.value) return;
    loadingBest.value = true;
    bestError.value = null;
    try {
      const response = await gameApi.getRatedPosts({
        sortBy: "rating",
        hasReviews: true,
        createdAfter: getWeekStartUtc(),
        take: 1,
      });
      if (response.error) {
        // Keep any stale post visible; the widget shows the error
        // text only when it has no post to render.
        bestError.value = "Не удалось загрузить лучший пост недели";
      } else {
        bestOfWeek.value = response.data?.resources?.[0] ?? null;
        lastFetchBest = now;
      }
    } finally {
      loadingBest.value = false;
      bestLoaded.value = true;
    }
  }

  /**
   * Fetch the latest rated post.
   *
   * @param excludeId - When set, fetches the top 2 results (instead of 1)
   *   and picks the first one whose id differs from `excludeId`. Used by
   *   the homepage to avoid showing the same post as BestWeeklyPost twice
   *   when they'd otherwise coincide. Backward compatible: omitting the
   *   param keeps the original take:1 behavior.
   * @param force - Bypass the cache TTL and refetch. Kept as a trailing
   *   param (rather than dropped) so no existing call site breaks.
   */
  async function fetchLatestRated(excludeId?: string, force = false) {
    const now = Date.now();
    if (
      !force &&
      latestRated.value !== null &&
      now - lastFetchLatest < CACHE_TTL
    ) {
      return;
    }
    if (loadingLatest.value) return;
    loadingLatest.value = true;
    latestError.value = null;
    try {
      const response = await gameApi.getRatedPosts({
        sortBy: "lastreview",
        hasReviews: true,
        take: excludeId ? 2 : 1,
      });
      if (response.error) {
        // Keep any stale post visible; the widget shows the error
        // text only when it has no post to render.
        latestError.value = "Не удалось загрузить последний оцененный пост";
      } else {
        const resources = response.data?.resources ?? [];
        latestRated.value = excludeId
          ? (resources.find((p) => p.id !== excludeId) ?? resources[0] ?? null)
          : (resources[0] ?? null);
        lastFetchLatest = now;
      }
    } finally {
      loadingLatest.value = false;
      latestLoaded.value = true;
    }
  }

  /**
   * Fetch the highest-rated post of a specific user, across all time.
   * Replaces the legacy /users/{name}/best-post endpoint by reusing the
   * generic rated-posts filter (author + rating sort + take=1).
   */
  async function fetchBestPostOfUser(username: string, force = false) {
    const now = Date.now();
    const existing = userBestPosts.value.get(username);
    if (
      !force &&
      existing &&
      !existing.loading &&
      now - existing.fetchedAt < CACHE_TTL
    ) {
      return;
    }
    if (existing?.loading) return;

    userBestPosts.value.set(username, {
      post: existing?.post ?? null,
      fetchedAt: existing?.fetchedAt ?? 0,
      loading: true,
      loaded: existing?.loaded ?? false,
      error: null,
    });

    try {
      const response = await gameApi.getRatedPosts({
        sortBy: "rating",
        sortOrder: "desc",
        authorUsernames: username,
        take: 1,
      });
      if (response.error) {
        // Keep the entry loaded-but-null so consumers can distinguish
        // "failed" (error set) from "empty" (error null, post null).
        userBestPosts.value.set(username, {
          post: existing?.post ?? null,
          fetchedAt: Date.now(),
          loading: false,
          loaded: true,
          error: "Не удалось загрузить лучший пост пользователя",
        });
        return;
      }
      userBestPosts.value.set(username, {
        post: response.data?.resources?.[0] ?? null,
        fetchedAt: Date.now(),
        loading: false,
        loaded: true,
        error: null,
      });
    } catch {
      // Leave the entry marked as loaded-but-null with an error so callers
      // can distinguish failure from a genuinely empty result.
      userBestPosts.value.set(username, {
        post: null,
        fetchedAt: Date.now(),
        loading: false,
        loaded: true,
        error: "Не удалось загрузить лучший пост пользователя",
      });
    }
  }

  function bestPostOfUser(username: string): Post | null {
    return userBestPosts.value.get(username)?.post ?? null;
  }

  function isLoadingBestPostOf(username: string): boolean {
    return userBestPosts.value.get(username)?.loading ?? false;
  }

  function isLoadedBestPostOf(username: string): boolean {
    return userBestPosts.value.get(username)?.loaded ?? false;
  }

  /** Error message for the last fetchBestPostOfUser call, or null. */
  function bestPostErrorOf(username: string): string | null {
    return userBestPosts.value.get(username)?.error ?? null;
  }

  return {
    bestOfWeek,
    latestRated,
    loadingBest,
    loadingLatest,
    bestLoaded,
    latestLoaded,
    bestError,
    latestError,
    fetchBestOfWeek,
    fetchLatestRated,
    fetchBestPostOfUser,
    bestPostOfUser,
    isLoadingBestPostOf,
    isLoadedBestPostOf,
    bestPostErrorOf,
  };
});
