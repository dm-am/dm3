// Game store

import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type {
  Game,
  GameRef,
  Character,
  Room,
  Post,
  Tag,
  GameUser,
  CreateRoomInput,
  GameStatusTransition,
  GamePremoderationTransition,
} from "./types";
import { GameParticipation } from "./types";
import type {
  ListEnvelope,
  PagingInfo,
  Comment,
  User,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
import gameApi, { type GamesSearchParams } from "../api/gameApi";
import {
  useApiList,
  useApiResource,
} from "@/shared/lib/composables/useApiResource";
import { unwrapResource, type CommentsQuery } from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { describeFailure } from "@/shared/lib/errors";
import type { GeneralError } from "@/shared/api/models/common";
import { requestNotSent } from "@/shared/lib/errors";

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

  // Tags with long cache (5 minutes - tags change rarely)
  const tags = useApiList<Tag>(() => gameApi.getTags(), { cacheMs: 300_000 });

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

    const { data, error } = await gameApi.searchGames(params);

    // Ignore stale responses
    if (!requestGuard.isCurrent(requestId)) {
      return;
    }

    if (error) {
      searchError.value = describeFailure(error, "Не удалось загрузить игры");
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

/**
 * Store for single game details (game page)
 */
export const useGameDetailsStore = defineStore("gameDetails", () => {
  // Game data
  const game = ref<Game | null>(null);
  const gameLoading = ref(false);
  const gameError = ref<string | null>(null);
  /**
   * HTTP status of the refusal, kept alongside the sentence.
   *
   * The sentence alone made a deleted game, a private one and a fallen server
   * read the same. The status is what the shell needs to draw the page the
   * forum has drawn for its topics all along.
   */
  const gameErrorStatus = ref<number | null>(null);

  // Rooms data
  const rooms = ref<Room[]>([]);
  const roomsLoading = ref(false);
  const roomsError = ref<string | null>(null);

  // Current room and posts
  const currentRoom = ref<Room | null>(null);
  const posts = ref<Post[]>([]);
  const postsPaging = ref<PagingInfo | null>(null);
  const postsLoading = ref(false);
  const postsError = ref<string | null>(null);

  // Characters data
  const characters = ref<Character[]>([]);
  const charactersLoading = ref(false);
  const charactersError = ref<string | null>(null);

  // Comments data. The failure is a flag and not a sentence: the discussion
  // section spells one wording for a failed load, wherever it fails.
  const comments = ref<Comment[]>([]);
  const commentsPaging = ref<PagingInfo | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref(false);

  // Blacklist data
  const blacklist = ref<User[]>([]);
  const blacklistLoading = ref(false);
  const blacklistError = ref<string | null>(null);

  // Game users data
  const users = ref<GameUser[]>([]);
  const usersLoading = ref(false);
  const usersError = ref<string | null>(null);

  // One monotonic token per independent slice. This store is a single bag for
  // "the current game", so a reply for the game (or room, or page) the user has
  // already left would otherwise land on top of the newer one — no error, no
  // spinner, wrong data. Per slice rather than one shared counter because the
  // slices load in parallel and a shared counter would let every new request
  // cancel its siblings.
  const gameGuard = createRequestGuard();
  const roomsGuard = createRequestGuard();
  const postsGuard = createRequestGuard();
  const charactersGuard = createRequestGuard();
  const commentsGuard = createRequestGuard();
  const blacklistGuard = createRequestGuard();
  const usersGuard = createRequestGuard();
  const detailGuards = [
    gameGuard,
    roomsGuard,
    postsGuard,
    charactersGuard,
    commentsGuard,
    blacklistGuard,
    usersGuard,
  ];

  // Active vs archived post rooms — a room is archived when isArchived is true.
  const activeRooms = computed(() => rooms.value.filter((r) => !r.isArchived));
  const archivedRooms = computed(() => rooms.value.filter((r) => r.isArchived));

  // Current-user role flags — single source of truth for the panel and page
  // (previously duplicated in GamePage.vue). Derived from game.participation,
  // which serializes the API's GameParticipation flags (see DM.Web.API
  // Game.cs), NOT GameRole names: Owner (master), Authority (master or
  // assistant), PendingAssistant, Player, Reader, Moderator (game mentor).
  const participation = computed<GameParticipation[]>(
    () => game.value?.participation ?? [],
  );
  const isMaster = computed(() =>
    participation.value.includes(GameParticipation.Owner),
  );
  const isAssistant = computed(
    () =>
      participation.value.includes(GameParticipation.Authority) &&
      !isMaster.value,
  );
  const isMentor = computed(() =>
    participation.value.includes(GameParticipation.Moderator),
  );
  const isSubscribed = computed(() =>
    participation.value.includes(GameParticipation.Reader),
  );
  const isPlayer = computed(
    () =>
      participation.value.includes(GameParticipation.Player) ||
      isMentor.value ||
      isMaster.value,
  );
  /** Master, assistant or mentor — may edit/manage the game. */
  const canManage = computed(
    () => isMaster.value || isAssistant.value || isMentor.value,
  );

  // Computed
  const isLoading = computed(
    () =>
      gameLoading.value ||
      roomsLoading.value ||
      postsLoading.value ||
      charactersLoading.value,
  );

  const hasError = computed(
    () =>
      gameError.value ||
      roomsError.value ||
      postsError.value ||
      charactersError.value,
  );

  /**
   * Load the game the URL names.
   *
   * Returns `{ ok, status }`, the same contract the forum store's selectors
   * carry: a game that does not exist, one the viewer may not read
   * (premoderation, blacklist, privacy) and a server that fell over are three
   * different pages, and the single paragraph this used to render told a reader
   * whose access was refused that the network had failed. `gameError` stays for
   * the sidebar panel, which has room for a line and not for a page. The
   * returned object is truthy, so the call sites that only await it are
   * unaffected.
   */
  async function loadGame(
    id: string,
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = gameGuard.next();
    gameLoading.value = true;
    gameError.value = null;
    gameErrorStatus.value = null;

    const { data, error } = await gameApi.getGame(id);

    // A newer load owns the visible state: committing here would draw the game
    // the user just left under the new title, and clearing the spinner would
    // present the request still on the wire as finished. Reported as ok so the
    // (equally stale) caller treats it as a no-op instead of raising an error
    // page over the newer navigation's state.
    if (!gameGuard.isCurrent(requestId)) return { ok: true };

    if (error) {
      gameError.value = "Не удалось загрузить игру";
      gameErrorStatus.value = error.status ?? null;
      game.value = null;
      gameLoading.value = false;
      return { ok: false, status: error.status };
    }
    if (data) {
      game.value = unwrapResource<Game>(data);
    }

    gameLoading.value = false;
    return { ok: true };
  }

  // Load rooms
  async function loadRooms(gameId: string): Promise<void> {
    const requestId = roomsGuard.next();
    roomsLoading.value = true;
    roomsError.value = null;

    const { data, error } = await gameApi.getRooms(gameId);

    // Stale continuation — the newer request owns the visible state.
    if (!roomsGuard.isCurrent(requestId)) return;

    if (error) {
      roomsError.value = "Не удалось загрузить комнаты";
      rooms.value = [];
    } else if (data) {
      rooms.value = data.resources;
    }

    roomsLoading.value = false;
  }

  // Load posts for a room by room ID
  async function loadPosts(roomId: string, page: number = 1): Promise<void> {
    const requestId = postsGuard.next();
    postsLoading.value = true;
    postsError.value = null;

    // Find the room to set as current
    const room = rooms.value.find((r) => r.id === roomId);
    if (room) {
      currentRoom.value = room;
    }

    const { data, error } = await gameApi.getPosts(roomId, { number: page });

    // Stale continuation. currentRoom is assigned synchronously above, so
    // without this the header names the room the newer call selected while the
    // list under it holds the posts of the older one.
    if (!postsGuard.isCurrent(requestId)) return;

    if (error) {
      postsError.value = "Не удалось загрузить посты";
      posts.value = [];
      postsPaging.value = null;
    } else if (data) {
      posts.value = data.resources;
      postsPaging.value = data.paging ?? null;
    }

    postsLoading.value = false;
  }

  // Load posts for a room by room number (URL-based)
  async function loadPostsByRoomNumber(
    gameId: string,
    roomNumber: number,
    page: number = 1,
  ): Promise<void> {
    const requestId = postsGuard.next();

    // Deep links (first-unread redirects, direct URLs) land here before the
    // rooms list is in the store - resolve it first
    if (!rooms.value.length) {
      await loadRooms(gameId);
    }

    // The room in the URL changed while the rooms list was on the wire: the
    // newer call resolves against its own room number, not this one.
    if (!postsGuard.isCurrent(requestId)) return;

    // Find the room by number
    const room = rooms.value.find((r) => r.roomNumber === roomNumber);
    if (!room) {
      postsError.value = `Комната №${roomNumber} не найдена`;
      posts.value = [];
      postsPaging.value = null;
      currentRoom.value = null;
      return;
    }

    await loadPosts(room.id as string, page);
  }

  // Load characters
  async function loadCharacters(gameId: string): Promise<void> {
    const requestId = charactersGuard.next();
    charactersLoading.value = true;
    charactersError.value = null;

    const { data, error } = await gameApi.getCharacters(gameId);

    // Stale continuation — the newer request owns the visible state.
    if (!charactersGuard.isCurrent(requestId)) return;

    if (error) {
      charactersError.value = "Не удалось загрузить персонажей";
      characters.value = [];
    } else if (data) {
      characters.value = data.resources;
    }

    charactersLoading.value = false;
  }

  // Load comments. Filter, sort and page all come from the URL through the
  // discussion section; this forwards the query it is handed.
  async function loadComments(
    gameId: string,
    query: CommentsQuery = {},
  ): Promise<void> {
    const requestId = commentsGuard.next();
    commentsLoading.value = true;
    commentsError.value = false;

    const { data, error } = await gameApi.getGameComments(gameId, query);

    // Stale continuation — the newer request owns the visible state.
    if (!commentsGuard.isCurrent(requestId)) return;

    if (error) {
      commentsError.value = true;
      comments.value = [];
      commentsPaging.value = null;
    } else if (data) {
      comments.value = data.resources;
      commentsPaging.value = data.paging ?? null;
    }

    commentsLoading.value = false;
  }

  // --- Single comment mutations (edit / delete / likes) ---
  // Mirror the forum boardsStore idiom: in-place list patches from the server
  // response, no full reload, and nothing patched when the server refused —
  // the error goes up to the page instead.

  async function updateComment(id: string, text: string) {
    const { data, error } = await gameApi.updateGameComment(id, { text });
    if (!error) {
      const updated = unwrapResource<Comment>(data);
      if (updated) {
        const index = comments.value.findIndex((c) => c.id === id);
        if (index !== -1) comments.value[index] = updated;
      }
    }
    return { error };
  }

  async function deleteComment(id: string) {
    const { error } = await gameApi.deleteGameComment(id);
    if (!error) {
      const index = comments.value.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value[index] = markRemoved(comments.value[index]);
      }
    }
    return { error };
  }

  async function likeComment(id: string) {
    const { data } = await gameApi.likeGameComment(id);
    const liker = unwrapResource<User>(data);
    if (liker) {
      const index = comments.value.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value[index];
        comments.value[index] = {
          ...comment,
          likes: [...(comment.likes ?? []), liker] as Comment["likes"],
        };
      }
    }
  }

  async function unlikeComment(id: string) {
    const { error } = await gameApi.unlikeGameComment(id);
    if (error) return;
    const index = comments.value.findIndex((c) => c.id === id);
    if (index === -1) return;
    const comment = comments.value[index];
    const username = useAuthStore().user?.username;
    if (comment.likes && username) {
      comments.value[index] = {
        ...comment,
        likes: comment.likes.filter(
          (u) => u.username !== username,
        ) as Comment["likes"],
      };
    }
  }

  // Load blacklist
  async function loadBlacklist(gameId: string): Promise<void> {
    const requestId = blacklistGuard.next();
    blacklistLoading.value = true;
    blacklistError.value = null;

    const { data, error } = await gameApi.getBlacklist(gameId);

    // Stale continuation — the newer request owns the visible state.
    if (!blacklistGuard.isCurrent(requestId)) return;

    if (error) {
      blacklistError.value = "Не удалось загрузить черный список";
      blacklist.value = [];
    } else if (data) {
      blacklist.value = data.resources;
    }

    blacklistLoading.value = false;
  }

  // Load game users
  async function loadUsers(gameId: string): Promise<void> {
    const requestId = usersGuard.next();
    usersLoading.value = true;
    usersError.value = null;

    const { data, error } = await gameApi.getUsers(gameId);

    // Stale continuation — the newer request owns the visible state.
    if (!usersGuard.isCurrent(requestId)) return;

    if (error) {
      usersError.value = "Не удалось загрузить участников";
      users.value = [];
    } else if (data) {
      users.value = data.resources;
    }

    usersLoading.value = false;
  }

  // === Mutations ===
  // Each mutation calls the API and then re-syncs the affected store slices
  // (loadGame refreshes participation/status/recruitment; loadRooms refreshes
  // the active/archived split). They return a boolean success flag so callers
  // can surface errors without reaching into the API layer.

  async function transitionStatus(
    transition: GameStatusTransition,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.transitionStatus(id, transition);
    if (error) return error;
    await loadGame(id);
    return null;
  }

  async function changePremoderation(
    transition: GamePremoderationTransition,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.changePremoderation(id, transition);
    if (error) return error;
    await loadGame(id);
    return null;
  }

  async function resetRecruitment(): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.resetRecruitment(id);
    if (error) return error;
    await loadGame(id);
    return null;
  }

  async function deleteGame(): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const { error } = await gameApi.deleteGame(game.value.id);
    if (error) return error;
    // Refresh the list caches: otherwise the game the user just deleted keeps
    // showing in /games and in the sidebar until the entries expire.
    await useGamesStore().invalidateGameLists();
    return null;
  }

  async function createRoom(
    room: CreateRoomInput,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await gameApi.createRoom(id, room);
    if (error) return error;
    await loadRooms(id);
    return null;
  }

  async function archiveRoom(roomId: string): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const { error } = await gameApi.archiveRoom(roomId);
    if (error) return error;
    await loadRooms(game.value.id);
    return null;
  }

  // Reset all data (when leaving game page)
  function reset(): void {
    // Replies still on the wire belong to the game being left. GamePage wipes
    // the store on an id change and on unmount, so without bumping every token
    // the late reply repopulates what was just cleared.
    detailGuards.forEach((guard) => guard.next());

    game.value = null;
    gameLoading.value = false;
    gameError.value = null;
    gameErrorStatus.value = null;

    rooms.value = [];
    roomsLoading.value = false;
    roomsError.value = null;

    currentRoom.value = null;
    posts.value = [];
    postsPaging.value = null;
    postsLoading.value = false;
    postsError.value = null;

    characters.value = [];
    charactersLoading.value = false;
    charactersError.value = null;

    comments.value = [];
    commentsPaging.value = null;
    commentsLoading.value = false;
    commentsError.value = false;

    blacklist.value = [];
    blacklistLoading.value = false;
    blacklistError.value = null;

    users.value = [];
    usersLoading.value = false;
    usersError.value = null;
  }

  // Subscribe to game
  async function subscribe(): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;

    const { error } = await gameApi.subscribe(game.value.id);
    if (error) {
      return error;
    }
    // Reload game to update roles
    await loadGame(game.value.id);
    return null;
  }

  // Unsubscribe from game
  async function unsubscribe(): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;

    const { error } = await gameApi.unsubscribe(game.value.id);
    if (error) {
      return error;
    }
    // Reload game to update roles
    await loadGame(game.value.id);
    return null;
  }

  return {
    // State
    game,
    gameLoading,
    gameError,
    gameErrorStatus,
    rooms,
    roomsLoading,
    roomsError,
    currentRoom,
    posts,
    postsPaging,
    postsLoading,
    postsError,
    characters,
    charactersLoading,
    charactersError,
    comments,
    commentsPaging,
    commentsLoading,
    commentsError,
    blacklist,
    blacklistLoading,
    blacklistError,
    users,
    usersLoading,
    usersError,

    // Computed
    isLoading,
    hasError,
    activeRooms,
    archivedRooms,
    isMaster,
    isAssistant,
    isMentor,
    isSubscribed,
    isPlayer,
    canManage,

    // Actions
    loadGame,
    loadRooms,
    loadPosts,
    loadPostsByRoomNumber,
    loadCharacters,
    loadComments,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
    loadBlacklist,
    loadUsers,
    transitionStatus,
    changePremoderation,
    resetRecruitment,
    deleteGame,
    createRoom,
    archiveRoom,
    reset,
    subscribe,
    unsubscribe,
  };
});
