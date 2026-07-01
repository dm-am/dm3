// Game store
// Migrated from stores/games.ts and stores/gameDetails.ts

import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { Game, GameRef, Character, Room, Post, Tag } from "./types";
import type { ListEnvelope, Paging, Comment } from "@/shared/api/models/common";
import gameApi, { type GamesSearchParams } from "../api/gameApi";
import {
  useApiList,
  useApiResource,
} from "@/shared/lib/composables/useApiResource";

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

  // Search cache: key → { data, timestamp }
  const CACHE_TTL = 30_000; // 30 seconds
  const searchCache = new Map<
    string,
    { data: ListEnvelope<Game>; timestamp: number }
  >();

  /**
   * Create stable cache key from search params
   */
  function createCacheKey(params: GamesSearchParams): string {
    // Sort keys for stable ordering
    const sorted: Record<string, unknown> = {};
    const keys = Object.keys(params).sort();
    for (const key of keys) {
      const value = params[key as keyof GamesSearchParams];
      if (value !== undefined && value !== null && value !== "") {
        // Sort arrays for stable keys
        if (Array.isArray(value)) {
          sorted[key] = [...value].sort().join(",");
        } else {
          sorted[key] = value;
        }
      }
    }
    return JSON.stringify(sorted);
  }

  /**
   * Search games with filters (with caching)
   */
  async function searchGames(params: GamesSearchParams): Promise<void> {
    const cacheKey = createCacheKey(params);
    const cached = searchCache.get(cacheKey);
    const now = Date.now();

    // Return cached if fresh
    if (cached && now - cached.timestamp < CACHE_TTL) {
      searchResult.value = cached.data;
      lastSearchParams.value = params;
      return;
    }

    // Show stale while revalidating
    if (cached) {
      searchResult.value = cached.data;
    }

    searchLoading.value = true;
    searchError.value = null;
    lastSearchParams.value = params;

    const { data, error } = await gameApi.searchGames(params);

    if (error) {
      searchError.value = "Не удалось загрузить игры";
      // Keep stale data on error if available
      if (!cached) {
        searchResult.value = null;
      }
    } else if (data) {
      searchResult.value = data;
      // Update cache
      searchCache.set(cacheKey, { data, timestamp: now });
      // Clean old entries (keep last 20)
      if (searchCache.size > 20) {
        const firstKey = searchCache.keys().next().value;
        if (firstKey) searchCache.delete(firstKey);
      }
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
    const cacheKey = createCacheKey(params);

    // Skip if already cached
    if (searchCache.has(cacheKey)) return;

    // Fetch in background without updating UI
    const { data } = await gameApi.searchGames(params);
    if (data) {
      searchCache.set(cacheKey, { data, timestamp: Date.now() });
      // Clean old entries (keep last 20)
      if (searchCache.size > 20) {
        const firstKey = searchCache.keys().next().value;
        if (firstKey) searchCache.delete(firstKey);
      }
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
    resetAllGames: () => {
      participating.reset();
      moderation.reset();
      popular.reset();
      activePage.reset();
      recruitingPage.reset();
      finishedPage.reset();
      resetSearch();
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

  // Rooms data
  const rooms = ref<Room[]>([]);
  const roomsLoading = ref(false);
  const roomsError = ref<string | null>(null);

  // Current room and posts
  const currentRoom = ref<Room | null>(null);
  const posts = ref<Post[]>([]);
  const postsPaging = ref<Paging | null>(null);
  const postsLoading = ref(false);
  const postsError = ref<string | null>(null);

  // Characters data
  const characters = ref<Character[]>([]);
  const charactersLoading = ref(false);
  const charactersError = ref<string | null>(null);

  // Comments data
  const comments = ref<Comment[]>([]);
  const commentsPaging = ref<Paging | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref<string | null>(null);

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

  // Load game
  async function loadGame(id: string): Promise<void> {
    gameLoading.value = true;
    gameError.value = null;

    const { data, error } = await gameApi.getGame(id);

    if (error) {
      gameError.value = "Не удалось загрузить игру";
      game.value = null;
    } else if (data) {
      // The details endpoint wraps the payload in a single-resource
      // envelope ({ resource }); unwrap defensively so a bare payload
      // keeps working too.
      game.value = data.resource ?? (data as unknown as Game);
    }

    gameLoading.value = false;
  }

  // Load rooms
  async function loadRooms(gameId: string): Promise<void> {
    roomsLoading.value = true;
    roomsError.value = null;

    const { data, error } = await gameApi.getRooms(gameId);

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
    postsLoading.value = true;
    postsError.value = null;

    // Find the room to set as current
    const room = rooms.value.find((r) => r.id === roomId);
    if (room) {
      currentRoom.value = room;
    }

    const { data, error } = await gameApi.getPosts(roomId, { number: page });

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
    // Deep links (first-unread redirects, direct URLs) land here before the
    // rooms list is in the store - resolve it first
    if (!rooms.value.length) {
      await loadRooms(gameId);
    }

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
    charactersLoading.value = true;
    charactersError.value = null;

    const { data, error } = await gameApi.getCharacters(gameId);

    if (error) {
      charactersError.value = "Не удалось загрузить персонажей";
      characters.value = [];
    } else if (data) {
      characters.value = data.resources;
    }

    charactersLoading.value = false;
  }

  // Load comments
  async function loadComments(gameId: string, page: number = 1): Promise<void> {
    commentsLoading.value = true;
    commentsError.value = null;

    const { data, error } = await gameApi.getGameComments(gameId, {
      number: page,
    });

    if (error) {
      commentsError.value = "Не удалось загрузить комментарии";
      comments.value = [];
      commentsPaging.value = null;
    } else if (data) {
      comments.value = data.resources;
      commentsPaging.value = data.paging ?? null;
    }

    commentsLoading.value = false;
  }

  // Reset all data (when leaving game page)
  function reset(): void {
    game.value = null;
    gameLoading.value = false;
    gameError.value = null;

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
    commentsError.value = null;
  }

  // Subscribe to game
  async function subscribe(): Promise<boolean> {
    if (!game.value) return false;

    const { error } = await gameApi.subscribe(game.value.id);
    if (error) {
      return false;
    }
    // Reload game to update roles
    await loadGame(game.value.id);
    return true;
  }

  // Unsubscribe from game
  async function unsubscribe(): Promise<boolean> {
    if (!game.value) return false;

    const { error } = await gameApi.unsubscribe(game.value.id);
    if (error) {
      return false;
    }
    // Reload game to update roles
    await loadGame(game.value.id);
    return true;
  }

  return {
    // State
    game,
    gameLoading,
    gameError,
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

    // Computed
    isLoading,
    hasError,

    // Actions
    loadGame,
    loadRooms,
    loadPosts,
    loadPostsByRoomNumber,
    loadCharacters,
    loadComments,
    reset,
    subscribe,
    unsubscribe,
  };
});
