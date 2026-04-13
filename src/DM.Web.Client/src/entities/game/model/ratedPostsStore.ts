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

/** Cache TTL: 5 minutes (rated posts don't change often) */
const CACHE_TTL = 300_000;

/**
 * Get the start of the current calendar week (Monday 00:00:00 UTC).
 * Week runs Monday to Sunday.
 */
function getWeekStartUtc(): Date {
  const now = new Date();
  const dayOfWeek = now.getUTCDay();
  const daysSinceMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const monday = new Date(now);
  monday.setUTCDate(now.getUTCDate() - daysSinceMonday);
  monday.setUTCHours(0, 0, 0, 0);
  return monday;
}

/** Per-user cache entry for "лучший пост за все время". */
interface UserBestPostEntry {
  post: Post | null;
  fetchedAt: number;
  loading: boolean;
  loaded: boolean;
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
    if (!force && bestOfWeek.value !== null && now - lastFetchBest < CACHE_TTL) {
      return;
    }
    if (loadingBest.value) return;
    loadingBest.value = true;
    try {
      const weekStart = getWeekStartUtc();
      const response = await gameApi.getRatedPosts({
        sortBy: "rating",
        hasReviews: true,
        createdAfter: weekStart.toISOString(),
        take: 1,
      });
      bestOfWeek.value = response.data?.resources?.[0] ?? null;
      lastFetchBest = now;
    } finally {
      loadingBest.value = false;
      bestLoaded.value = true;
    }
  }

  async function fetchLatestRated(force = false) {
    const now = Date.now();
    if (!force && latestRated.value !== null && now - lastFetchLatest < CACHE_TTL) {
      return;
    }
    if (loadingLatest.value) return;
    loadingLatest.value = true;
    try {
      const response = await gameApi.getRatedPosts({
        sortBy: "lastreview",
        hasReviews: true,
        take: 1,
      });
      latestRated.value = response.data?.resources?.[0] ?? null;
      lastFetchLatest = now;
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
    });

    try {
      const response = await gameApi.getRatedPosts({
        sortBy: "rating",
        sortOrder: "desc",
        authorUsernames: username,
        take: 1,
      });
      userBestPosts.value.set(username, {
        post: response.data?.resources?.[0] ?? null,
        fetchedAt: Date.now(),
        loading: false,
        loaded: true,
      });
    } catch {
      // Leave the entry marked as loaded-but-null so the empty state shows
      // rather than an infinite skeleton. Swallow the error for parity
      // with the fetchBestOfWeek / fetchLatestRated paths (they don't
      // rethrow either — the UI simply shows the fallback empty state).
      userBestPosts.value.set(username, {
        post: null,
        fetchedAt: Date.now(),
        loading: false,
        loaded: true,
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

  return {
    bestOfWeek,
    latestRated,
    loadingBest,
    loadingLatest,
    bestLoaded,
    latestLoaded,
    fetchBestOfWeek,
    fetchLatestRated,
    fetchBestPostOfUser,
    bestPostOfUser,
    isLoadingBestPostOf,
    isLoadedBestPostOf,
  };
});
