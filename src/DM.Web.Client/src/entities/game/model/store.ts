// Game store
// Migrated from stores/games.ts and stores/gameDetails.ts

import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { Game, Character, Room, Post } from "./types";
import type { ListEnvelope, Paging, Comment } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import { useApiList, useApiResource } from "@/shared/lib/composables/useApiResource";

/**
 * Store for game lists (menu/sidebar, pagination)
 */
export const useGamesStore = defineStore("games", () => {
  // Menu/sidebar lists - just need the resources array
  const own = useApiList<Game>(() => gameApi.getOwnGames());
  const moderation = useApiList<Game>(() => gameApi.getModerationGames());
  const popular = useApiList<Game>(() => gameApi.getPopularGames());
  const subscribed = useApiList<Game>(() => gameApi.getSubscribedGames());

  // Page lists - need full envelope with paging
  const activePage = useApiResource<ListEnvelope<Game>>(
    () => gameApi.getActiveGames(),
    { cacheMs: 30_000 },
  );
  const recruitingPage = useApiResource<ListEnvelope<Game>>(
    () => gameApi.getRecruitingGames(),
    { cacheMs: 30_000 },
  );
  const finishedPage = useApiResource<ListEnvelope<Game>>(
    () => gameApi.getFinishedGames(),
    { cacheMs: 30_000 },
  );
  const moderationPage = useApiResource<ListEnvelope<Game>>(
    () => gameApi.getModerationGames(),
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
    subscribedGames: subscribed.data,
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

    // Loading states for subscribed
    subscribedGamesLoading: subscribed.loading,

    // Fetch functions
    fetchOwnGames: own.fetch,
    fetchModerationGames: moderation.fetch,
    fetchModerationGamesPage: moderationPage.fetch,
    fetchPopularGames: popular.fetch,
    fetchSubscribedGames: subscribed.fetch,
    fetchActiveGames: activePage.fetch,
    fetchRecruitingGames: recruitingPage.fetch,
    fetchFinishedGames: finishedPage.fetch,

    // Reset functions (for logout)
    resetOwnGames: own.reset,
    resetModerationGames: moderation.reset,
    resetSubscribedGames: subscribed.reset,
    resetAllGames: () => {
      own.reset();
      moderation.reset();
      popular.reset();
      subscribed.reset();
      activePage.reset();
      recruitingPage.reset();
      finishedPage.reset();
    },
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
      gameError.value = error.title || "Failed to load game";
      game.value = null;
    } else if (data) {
      game.value = data;
    }

    gameLoading.value = false;
  }

  // Load rooms
  async function loadRooms(gameId: string): Promise<void> {
    roomsLoading.value = true;
    roomsError.value = null;

    const { data, error } = await gameApi.getRooms(gameId);

    if (error) {
      roomsError.value = error.title || "Failed to load rooms";
      rooms.value = [];
    } else if (data) {
      rooms.value = data.resources;
    }

    roomsLoading.value = false;
  }

  // Load posts for a room
  async function loadPosts(
    roomId: string,
    page: number = 1,
  ): Promise<void> {
    postsLoading.value = true;
    postsError.value = null;

    // Find the room to set as current
    const room = rooms.value.find((r) => r.id === roomId);
    if (room) {
      currentRoom.value = room;
    }

    const { data, error } = await gameApi.getPosts(roomId, { number: page });

    if (error) {
      postsError.value = error.title || "Failed to load posts";
      posts.value = [];
      postsPaging.value = null;
    } else if (data) {
      posts.value = data.resources;
      postsPaging.value = data.paging ?? null;
    }

    postsLoading.value = false;
  }

  // Load characters
  async function loadCharacters(gameId: string): Promise<void> {
    charactersLoading.value = true;
    charactersError.value = null;

    const { data, error } = await gameApi.getCharacters(gameId);

    if (error) {
      charactersError.value = error.title || "Failed to load characters";
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

    const { data, error } = await gameApi.getGameComments(gameId, { number: page });

    if (error) {
      commentsError.value = error.title || "Failed to load comments";
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
    loadCharacters,
    loadComments,
    reset,
    subscribe,
    unsubscribe,
  };
});
