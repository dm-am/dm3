// The rated-post widgets of the home page and the profile, cached so a way back does not refetch.

import { defineStore } from "pinia";
import { computed, ref, type Ref } from "vue";
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
    // The best of the week plus one entry per profile visited in five minutes.
    maxEntries: 32,
  });
  // The latest block keeps both candidates, so it holds a page and not a post.
  const latestCache = createKeyedCache<Post[]>({
    ttlMs: RATED_POST_TTL,
    maxEntries: 1,
  });

  const best = createBestOfWeek(postCache);
  const latest = createLatestRated(
    latestCache,
    best.bestOfWeek,
    best.bestSettled,
    best.loadingBest,
  );

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
  // Only a real answer counts: after a refusal the retry shows the skeleton,
  // not "no posts".
  const bestLoaded = ref(false);
  // An answer or a refusal, which is all the latest block waits for.
  const bestSettled = ref(false);
  const bestError = ref<string | null>(null);

  async function fetchBestOfWeek(force = false) {
    if (!force) {
      // A week with no rated post is an answer too, and cached like any other.
      const cached = postCache.get(bestOfWeekKey);
      if (cached !== undefined) {
        bestOfWeek.value = cached;
        bestLoaded.value = true;
        bestSettled.value = true;
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
        bestLoaded.value = false;
      } else {
        const post = response.data?.resources[0] ?? null;
        bestOfWeek.value = post;
        postCache.set(bestOfWeekKey, post);
        bestLoaded.value = true;
      }
    } finally {
      loadingBest.value = false;
      bestSettled.value = true;
    }
  }

  return {
    bestOfWeek,
    loadingBest,
    bestLoaded,
    bestSettled,
    bestError,
    fetchBestOfWeek,
  };
}

const latestRatedKey = "latest-rated";

/**
 * The home page's second block, which must not repeat the first. Its request
 * does not wait for the best of the week: it always takes two candidates, and
 * the post is picked out of them once both answers are in.
 */
function createLatestRated(
  latestCache: KeyedCache<Post[]>,
  bestOfWeek: Ref<Post | null>,
  bestSettled: Ref<boolean>,
  loadingBest: Ref<boolean>,
) {
  const candidates = ref<Post[]>([]);
  const loadingLatest = ref(false);
  // A real answer, so a retry after a refusal shows the skeleton, not "no posts".
  const candidatesLoaded = ref(false);
  const candidatesSettled = ref(false);
  const latestError = ref<string | null>(null);

  // Nothing is picked until the best of the week is in too: picked earlier, the
  // block could show the best post and then swap it for the next one.
  const bothSettled = computed(
    () => candidatesSettled.value && bestSettled.value,
  );
  const latestLoaded = computed(
    () => candidatesLoaded.value && bestSettled.value,
  );

  // Picked once per pair of answers: while either request is out the block
  // keeps what it shows, a post or nothing, or a refresh picks against the
  // stale best and then again.
  const latestRated = computed<Post | null>((previous) => {
    if (!bothSettled.value) return null;
    if (loadingLatest.value || loadingBest.value) return previous ?? null;
    const bestId = bestOfWeek.value?.id;
    return (
      candidates.value.find((p) => p.id !== bestId) ??
      candidates.value[0] ??
      null
    );
  });

  async function fetchLatestRated(force = false) {
    if (!force) {
      const cached = latestCache.get(latestRatedKey);
      if (cached !== undefined) {
        candidates.value = cached;
        candidatesLoaded.value = true;
        candidatesSettled.value = true;
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
        take: 2,
      });
      if (response.error) {
        latestError.value = "Не удалось загрузить последний оцененный пост";
        candidatesLoaded.value = false;
      } else {
        const resources = response.data?.resources ?? [];
        candidates.value = resources;
        latestCache.set(latestRatedKey, resources);
        candidatesLoaded.value = true;
      }
    } finally {
      loadingLatest.value = false;
      candidatesSettled.value = true;
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

  if (existing?.loading) return;
  const cached = force ? undefined : postCache.get(key);
  if (cached !== undefined) {
    userBestPosts.value.set(username, {
      post: cached,
      loading: false,
      loaded: true,
      error: null,
    });
    return;
  }

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
    // Not cached: the retry button exists to ask again. Not loaded either, so
    // the retry shows the skeleton and not "no posts".
    userBestPosts.value.set(username, {
      post: existing?.post ?? null,
      loading: false,
      loaded: false,
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
