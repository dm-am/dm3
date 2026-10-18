// Store for the game lists (menu, sidebar, search page).
//
// A single game and everything hanging off it lives in detailsStore.ts.

import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { Game, GameRef, Tag } from "./types";
import type { GeneralError, ListEnvelope } from "@/shared/api/models/common";
import gameApi, { type GamesSearchParams } from "../api/gameApi";
import {
  useApiList,
  useApiResource,
} from "@/shared/lib/composables/useApiResource";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { describeFailure, describeRateLimit } from "@/shared/lib/errors";

/**
 * The search takes its own refusals over.
 *
 * A failed search is shown by GamesDataTable as a strip standing where the rows
 * would be, so the failure is already on screen and in the place the reader is
 * looking; the interceptor's plate in the corner repeated it word for word on a
 * 403 and a 5xx. With these flags the interceptor stays quiet about 403, 429
 * and 5xx for this request, and the strip is the one carrier.
 *
 * An expired session and a dead connection are not in here on purpose: the
 * interceptor speaks for both, because neither is about the search — one is
 * about the reader's session, the other about the road to the site, and only
 * the interceptor can offer the other address. The strip beside them says a
 * different thing: that this list is empty.
 */
const SEARCH_OWNS_REFUSAL = {
  ownsRefusal: true,
  ownsRateLimit: true,
} as const;

/**
 * The one sentence the strip shows for a failed search.
 *
 * A wait is worded by the shared sentence rather than by the title the API sent
 * with it: that one is the only wording carrying the seconds the server asked
 * for, and it is the wording the interceptor would have raised, so taking the
 * refusal over costs the reader nothing. Everything else the API named is
 * relayed as it stands.
 */
function searchFailure(error: GeneralError, fallback: string): string {
  return error.status === 429
    ? describeRateLimit(error.retryAfterSeconds)
    : describeFailure(error, fallback);
}

/**
 * Store for game lists (menu/sidebar, pagination)
 */
export const useGamesStore = defineStore("games", () => {
  // Menu/sidebar lists - use lightweight GameRef for efficiency
  // participating = games where user is master, mentor, assistant, player, or reader
  const participating = useApiList<GameRef>(() =>
    gameApi.getParticipatingGames(),
  );
  const moderation = useApiList<Game>(() => gameApi.getModerationGames());
  const popular = useApiList<GameRef>(() => gameApi.getPopularGames());

  // Tags with long cache (5 minutes - tags change rarely). A forced fetch asks
  // the origin: the endpoint answers `public, max-age=300`, so dropping this
  // store's copy alone would be answered from the browser's.
  const tags = useApiList<Tag>((fresh) => gameApi.getTags(fresh), {
    cacheMs: 300_000,
  });

  // Sidebar lists - use lightweight GameRef
  const activePage = useApiResource<ListEnvelope<GameRef>>(
    () => gameApi.getActiveGames(),
    { cacheMs: 30_000 },
  );
  const recruitingPage = useApiResource<ListEnvelope<GameRef>>(
    () => gameApi.getRecruitingGames(),
    { cacheMs: 30_000 },
  );
  const finishedPage = useApiResource<ListEnvelope<GameRef>>(
    () => gameApi.getFinishedGames(),
    { cacheMs: 30_000 },
  );
  // Moderation page needs full Game with all details
  const moderationPage = useApiResource<ListEnvelope<Game>>(
    () => gameApi.getModerationGames(),
    { cacheMs: 30_000 },
  );

  // Search results with filters
  const searchResult = ref<ListEnvelope<Game> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<GamesSearchParams | null>(null);

  const searchCache = createKeyedCache<ListEnvelope<Game>>({ ttlMs: 30_000 });

  // Request guard to discard stale out-of-order responses
  const requestGuard = createRequestGuard();

  /**
   * Search games with filters (with caching)
   */
  async function searchGames(params: GamesSearchParams): Promise<void> {
    const requestId = requestGuard.next();

    // Reset error before the cache lookup so a stale error never survives
    // a cache-hit navigation.
    searchError.value = null;

    const cacheKey = stableCacheKey(params);
    const fresh = searchCache.get(cacheKey);

    if (fresh) {
      searchResult.value = fresh;
      lastSearchParams.value = params;
      searchLoading.value = false;
      return;
    }

    // Show stale while revalidating
    const stale = searchCache.getStale(cacheKey);
    if (stale) {
      searchResult.value = stale;
    }

    searchLoading.value = true;
    lastSearchParams.value = params;

    const { data, error } = await gameApi.searchGames(
      params,
      SEARCH_OWNS_REFUSAL,
    );

    // Ignore stale responses
    if (!requestGuard.isCurrent(requestId)) {
      return;
    }

    if (error) {
      searchError.value = searchFailure(error, "Не удалось загрузить игры");
      // Keep stale data on error if available
      if (!stale) {
        searchResult.value = null;
      }
    } else if (data) {
      searchResult.value = data;
      searchCache.set(cacheKey, data);
    }

    searchLoading.value = false;
  }

  /**
   * Load next page of search results
   */
  async function loadNextSearchPage(): Promise<void> {
    if (!lastSearchParams.value || !searchResult.value?.paging) return;

    const currentPage = searchResult.value.paging.current;
    const totalPages = searchResult.value.paging.pages;

    if (currentPage >= totalPages) return;

    await searchGames({
      ...lastSearchParams.value,
      number: currentPage + 1,
    });
  }

  /**
   * Prefetch a page in background (for next page optimization)
   * Does not update visible results, only warms the cache
   */
  async function prefetchPage(page: number): Promise<void> {
    if (!lastSearchParams.value) return;

    const params = { ...lastSearchParams.value, number: page };
    const cacheKey = stableCacheKey(params);

    // Skip if already cached and still fresh — a stale entry is worth replacing
    // here, since the point of the prefetch is that the next page is ready.
    if (searchCache.get(cacheKey)) return;

    // Fetch in background without updating UI
    const { data } = await gameApi.searchGames(params);
    if (data) {
      searchCache.set(cacheKey, data);
    }
  }

  /**
   * Reset search state
   */
  function resetSearch(): void {
    searchResult.value = null;
    searchLoading.value = false;
    searchError.value = null;
    lastSearchParams.value = null;
    searchCache.clear();
  }

  // The menu and the sidebar want a plain list; the pages below want the paged
  // envelope the same request returns. Both are served off one fetch.
  const activeGames = computed(() => activePage.data.value?.resources ?? null);
  const recruitingGames = computed(
    () => recruitingPage.data.value?.resources ?? null,
  );
  const finishedGames = computed(
    () => finishedPage.data.value?.resources ?? null,
  );

  return {
    // Menu/sidebar games data (simple arrays)
    // participatingGames = games where user has any role (master, mentor, assistant, player, reader)
    participatingGames: participating.data,
    moderationGames: moderation.data,
    popularGames: popular.data,
    activeGames,
    recruitingGames,
    finishedGames,

    // Page games data (with paging)
    activeGamesPage: activePage.data,
    recruitingGamesPage: recruitingPage.data,
    finishedGamesPage: finishedPage.data,
    moderationGamesPage: moderationPage.data,

    // Error states
    participatingGamesError: participating.error,
    moderationGamesError: moderation.error,
    activeGamesError: activePage.error,
    popularGamesError: popular.error,
    recruitingGamesError: recruitingPage.error,
    finishedGamesError: finishedPage.error,

    // Loading states
    participatingGamesLoading: participating.loading,
    moderationGamesLoading: moderation.loading,
    activeGamesLoading: activePage.loading,
    recruitingGamesLoading: recruitingPage.loading,
    finishedGamesLoading: finishedPage.loading,
    moderationGamesPageLoading: moderationPage.loading,

    // Tags
    tags: tags.data,
    tagsLoading: tags.loading,
    tagsError: tags.error,
    fetchTags: tags.fetch,

    // Fetch functions
    fetchParticipatingGames: participating.fetch,
    fetchModerationGames: moderation.fetch,
    fetchModerationGamesPage: moderationPage.fetch,
    fetchPopularGames: popular.fetch,
    fetchActiveGames: activePage.fetch,
    fetchRecruitingGames: recruitingPage.fetch,
    fetchFinishedGames: finishedPage.fetch,

    // Reset functions (for logout)
    resetParticipatingGames: participating.reset,
    resetModerationGames: moderation.reset,

    /**
     * After a mutation changed which games exist. Distinct from the resets
     * above, which blank the lists: that is right for logout and wrong here,
     * because the sidebar blocks fetch on mount and the shell mounts once per
     * session, so a blanked list stays blank until a reload.
     */
    invalidateGameLists: async () => {
      searchCache.clear();
      await Promise.all([
        participating.invalidate(),
        moderation.invalidate(),
        popular.invalidate(),
        activePage.invalidate(),
        recruitingPage.invalidate(),
        finishedPage.invalidate(),
      ]);
    },

    // Search with filters
    searchResult,
    searchLoading,
    searchError,
    searchGames,
    loadNextSearchPage,
    prefetchPage,
    resetSearch,
  };
});
