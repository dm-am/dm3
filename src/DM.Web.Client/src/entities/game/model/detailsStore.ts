// Store for a single game: the zone shell, its sub-pages and the game panel.
//
// Its own module rather than a second store next to the list store. The three
// sidebar blocks that draw the game lists are reachable from the entry, and a
// store declared at the top level of a module is not something the bundler may
// drop, so sharing one file put every room, character, post, comment, notepad
// and blacklist request of the game zone into the entry chunk with them.

import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type {
  Game,
  Character,
  Room,
  Post,
  GameUser,
  CreateRoomInput,
  GameStatusTransition,
  GamePremoderationTransition,
} from "./types";
import { GameParticipation } from "./types";
import type {
  PagingInfo,
  User,
  GeneralError,
} from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import { unwrapResource } from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createCommentSection } from "@/shared/lib/composables/createCommentSection";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { requestNotSent } from "@/shared/lib/errors";
// One edge, and it points this way on purpose: deleting a game has to drop the
// list caches. The list store must not import this one back — that is what put
// the whole game zone in the entry chunk in the first place.
import { useGamesStore } from "./store";

/**
 * Store for single game details (game page)
 */
export const useGameDetailsStore = defineStore("gameDetails", () => {
  // The room asks the API for the reader's own page size, the same shape the
  // forum store uses for its topics and its comments. Read here and not in the
  // API module: that one has no store to ask, and a size spelled there is a
  // number no setting can move.
  const { postsPerPage } = usePaging();

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

  // Discussion comments: state, guarded loader and single-comment mutations
  // come from the shared section factory — the blog details store runs the
  // same code against its own endpoints.
  const {
    comments,
    commentsPaging,
    commentsLoading,
    commentsError,
    commentsGuard,
    loadComments,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
  } = createCommentSection({
    getComments: (gameId, query) => gameApi.getGameComments(gameId, query),
    updateComment: (id, comment) => gameApi.updateGameComment(id, comment),
    deleteComment: (id) => gameApi.deleteGameComment(id),
    likeComment: (id) => gameApi.likeGameComment(id),
    unlikeComment: (id) => gameApi.unlikeGameComment(id),
    currentUsername: () => useAuthStore().user?.username,
  });

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

  /**
   * The rooms request currently on the wire, kept so a second caller joins it
   * instead of starting its own.
   *
   * Two loaders want the list on one mount: the game shell preloads it for
   * navigation, and a room page resolves its own room number against it. Each
   * fired a request, and the guard below then threw one of the two answers
   * away — whichever lost owned an awaiter, and that awaiter woke up to an
   * empty list and reported the room as missing. A cold load of a room showed
   * "Комната №N не найдена" and never asked for the posts at all.
   *
   * Keyed by game id: a join is only ever a join to the same list. It is not
   * a cache — the entry lives exactly as long as the request, so a refresh
   * after a post or a room edit still goes to the server.
   */
  let roomsInFlight: { gameId: string; promise: Promise<void> } | null = null;

  // Load rooms
  async function loadRooms(gameId: string): Promise<void> {
    if (roomsInFlight?.gameId === gameId) return roomsInFlight.promise;

    const promise = fetchRooms(gameId);
    roomsInFlight = { gameId, promise };
    try {
      await promise;
    } finally {
      // Only the owner clears it, and only if a newer request has not
      // already taken the slot.
      if (roomsInFlight?.promise === promise) roomsInFlight = null;
    }
  }

  async function fetchRooms(gameId: string): Promise<void> {
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

    const { data, error } = await gameApi.getPosts(roomId, {
      number: page,
      take: postsPerPage.value,
    });

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

    // The wait starts here, not in loadPosts below: resolving the room number
    // can take a request of its own, and until then the page read "В этой
    // комнате пока нет постов" — an answer, and the wrong one, while the
    // question was still open.
    postsLoading.value = true;
    postsError.value = null;

    // Deep links (first-unread redirects, direct URLs) land here before the
    // rooms list is in the store - resolve it first
    if (!rooms.value.length) {
      await loadRooms(gameId);
    }

    // The room in the URL changed while the rooms list was on the wire: the
    // newer call resolves against its own room number, not this one. The
    // loading flag belongs to that newer call now, so it is left alone.
    if (!postsGuard.isCurrent(requestId)) return;

    // The list itself failed to load: saying the room does not exist would
    // blame the address for a network failure. Report the load error and let
    // retry re-ask; not-found is reserved for a list that answered.
    if (!rooms.value.length && roomsError.value) {
      postsError.value = roomsError.value;
      posts.value = [];
      postsPaging.value = null;
      currentRoom.value = null;
      postsLoading.value = false;
      return;
    }

    // Find the room by number
    const room = rooms.value.find((r) => r.roomNumber === roomNumber);
    if (!room) {
      postsError.value = `Комната №${roomNumber} не найдена`;
      posts.value = [];
      postsPaging.value = null;
      currentRoom.value = null;
      postsLoading.value = false;
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
    // the late reply repopulates what was just cleared. The rooms request in
    // flight is one of those replies: its answer is about to be discarded, so
    // a later caller must start its own instead of joining it.
    detailGuards.forEach((guard) => guard.next());
    roomsInFlight = null;

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
