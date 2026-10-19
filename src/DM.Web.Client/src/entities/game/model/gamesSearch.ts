import { ref } from "vue";
import type { Game } from "./gameTypes";
import type { ListEnvelope } from "@/shared/api/models/common";
import gameCatalogApi, { type GamesSearchParams } from "../api/gameCatalogApi";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { describeFailureOrWait } from "@/shared/lib/errors";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

function searchState() {
  return {
    searchResult: ref<ListEnvelope<Game> | null>(null),
    searchLoading: ref(false),
    searchError: ref<string | null>(null),
    lastSearchParams: ref<GamesSearchParams | null>(null),

    searchCache: createKeyedCache<ListEnvelope<Game>>({ ttlMs: 30_000 }),

    requestGuard: createRequestGuard(),
  };
}

type GamesSearch = ReturnType<typeof searchState>;

export function createGamesSearch() {
  const state = searchState();
  const { searchResult, searchLoading, searchError, searchCache } = state;

  return {
    search: {
      searchResult,
      searchLoading,
      searchError,
      searchGames: (params: GamesSearchParams) => searchGames(state, params),

      prefetchPage: (page: number) => prefetchPage(state, page),
    },
    searchCache,
  };
}

async function searchGames(
  state: GamesSearch,
  params: GamesSearchParams,
): Promise<void> {
  const { searchResult, searchLoading, searchError, lastSearchParams } = state;
  const { searchCache, requestGuard } = state;
  const requestId = requestGuard.next();

  // Before the cache lookup, so an old error never survives a cache hit.
  searchError.value = null;

  const cacheKey = stableCacheKey(params);
  const fresh = searchCache.get(cacheKey);

  if (fresh) {
    searchResult.value = fresh;
    lastSearchParams.value = params;
    searchLoading.value = false;
    return;
  }

  const stale = searchCache.getStale(cacheKey);
  if (stale) {
    searchResult.value = stale;
  }

  searchLoading.value = true;
  lastSearchParams.value = params;

  // The table's strip says the refusal where the rows would be.
  const { data, error } = await gameCatalogApi.searchGames(
    params,
    OWNS_REFUSAL,
  );

  if (!requestGuard.isCurrent(requestId)) {
    return;
  }

  if (error) {
    searchError.value = describeFailureOrWait(
      error,
      "Не удалось загрузить игры",
    );
    if (!stale) {
      searchResult.value = null;
    }
  } else if (data) {
    searchResult.value = data;
    searchCache.set(cacheKey, data);
  }

  searchLoading.value = false;
}

async function prefetchPage(state: GamesSearch, page: number): Promise<void> {
  const { lastSearchParams, searchCache } = state;
  if (!lastSearchParams.value) return;

  const params = { ...lastSearchParams.value, number: page };
  const cacheKey = stableCacheKey(params);

  // Replacing a stale entry is the point of a prefetch.
  if (searchCache.get(cacheKey)) return;

  const { data } = await gameCatalogApi.searchGames(params);
  if (data) {
    searchCache.set(cacheKey, data);
  }
}
