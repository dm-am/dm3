/** The data and the mount of the post specs; a spec that mounts a post stubs ResizeObserver once. */
import { vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createMemoryHistory, createRouter } from "vue-router";
import { PostReviewItem } from "@/entities/game";
import type { Post, PostReview } from "@/entities/game";
import {
  createMockGameRef,
  createMockPost,
} from "@/entities/game/model/store.fixtures";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { useAuthStore } from "@/shared/stores";
import GamePost from "./GamePost.vue";

/** Geometry is not what these specs read: the observer observes nothing. */
export function stubResizeObserver() {
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
}

/** Newest first, the order the reviews endpoint answers in. */
export const REVIEWS = [
  {
    id: "r-new",
    sign: "Positive",
    text: "<p>Свежий отзыв</p>",
    createdUtc: "2026-07-26T16:00:00",
    author: { username: "Vasya", role: "Player" },
  },
  {
    id: "r-old",
    sign: "Neutral",
    text: "<p>Старый отзыв</p>",
    createdUtc: "2026-07-25T16:00:00",
    author: { username: "Petya", role: "Player" },
  },
] as unknown as PostReview[];

export const post: Post = {
  ...createMockPost("p-1", "<p>Текст поста</p>"),
  createdUtc: "2026-07-26T15:00:00",
  reviewCount: 2,
  rating: 1,
  author: createMockUser("u-author", "Author"),
  room: {
    id: asServed("room-guid"),
    roomNumber: asServed(2),
    title: "Комната",
    unreadPostsCount: 0,
    // No id, as the post lists serve it: the name of a character is no link.
    game: {
      ...createMockGameRef("", "Игра"),
      publicId: asServed("abcde"),
    },
  },
};

/** The same post as the room page answers with: no `room`, that IS the page. */
export const roomPost: Post = { ...post, room: undefined };

/** The post as the server answers it to a viewer. */
export const answered = (
  subject: Post,
  canReview = true,
  gameId = "game-guid",
): Post => ({
  ...subject,
  gameId: asServed(gameId),
  viewer: asServed({ canReview, canEdit: false }),
});

const ROUTES = [
  { path: "/", name: "home", component: { template: "<div />" } },
  // The author's name inside the post and inside every review is a link.
  {
    path: "/users/:username",
    name: "profile",
    component: { template: "<div />" },
  },
  {
    path: "/game/:id/rooms/:num",
    name: "game-room",
    component: { template: "<div />" },
  },
];

/** The room page a post of the fixtures lives on. */
export const ROOM = "/game/abcde/rooms/2";

/** A router standing on `location`. */
export async function routerAt(location: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();
  return router;
}

/** Who reads the post: a login, or the fields of the signed-in user. */
type Viewer = string | Record<string, unknown> | null;

/** Mounts the post as it stands on `location` for `viewer`, its reviews answerable. */
export async function render(
  location: string,
  subject: Post = post,
  {
    viewer = null,
    props = {},
  }: { viewer?: Viewer; props?: Record<string, unknown> } = {},
) {
  const router = await routerAt(location);
  const pinia = createPinia();
  setActivePinia(pinia);
  if (viewer) {
    useAuthStore().user = (
      typeof viewer === "string"
        ? { username: viewer, role: "Player" }
        : { role: "Player", ...viewer }
    ) as never;
  }

  const wrapper = mount(GamePost, {
    props: { post: subject, number: 7, ...props },
    global: { plugins: [pinia, router] },
  });
  await flushPromises();
  return wrapper;
}

export type PostWrapper = Awaited<ReturnType<typeof render>>;

export const reviewsOf = (wrapper: PostWrapper) =>
  wrapper.findAllComponents(PostReviewItem);

/** A post of the viewer's own, inside the edit window, on the room page. */
export function renderAs(
  viewer: string | null,
  over: Record<string, unknown> = {},
  editable = true,
  role = "Player",
) {
  const subject: Post = {
    ...roomPost,
    createdUtc: new Date().toISOString(),
    ...over,
  };
  return render(ROOM, subject, {
    viewer: viewer && { username: viewer, role },
    props: { editable },
  });
}

export const lineOf = (wrapper: PostWrapper) => wrapper.find(".post-edit-line");

/** The text of an element as it is copied: its runs of space made one. */
export const copied = (element: Element) =>
  (element.textContent ?? "").replace(/\s+/g, " ").trim();
