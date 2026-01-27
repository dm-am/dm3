import { defineStore } from "pinia";
import { computed } from "vue";
import type { Game } from "@/api/models/gaming";
import type { ListEnvelope } from "@/api/models/common";
import gamingApi from "@/api/requests/gamingApi";
import { useApiList, useApiResource } from "@/composables/useApiResource";

export const useGamesStore = defineStore("games", () => {
  // Menu/sidebar lists - just need the resources array
  const own = useApiList<Game>(() => gamingApi.getOwnGames());
  const moderation = useApiList<Game>(() => gamingApi.getModerationGames());
  const popular = useApiList<Game>(() => gamingApi.getPopularGames());

  // Page lists - need full envelope with paging
  const activePage = useApiResource<ListEnvelope<Game>>(
    () => gamingApi.getActiveGames(),
    { cacheMs: 30_000 },
  );
  const recruitingPage = useApiResource<ListEnvelope<Game>>(
    () => gamingApi.getRecruitingGames(),
    { cacheMs: 30_000 },
  );
  const finishedPage = useApiResource<ListEnvelope<Game>>(
    () => gamingApi.getFinishedGames(),
    { cacheMs: 30_000 },
  );
  const moderationPage = useApiResource<ListEnvelope<Game>>(
    () => gamingApi.getModerationGames(),
    { cacheMs: 30_000 },
  );

  // Computed simple arrays for menu (backwards compatibility)
  const activeGames = computed(() => activePage.data.value?.resources ?? null);
  const recruitingGames = computed(
    () => recruitingPage.data.value?.resources ?? null,
  );
  const finishedGames = computed(
    () => finishedPage.data.value?.resources ?? null,
  );

  return {
    // Menu/sidebar games data (simple arrays)
    ownGames: own.data,
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
    ownGamesError: own.error,
    moderationGamesError: moderation.error,
    activeGamesError: activePage.error,

    // Loading states
    ownGamesLoading: own.loading,
    moderationGamesLoading: moderation.loading,
    activeGamesLoading: activePage.loading,
    recruitingGamesLoading: recruitingPage.loading,
    finishedGamesLoading: finishedPage.loading,
    moderationGamesPageLoading: moderationPage.loading,

    // Fetch functions
    fetchOwnGames: own.fetch,
    fetchModerationGames: moderation.fetch,
    fetchModerationGamesPage: moderationPage.fetch,
    fetchPopularGames: popular.fetch,
    fetchActiveGames: activePage.fetch,
    fetchRecruitingGames: recruitingPage.fetch,
    fetchFinishedGames: finishedPage.fetch,

    // Reset functions (for logout)
    resetOwnGames: own.reset,
    resetModerationGames: moderation.reset,
    resetAllGames: () => {
      own.reset();
      moderation.reset();
      popular.reset();
      activePage.reset();
      recruitingPage.reset();
      finishedPage.reset();
    },
  };
});
