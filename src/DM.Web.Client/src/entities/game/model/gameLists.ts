import { computed, ref, type Ref } from "vue";
import type { Game, GameRef, Tag } from "./gameTypes";
import type { ListEnvelope } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import gameCatalogApi from "../api/gameCatalogApi";
import { SIDEBAR_GAME_ROWS } from "./sidebarRows";
import {
  useApiList,
  useApiResource,
} from "@/shared/lib/composables/useApiResource";

function gameListSources() {
  // How many rows the column's lists ask for: the block's own, or more when a
  // block above leaves some of them out.
  const takes = {
    active: ref<number>(SIDEBAR_GAME_ROWS.active),
    recruiting: ref<number>(SIDEBAR_GAME_ROWS.recruiting),
    finished: ref<number>(SIDEBAR_GAME_ROWS.finished),
  };
  return {
    takes,
    // The games the viewer takes part in, in any role.
    participating: useApiList<GameRef>(() =>
      gameCatalogApi.getParticipatingGames(),
    ),
    moderation: useApiList<Game>(() => gameCatalogApi.getModerationGames()),
    popular: useApiList<GameRef>(() => gameCatalogApi.getPopularGames()),

    // A forced fetch asks the origin, past the browser's five-minute copy.
    tags: useApiList<Tag>((fresh) => gameApi.getTags(fresh), {
      cacheMs: 300_000,
    }),

    activePage: useApiResource<ListEnvelope<GameRef>>(
      () => gameCatalogApi.getActiveGames(takes.active.value),
      { cacheMs: 30_000 },
    ),
    recruitingPage: useApiResource<ListEnvelope<GameRef>>(
      () => gameCatalogApi.getRecruitingGames(takes.recruiting.value),
      { cacheMs: 30_000 },
    ),
    finishedPage: useApiResource<ListEnvelope<GameRef>>(
      () => gameCatalogApi.getFinishedGames(takes.finished.value),
      { cacheMs: 30_000 },
    ),
    moderationPage: useApiResource<ListEnvelope<Game>>(
      () => gameCatalogApi.getModerationGames(),
      { cacheMs: 30_000 },
    ),
  };
}

type GameListSources = ReturnType<typeof gameListSources>;

/** A read of a sidebar list asking for more rows: the block above left some out. */
const widen =
  (take: Ref<number>, page: { fetch: (force?: boolean) => Promise<void> }) =>
  (rows: number) => {
    take.value = rows;
    return page.fetch(true);
  };

export function createGameLists() {
  const sources = gameListSources();
  const { participating, moderation, popular } = sources;
  const { activePage, recruitingPage, finishedPage } = sources;

  function invalidateLists() {
    return Promise.all([
      participating.invalidate(),
      moderation.invalidate(),
      popular.invalidate(),
      activePage.invalidate(),
      recruitingPage.invalidate(),
      finishedPage.invalidate(),
    ]);
  }

  return { lists: listsOf(sources), invalidateLists };
}

function listsOf({
  takes,
  participating,
  moderation,
  popular,
  tags,
  activePage,
  recruitingPage,
  finishedPage,
  moderationPage,
}: GameListSources) {
  // The sidebar's plain lists and the pages' envelopes come off one fetch.
  const activeGames = computed(() => activePage.data.value?.resources ?? null);
  const recruitingGames = computed(
    () => recruitingPage.data.value?.resources ?? null,
  );
  const finishedGames = computed(
    () => finishedPage.data.value?.resources ?? null,
  );

  return {
    participatingGames: participating.data,
    moderationGames: moderation.data,
    popularGames: popular.data,
    activeGames,
    recruitingGames,
    finishedGames,

    activeGamesPage: activePage.data,
    recruitingGamesPage: recruitingPage.data,
    finishedGamesPage: finishedPage.data,
    moderationGamesPage: moderationPage.data,

    participatingGamesError: participating.error,
    moderationGamesError: moderation.error,
    activeGamesError: activePage.error,
    popularGamesError: popular.error,
    recruitingGamesError: recruitingPage.error,
    finishedGamesError: finishedPage.error,

    participatingGamesLoading: participating.loading,
    moderationGamesLoading: moderation.loading,
    activeGamesLoading: activePage.loading,
    recruitingGamesLoading: recruitingPage.loading,
    finishedGamesLoading: finishedPage.loading,
    moderationGamesPageLoading: moderationPage.loading,

    tags: tags.data,
    tagsLoading: tags.loading,
    tagsError: tags.error,
    fetchTags: tags.fetch,

    fetchParticipatingGames: participating.fetch,
    fetchModerationGames: moderation.fetch,
    fetchModerationGamesPage: moderationPage.fetch,
    fetchPopularGames: popular.fetch,
    fetchActiveGames: activePage.fetch,
    fetchRecruitingGames: recruitingPage.fetch,
    fetchFinishedGames: finishedPage.fetch,

    // The rows asked for, and a read asking for more of them.
    activeGamesTake: takes.active,
    recruitingGamesTake: takes.recruiting,
    finishedGamesTake: takes.finished,
    widenActiveGames: widen(takes.active, activePage),
    widenRecruitingGames: widen(takes.recruiting, recruitingPage),
    widenFinishedGames: widen(takes.finished, finishedPage),

    // For logout.
    resetParticipatingGames: participating.reset,
    resetModerationGames: moderation.reset,
  };
}
