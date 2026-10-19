// Kept apart from the list store, which the entry reaches through the sidebar:
// in one module the whole game zone landed in the entry chunk.

import { defineStore } from "pinia";

import characterApi from "../api/characterApi";
import discussionApi from "../api/discussionApi";
import gameApi from "../api/gameApi";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createCommentSection } from "@/shared/lib/composables/createCommentSection";
import { createGameSlice } from "./details/gameSlice";
import { createGameRoles } from "./details/gameRoles";
import {
  createGameChanges,
  createSubscriptionChanges,
} from "./details/gameChanges";
import { createRoomsSlice, createRoomChanges } from "./details/roomsSlice";
import { createRoomPostsSlice } from "./details/roomPostsSlice";
import { createListSlice } from "./details/listSlice";

export const useGameDetailsStore = defineStore("gameDetails", () => {
  const { pageSize } = usePaging();

  const {
    game,
    gameLoading,
    gameError,
    gameErrorStatus,
    gameGuard,
    loadGame,
    resetGame,
  } = createGameSlice();
  const {
    rooms,
    roomsLoading,
    roomsError,
    roomsGuard,
    activeRooms,
    archivedRooms,
    loadRooms,
    resetRooms,
  } = createRoomsSlice();
  const {
    currentRoom,
    posts,
    postsPaging,
    postsLoading,
    postsError,
    postsGuard,
    loadPosts,
    loadPostsByRoomNumber,
    locateFirstUnreadPost,
    locatePostPage,
    pageOfPostNumber,
    resetRoomPosts,
  } = createRoomPostsSlice({ rooms, roomsError, loadRooms, pageSize });

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
    getComments: (gameId, query) =>
      discussionApi.getGameComments(gameId, query),
    updateComment: (id, comment) =>
      discussionApi.updateGameComment(id, comment),
    deleteComment: (id, options) =>
      discussionApi.deleteGameComment(id, options),
    likeComment: (id) => discussionApi.likeGameComment(id),
    unlikeComment: (id) => discussionApi.unlikeGameComment(id),
    currentUsername: () => useAuthStore().user?.username,
  });

  const characters = createListSlice(
    (gameId) => characterApi.getCharacters(gameId),
    "Не удалось загрузить персонажей",
  );
  const blacklist = createListSlice(
    (gameId) => gameApi.getBlacklist(gameId),
    "Не удалось загрузить черный список",
  );
  const users = createListSlice(
    (gameId) => gameApi.getUsers(gameId),
    "Не удалось загрузить участников",
  );

  // One token per slice: the slices load in parallel, and a shared one would cancel siblings.
  const detailGuards = [
    gameGuard,
    roomsGuard,
    postsGuard,
    characters.guard,
    commentsGuard,
    blacklist.guard,
    users.guard,
  ];

  function reset(): void {
    // Replies still on the wire belong to the game being left.
    detailGuards.forEach((guard) => guard.next());

    resetGame();
    resetRooms();
    resetRoomPosts();
    characters.reset();

    comments.value = [];
    commentsPaging.value = null;
    commentsLoading.value = false;
    commentsError.value = false;

    blacklist.reset();
    users.reset();
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
    characters: characters.items,
    charactersLoading: characters.loading,
    charactersError: characters.error,
    comments,
    commentsPaging,
    commentsLoading,
    commentsError,
    blacklist: blacklist.items,
    blacklistLoading: blacklist.loading,
    blacklistError: blacklist.error,
    users: users.items,
    usersLoading: users.loading,
    usersError: users.error,

    activeRooms,
    archivedRooms,
    ...createGameRoles(game),

    // Actions
    loadGame,
    loadRooms,
    loadPosts,
    loadPostsByRoomNumber,
    locateFirstUnreadPost,
    locatePostPage,
    pageOfPostNumber,
    loadCharacters: characters.load,
    loadComments,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
    loadBlacklist: blacklist.load,
    loadUsers: users.load,
    ...createGameChanges(game, loadGame),
    ...createRoomChanges(game, loadRooms),
    reset,
    ...createSubscriptionChanges(game, loadGame),
  };
});
