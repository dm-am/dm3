import { defineStore } from "pinia";
import { ref } from "vue";
import type { Game } from "@/api/models/gaming";
import type { ListEnvelope, ApiResult } from "@/api/models/common";
import gamingApi from "@/api/requests/gamingApi";

type GamesFetcher = () => Promise<ApiResult<ListEnvelope<Game>>>;

const GAMES_CACHE_MS = 60_000; // 60 секунд

function createGamesList(fetcher: GamesFetcher) {
  const games = ref<Game[] | null>(null);
  let lastFetched = 0;

  /**
   * Загрузить список игр с кэшированием.
   * Stale-while-revalidate: если данные есть, возвращает сразу,
   * но обновляет в фоне если устарели.
   */
  async function fetch(force = false) {
    const now = Date.now();
    const age = now - lastFetched;
    const isStale = age > GAMES_CACHE_MS;

    // Если есть свежий кэш — ничего не делаем
    if (!force && games.value && !isStale) {
      return;
    }

    // Stale-while-revalidate: есть устаревшие данные — обновляем в фоне
    if (games.value && isStale && !force) {
      fetcher().then(({ data, error }) => {
        if (!error && data) {
          games.value = data.resources;
          lastFetched = Date.now();
        }
      });
      return;
    }

    // Нет данных или force — ждём загрузки
    const { data, error } = await fetcher();
    games.value = !error && data ? data.resources : [];
    lastFetched = now;
  }

  return { games, fetch };
}

export const useGamesStore = defineStore("games", () => {
  const own = createGamesList(() => gamingApi.getOwnGames());
  const moderation = createGamesList(() => gamingApi.getModerationGames());
  const active = createGamesList(() => gamingApi.getActiveGames());
  const requirement = createGamesList(() => gamingApi.getRequirementGames());
  const finished = createGamesList(() => gamingApi.getFinishedGames());
  const popular = createGamesList(() => gamingApi.getPopularGames());

  return {
    ownGames: own.games,
    moderationGames: moderation.games,
    activeGames: active.games,
    requirementGames: requirement.games,
    finishedGames: finished.games,
    popularGames: popular.games,
    fetchOwnGames: own.fetch,
    fetchModerationGames: moderation.fetch,
    fetchActiveGames: active.fetch,
    fetchRequirementGames: requirement.fetch,
    fetchFinishedGames: finished.fetch,
    fetchPopularGames: popular.fetch,
  };
});
