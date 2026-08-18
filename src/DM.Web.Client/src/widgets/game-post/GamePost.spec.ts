/**
 * @vitest-environment jsdom
 */

/**
 * A review has no page of its own, and the number beside it is its address.
 * That address used to be the current `window.location.pathname` plus a
 * `#review-` hash nothing in the client reads: on the home page it pointed at
 * the home page, and anywhere it landed the reader on a post whose reviews
 * block is collapsed and unfetched, so the review it was made for was not on
 * the screen at all.
 *
 * Where it leads now is the room the post lives in, anchored at the post and
 * naming the review. What the widget does when it is handed that address back
 * is open the block and mark the review named. The two halves are one
 * contract, so they are checked through one router: what the link addresses
 * is what the other half is asked to honour.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
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
  },
  {
    id: "r-old",
    sign: "Neutral",
    text: "<p>Старый отзыв</p>",
    createdUtc: "2026-07-25T16:00:00",
    author: { username: "Petya", role: "Player" },
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

/** The same post as the room page answers with: no `room`, that IS the page. */
const roomPost = { ...post, room: undefined } as unknown as Post;

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
async function render(location: string, subject: Post = post) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();

  const wrapper = mount(GamePost, {
    props: { post: subject, number: 7 },
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

  afterEach(() => {
    // Since vitest 4, respying an already-spied method returns the same spy
    // with its call history intact, so without a restore the "not called"
    // assertion below would read the previous tests' calls.
    vi.restoreAllMocks();
  });

  it("points every review at the room, not at the page the post is drawn on", async () => {
    const wrapper = await render("/");

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    // Read off the rendered href rather than the prop: the route object and
    // the address it resolves to are the same claim only if the router agrees.
    expect(
      wrapper.findAll(".review-anchor").map((a) => a.attributes("href")),
    ).toEqual([
      "/game/abcde/rooms/2?review=r-new#post-p-1",
      "/game/abcde/rooms/2?review=r-old#post-p-1",
    ]);
  });

  it("keeps the page of the room in the address when drawn on that page", async () => {
    // On the room page `post.room` is redundant and not sent, so the address
    // is built from the current route. Its query carries the page number, and
    // a link that dropped it would point at page one of the room.
    const wrapper = await render("/game/abcde/rooms/2?number=3", roomPost);

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    expect(wrapper.get(".review-anchor").attributes("href")).toBe(
      "/game/abcde/rooms/2?number=3&review=r-new#post-p-1",
    );
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
