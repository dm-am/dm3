import { nextTick, onScopeDispose, ref, watch, type Ref } from "vue";
import {
  useRoute,
  useRouter,
  type RouteLocationNormalizedLoaded,
  type Router,
} from "vue-router";
import {
  postApi,
  useGameDetailsStore,
  type RoomPostsFilter,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { markArrival } from "@/shared/lib/composables/useScrollToElement";
import {
  useGuardedRequest,
  type UseGuardedRequestReturn,
} from "@/shared/lib/composables/useGuardedRequest";
import { getScrollContainer } from "@/shared/lib/scroll";

/** Room between the top edge and a post brought up to it. */
const REVEAL_GAP = 16;

interface RoomOpeningOptions {
  gameId: Ref<string>;
  roomNum: Ref<number>;
  page: Ref<number>;
  apiFilter: Ref<RoomPostsFilter>;
  isFilteredView: Ref<boolean>;
  ensureRooms: () => Promise<void>;
  roomRefused: Ref<boolean>;
}

interface RoomView extends RoomOpeningOptions {
  route: RouteLocationNormalizedLoaded;
  router: Router;
  gameStore: ReturnType<typeof useGameDetailsStore>;
  auth: ReturnType<typeof useAuthStore>;
  // Drops a step answered after a newer one began; what follows a step starts
  // in its apply, so nothing newer slips in between.
  runView: UseGuardedRequestReturn["run"];
  locating: Ref<boolean>;
  // A change of it is an entry.
  enteredRoom: string | null;
  markedRoomId: string | null;
  pendingTarget: { postId: string; mark: boolean } | null;
  /** The room is off the page: a post on its way up stays where it is. */
  gone: boolean;
}

function hashPostId(view: RoomView): string | null {
  const { hash } = view.route;
  return hash.startsWith("#post-") ? hash.slice(6) || null : null;
}

/**
 * The page of a linked post, or for a signed-in reader of the first unread one
 * (the last when all is read); null means the first page.
 */
async function locateOpening(view: RoomView): Promise<{
  page: number;
  postId: string;
  mark: boolean;
} | null> {
  await view.ensureRooms();
  if (view.roomRefused.value) return null;
  const linked = hashPostId(view);
  if (linked) {
    const linkedPage = await view.gameStore.locatePostPage(linked);
    return linkedPage
      ? { page: linkedPage, postId: linked, mark: false }
      : null;
  }
  if (!view.auth.user) return null;
  const found = await view.gameStore.locateFirstUnreadPost(
    view.gameId.value,
    view.roomNum.value,
  );
  return found
    ? { page: found.page, postId: found.postId, mark: found.hasUnread }
    : null;
}

function openView(view: RoomView): Promise<void> {
  const { route } = view;
  const roomKey = `${view.gameId.value}/${view.roomNum.value}`;
  const entering = roomKey !== view.enteredRoom;
  view.enteredRoom = roomKey;
  view.locating.value = false;

  // A filtered view starts at its first page; the unread link names its own.
  if (
    entering &&
    !view.isFilteredView.value &&
    !route.query.number &&
    !route.query.scrollTo
  ) {
    view.locating.value = true;
    return view.runView(
      async () => ({ data: await locateOpening(view), error: null }),
      (opening) => {
        view.locating.value = false;
        if (opening) {
          view.pendingTarget = { postId: opening.postId, mark: opening.mark };
          if (opening.page > 1) {
            // So "Назад" and a reload come back; the change of page loads it.
            view.router.replace({
              name: "game-room",
              params: route.params,
              query: { ...route.query, number: String(opening.page) },
              hash: route.hash,
            });
            return;
          }
        }
        void loadView(view, entering);
      },
    );
  }

  return loadView(view, entering);
}

function loadView(view: RoomView, entering = false): Promise<void> {
  return view.runView(
    async () => {
      await view.ensureRooms();
      if (view.roomRefused.value) return { data: null, error: null };
      await view.gameStore.loadPostsByRoomNumber(
        view.gameId.value,
        view.roomNum.value,
        view.page.value,
        view.apiFilter.value,
      );
      // The posts, their wait and their failure stay in the store.
      return { data: null, error: null };
    },
    () => {
      if (!view.gameStore.postsError && !view.roomRefused.value)
        void settleView(view, entering);
    },
  );
}

async function settleView(view: RoomView, entering: boolean) {
  const linked = entering ? hashPostId(view) : null;
  const target =
    view.pendingTarget ?? (linked ? { postId: linked, mark: false } : null);
  view.pendingTarget = null;

  if (view.isFilteredView.value) {
    leaveFilterForHashedPost(view);
    return;
  }
  if (target) await revealPost(view, target.postId, target.mark);
  markRoomRead(view);
}

// The mark goes on the card, whose own fill would hide a tint around it. A
// room gone during the wait finds nothing of its own by the id any more.
async function revealPost(view: RoomView, postId: string, mark: boolean) {
  await nextTick();
  if (view.gone) return;
  await new Promise((resolve) => requestAnimationFrame(resolve));
  if (view.gone) return;
  const article = document.getElementById(`post-${postId}`);
  if (!article) return;
  const container = getScrollContainer();
  if (container) {
    const offset =
      article.getBoundingClientRect().top -
      container.getBoundingClientRect().top;
    container.scrollTo({ top: container.scrollTop + offset - REVEAL_GAP });
  } else {
    article.scrollIntoView({ block: "start" });
  }
  if (mark) markArrival(article.querySelector(".post-card") ?? article);
}

// A filtered view or an empty room is not reading it. Once per room per visit.
async function markRoomRead(view: RoomView) {
  const id = view.gameStore.currentRoom?.id;
  if (
    !id ||
    !view.auth.user ||
    view.isFilteredView.value ||
    !view.gameStore.posts.length
  )
    return;
  if (view.markedRoomId === id) return;
  view.markedRoomId = id;
  await postApi.markRoomAsRead(id);
}

// In a filtered view the post a hash names stands on another page of its room.
function leaveFilterForHashedPost(view: RoomView) {
  const { route } = view;
  if (!view.isFilteredView.value) return;
  const id = hashPostId(view);
  const post = id ? view.gameStore.posts.find((p) => p.id === id) : undefined;
  if (!id || !post?.number) return;

  const ownPage = view.gameStore.pageOfPostNumber(post.number);
  const query: Record<string, string> = {};
  if (ownPage > 1) query.number = String(ownPage);
  // The review a link names stays named: the post opens it.
  if (typeof route.query.review === "string") query.review = route.query.review;
  view.pendingTarget = { postId: id, mark: false };
  view.router.replace({
    name: "game-room",
    params: route.params,
    query,
    hash: route.hash,
  });
}

/**
 * Opening the room of posts: where it opens, the page the address names, the
 * post it brings up and the mark of reading.
 */
export function useRoomOpening(options: RoomOpeningOptions) {
  const view: RoomView = {
    ...options,
    route: useRoute(),
    router: useRouter(),
    gameStore: useGameDetailsStore(),
    auth: useAuthStore(),
    runView: useGuardedRequest({ message: "Не удалось открыть комнату" }).run,
    locating: ref(false),
    enteredRoom: null,
    markedRoomId: null,
    pendingTarget: null,
    gone: false,
  };
  onScopeDispose(() => (view.gone = true));

  // After the render, so the shell's reset on a change of game runs first.
  watch(
    () =>
      JSON.stringify([
        view.gameId.value,
        view.roomNum.value,
        view.page.value,
        view.apiFilter.value,
      ]),
    () => openView(view),
    { immediate: true, flush: "post" },
  );

  watch(
    () => view.route.hash,
    () => leaveFilterForHashedPost(view),
  );

  return {
    locating: view.locating,
    reloadView: () => loadView(view),
  };
}
