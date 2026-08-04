/**
 * @vitest-environment jsdom
 */

/**
 * A review has no page of its own, and the number beside it copies an address.
 * That address used to be the current `window.location.pathname` plus a
 * `#review-` hash nothing in the client reads: copied on the home page it
 * pointed at the home page, and copied anywhere it landed the reader on a post
 * whose reviews block is collapsed and unfetched, so the review it was made
 * for was not on the screen at all.
 *
 * What it copies now is the room the post lives in, anchored at the post and
 * naming the review. What the widget does when it is handed that address back
 * is open the block and mark the review named. The two halves are one
 * contract, so they are checked through one router: what the control writes
 * into the clipboard is what the other half is asked to honour.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { createMemoryHistory, createRouter } from "vue-router";
import { gameApi, PostReviewItem } from "@/entities/game";
import type { Post, PostReview } from "@/entities/game";
import GamePost from "./GamePost.vue";

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue"],
  },
}));

// The post sizes its meta column with a ResizeObserver, which jsdom has not.
// Geometry is not what this spec reads.
vi.stubGlobal(
  "ResizeObserver",
  class {
    observe() {}
    disconnect() {}
  },
);

/** Newest first, the order the reviews endpoint answers in. */
const REVIEWS = [
  {
    id: "r-new",
    sign: "Positive",
    text: "<p>Свежий отзыв</p>",
    createdUtc: "2026-07-26T16:00:00",
    author: { username: "Vasya", role: "Player" },
    likes: [],
  },
  {
    id: "r-old",
    sign: "Neutral",
    text: "<p>Старый отзыв</p>",
    createdUtc: "2026-07-25T16:00:00",
    author: { username: "Petya", role: "Player" },
    likes: [],
  },
] as unknown as PostReview[];

const post = {
  id: "p-1",
  createdUtc: "2026-07-26T15:00:00",
  gameText: "<p>Текст поста</p>",
  reviewCount: 2,
  rating: 1,
  author: { username: "Author", role: "Player" },
  room: { id: "room-guid", roomNumber: 2, game: { publicId: "abcde" } },
} as unknown as Post;

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

/** Mounts the post as it stands on `location`, with its reviews answerable. */
async function render(location: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();

  const wrapper = mount(GamePost, {
    props: { post, number: 7 },
    global: { plugins: [createPinia(), router] },
  });
  await flushPromises();
  return wrapper;
}

const reviewsOf = (wrapper: Awaited<ReturnType<typeof render>>) =>
  wrapper.findAllComponents(PostReviewItem);

describe("GamePost review permalinks", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  it("hands every review the room address, not the page the post is drawn on", async () => {
    const wrapper = await render("/");

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    expect(reviewsOf(wrapper).map((item) => item.props("permalink"))).toEqual([
      `${window.location.origin}/game/abcde/rooms/2?review=r-new#post-p-1`,
      `${window.location.origin}/game/abcde/rooms/2?review=r-old#post-p-1`,
    ]);
  });

  it("opens the reviews on arrival and marks the one the address names", async () => {
    const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-1");

    expect(wrapper.get(".reviews-collapse").classes()).toContain("expanded");
    expect(reviewsOf(wrapper).map((item) => item.props("highlight"))).toEqual([
      false,
      true,
    ]);
  });

  it("leaves the posts the address does not name collapsed", async () => {
    // Every post of the room reads the same query; the anchor is what tells
    // them apart, so a link into a neighbour must not open this one.
    const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-9");

    expect(wrapper.get(".reviews-collapse").classes()).not.toContain(
      "expanded",
    );
    expect(gameApi.getPostReviews).not.toHaveBeenCalled();
  });
});
