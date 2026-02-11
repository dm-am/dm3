import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { Game, Character, Room, Post } from "@/api/models/game";
import type { Paging } from "@/api/models/common";
import type { Comment } from "@/api/models/forum";
import gameApi from "@/api/requests/gameApi";

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
      game.value = data.resource;
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
    // Reload game to update participation
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
    // Reload game to update participation
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
