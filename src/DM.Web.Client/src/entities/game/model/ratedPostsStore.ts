// The rated-post widgets of the home page and the profile, cached so a way back does not refetch.

import { defineStore } from "pinia";
import { ref, type Ref } from "vue";
import type { Post } from "./postTypes";
import postApi from "../api/postApi";
import { getWeekStartUtc } from "@/shared/lib/utils/datetime";
import {
  createKeyedCache,
  type KeyedCache,
} from "@/shared/lib/utils/keyedCache";

// Literal keys: the best-of-week query carries a timestamp taken at call time.
const RATED_POST_TTL = 300_000;

export const useRatedPostsStore = defineStore("ratedPosts", () => {
  const postCache = createKeyedCache<Post | null>({
    ttlMs: RATED_POST_TTL,
    // Two singletons plus one entry per profile visited in five minutes.
    maxEntries: 32,
  });

  const best = createBestOfWeek(postCache);
  const latest = createLatestRated(postCache);

  return {
    bestOfWeek: best.bestOfWeek,
    latestRated: latest.latestRated,
    loadingBest: best.loadingBest,
    loadingLatest: latest.loadingLatest,
    bestLoaded: best.bestLoaded,
    latestLoaded: latest.latestLoaded,
    bestError: best.bestError,
    latestError: latest.latestError,
    fetchBestOfWeek: best.fetchBestOfWeek,
    fetchLatestRated: latest.fetchLatestRated,
    ...createUserBestPost(postCache),
  };
});

const bestOfWeekKey = "best-of-week";

function createBestOfWeek(postCache: KeyedCache<Post | null>) {
  const bestOfWeek = ref<Post | null>(null);
  const loadingBest = ref(false);
  // Apart from latestLoaded, so the other widget does not flip to empty early.
  const bestLoaded = ref(false);
  const bestError = ref<string | null>(null);

  async function fetchBestOfWeek(force = false) {
    if (!force) {
      // A week with no rated post is an answer too, and cached like any other.
      const cached = postCache.get(bestOfWeekKey);
      if (cached !== undefined) {
        bestOfWeek.value = cached;
        bestLoaded.value = true;
        return;
      }
    }
    if (loadingBest.value) return;
    loadingBest.value = true;
    bestError.value = null;
    try {
      const response = await postApi.getRatedPosts({
        sortBy: "rating",
        hasReviews: true,
        createdFromUtc: getWeekStartUtc(),
        take: 1,
      });
      if (response.error) {
        // The post already shown stays.
        bestError.value = "Не удалось загрузить лучший пост недели";
      } else {
        const post = response.data?.resources[0] ?? null;
        bestOfWeek.value = post;
        postCache.set(bestOfWeekKey, post);
      }
    } finally {
      loadingBest.value = false;
      bestLoaded.value = true;
    }
  }

  return { bestOfWeek, loadingBest, bestLoaded, bestError, fetchBestOfWeek };
}

// The excluded post is part of the key: two exclusions are two answers.
const latestRatedKey = (excludeId?: string) =>
  `latest-rated:${excludeId ?? ""}`;

function createLatestRated(postCache: KeyedCache<Post | null>) {
  const latestRated = ref<Post | null>(null);
  const loadingLatest = ref(false);
  const latestLoaded = ref(false);
  const latestError = ref<string | null>(null);

  /** @param excludeId The best post of the week, so the home page does not show it twice. */
  async function fetchLatestRated(excludeId?: string, force = false) {
    const key = latestRatedKey(excludeId);
    if (!force) {
      const cached = postCache.get(key);
      if (cached !== undefined) {
        latestRated.value = cached;
        latestLoaded.value = true;
        return;
      }
    }
    if (loadingLatest.value) return;
    loadingLatest.value = true;
    latestError.value = null;
    try {
      const response = await postApi.getRatedPosts({
        sortBy: "lastreview",
        hasReviews: true,
        take: excludeId ? 2 : 1,
      });
      if (response.error) {
        latestError.value = "Не удалось загрузить последний оцененный пост";
      } else {
        const resources = response.data?.resources ?? [];
        const post = excludeId
          ? (resources.find((p) => p.id !== excludeId) ?? resources[0] ?? null)
          : (resources[0] ?? null);
        latestRated.value = post;
        postCache.set(key, post);
      }
    } finally {
      loadingLatest.value = false;
      latestLoaded.value = true;
    }
  }

  return {
    latestRated,
    loadingLatest,
    latestLoaded,
    latestError,
    fetchLatestRated,
  };
}

const userBestKey = (username: string) => `user-best:${username}`;

interface UserBestPostEntry {
  post: Post | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
}

function createUserBestPost(postCache: KeyedCache<Post | null>) {
  // A new entry through .set, so the getters below see the change.
  const userBestPosts = ref<Map<string, UserBestPostEntry>>(new Map());

  function bestPostOfUser(username: string): Post | null {
    return userBestPosts.value.get(username)?.post ?? null;
  }

  function isLoadingBestPostOf(username: string): boolean {
    return userBestPosts.value.get(username)?.loading ?? false;
  }

  function isLoadedBestPostOf(username: string): boolean {
    return userBestPosts.value.get(username)?.loaded ?? false;
  }

  function bestPostErrorOf(username: string): string | null {
    return userBestPosts.value.get(username)?.error ?? null;
  }

  return {
    fetchBestPostOfUser: (username: string, force = false) =>
      fetchBestPostOfUser(userBestPosts, postCache, username, force),
    bestPostOfUser,
    isLoadingBestPostOf,
    isLoadedBestPostOf,
    bestPostErrorOf,
  };
}

async function fetchBestPostOfUser(
  userBestPosts: Ref<Map<string, UserBestPostEntry>>,
  postCache: KeyedCache<Post | null>,
  username: string,
  force: boolean,
) {
  const key = userBestKey(username);
  const existing = userBestPosts.value.get(username);

  if (!force && !existing?.loading) {
    const cached = postCache.get(key);
    if (cached !== undefined) {
      userBestPosts.value.set(username, {
        post: cached,
        loading: false,
        loaded: true,
        error: null,
      });
      return;
    }
  }
  if (existing?.loading) return;

  userBestPosts.value.set(username, {
    post: existing?.post ?? null,
    loading: true,
    loaded: existing?.loaded ?? false,
    error: null,
  });

  const response = await postApi.getRatedPosts({
    sortBy: "rating",
    sortOrder: "desc",
    authorUsernames: [username],
    take: 1,
  });
  if (response.error) {
    // Not cached: the retry button exists to ask again.
    userBestPosts.value.set(username, {
      post: existing?.post ?? null,
      loading: false,
      loaded: true,
      error: "Не удалось загрузить лучший пост пользователя",
    });
    return;
  }
  const post = response.data?.resources[0] ?? null;
  postCache.set(key, post);
  userBestPosts.value.set(username, {
    post,
    loading: false,
    loaded: true,
    error: null,
  });
}
