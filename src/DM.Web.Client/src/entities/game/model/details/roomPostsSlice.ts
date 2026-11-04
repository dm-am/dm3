import { ref, type Ref } from "vue";
import type { Room } from "../roomTypes";
import type { Post, RoomPostsFilter } from "../postTypes";
import type { PagingInfo } from "@/shared/api/models/common";
import postApi from "../../api/postApi";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

export interface RoomsOfGame {
  rooms: Ref<Room[]>;
  roomsError: Ref<string | null>;
  loadRooms: (gameId: string) => Promise<void>;
  pageSize: Ref<number>;
}

function roomPostsState() {
  return {
    currentRoom: ref<Room | null>(null),
    posts: ref<Post[]>([]),
    postsPaging: ref<PagingInfo | null>(null),
    postsLoading: ref(false),
    postsError: ref<string | null>(null),
    postsGuard: createRequestGuard(),
  };
}

type RoomPosts = ReturnType<typeof roomPostsState>;

export function createRoomPostsSlice(of: RoomsOfGame) {
  const state = roomPostsState();

  return {
    ...state,
    loadPosts: (roomId: string, page: number = 1, filter?: RoomPostsFilter) =>
      loadPosts(state, of, roomId, page, filter),
    loadPostsByRoomNumber: (
      gameId: string,
      roomNumber: number,
      page: number = 1,
      filter?: RoomPostsFilter,
    ) => loadPostsByRoomNumber(state, of, gameId, roomNumber, page, filter),
    locateFirstUnreadPost: (gameId: string, roomNumber: number) =>
      locateFirstUnreadPost(of, gameId, roomNumber),
    locatePostPage: (postId: string) => locatePostPage(of, postId),
    pageOfPostNumber: (number: number) => pageOfPostNumber(of, number),
    resetRoomPosts: () => resetRoomPosts(state),
  };
}

async function loadPosts(
  state: RoomPosts,
  { rooms, pageSize }: RoomsOfGame,
  roomId: string,
  page: number,
  filter?: RoomPostsFilter,
): Promise<void> {
  const { currentRoom, posts, postsPaging, postsLoading, postsError } = state;
  const requestId = state.postsGuard.next();
  postsLoading.value = true;
  postsError.value = null;

  const room = rooms.value.find((r) => r.id === roomId);
  if (room) {
    currentRoom.value = room;
  }

  const { data, error } = await postApi.getPosts(
    roomId,
    { number: page, take: pageSize.value },
    filter,
  );

  if (!state.postsGuard.isCurrent(requestId)) return;

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

async function loadPostsByRoomNumber(
  state: RoomPosts,
  of: RoomsOfGame,
  gameId: string,
  roomNumber: number,
  page: number,
  filter?: RoomPostsFilter,
): Promise<void> {
  const { rooms, roomsError, loadRooms } = of;
  const { postsLoading, postsError } = state;
  const requestId = state.postsGuard.next();

  // Here, not in loadPosts: resolving the room number can take a request of its own.
  postsLoading.value = true;
  postsError.value = null;

  // A deep link lands here before the rooms are in the store.
  if (!rooms.value.length) {
    await loadRooms(gameId);
  }

  if (!state.postsGuard.isCurrent(requestId)) return;

  // Not-found is only for a list that answered, so a failed load says itself.
  if (!rooms.value.length && roomsError.value) {
    resetRoomPosts(state);
    postsError.value = roomsError.value;
    return;
  }

  const room = rooms.value.find((r) => r.roomNumber === roomNumber);
  if (!room) {
    resetRoomPosts(state);
    postsError.value = `Комната №${roomNumber} не найдена`;
    return;
  }

  await loadPosts(state, of, room.id, page, filter);
}

// Asked before the page loads, which marks the room read; null opens the first page.
async function locateFirstUnreadPost(
  { rooms, loadRooms, pageSize }: RoomsOfGame,
  gameId: string,
  roomNumber: number,
): Promise<{ page: number; postId: string; hasUnread: boolean } | null> {
  if (!rooms.value.length) await loadRooms(gameId);
  const room = rooms.value.find((r) => r.roomNumber === roomNumber);
  if (!room?.id) return null;

  const { data } = await postApi.getFirstUnreadRoomPost(
    room.id,
    pageSize.value,
  );
  const found = data?.resource;
  if (!found?.postId) return null;
  return {
    page: Math.max(1, found.page),
    postId: found.postId,
    hasUnread: found.hasUnread,
  };
}

async function locatePostPage(
  of: RoomsOfGame,
  postId: string,
): Promise<number | null> {
  const { data } = await postApi.getPost(postId, { ownsRefusal: true });
  const number = data?.resource?.number;
  return number ? pageOfPostNumber(of, number) : null;
}

function pageOfPostNumber({ pageSize }: RoomsOfGame, number: number): number {
  return Math.max(1, Math.ceil(number / pageSize.value));
}

function resetRoomPosts(state: RoomPosts): void {
  state.currentRoom.value = null;
  state.posts.value = [];
  state.postsPaging.value = null;
  state.postsLoading.value = false;
  state.postsError.value = null;
}
