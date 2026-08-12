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
  Comment,
  User,
  GeneralError,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
import gameApi from "../api/gameApi";
import { unwrapResource, type CommentsQuery } from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
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
