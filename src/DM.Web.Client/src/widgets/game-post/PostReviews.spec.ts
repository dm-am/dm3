/**
 * @vitest-environment jsdom
 */

/** A review's address is the room anchored at the post; handed back, it opens and marks it. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { postApi } from "@/entities/game";
import type { Post } from "@/entities/game";
import {
  REVIEWS,
  ROOM,
  answered,
  render,
  reviewsOf,
  roomPost,
  stubResizeObserver,
} from "./GamePost.fixtures";

// A post left mounted keeps its reviews in the registry of "Развернуть все".
enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

describe("GamePost review permalinks", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    // A respied method keeps its call history: restore, or "not called" reads old calls.
    vi.restoreAllMocks();
  });

  it("points every review at the room, not at the page the post is drawn on", async () => {
    const wrapper = await render("/");

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    // The rendered href, not the prop: the two agree only if the router does.
    expect(
      wrapper.findAll(".review-anchor").map((a) => a.attributes("href")),
    ).toEqual([
      "/game/abcde/rooms/2?review=r-new#post-p-1",
      "/game/abcde/rooms/2?review=r-old#post-p-1",
    ]);
  });

  it("keeps the page of the room in the address when drawn on that page", async () => {
    // On the room page the address keeps the room's query, its page number.
    const wrapper = await render("/game/abcde/rooms/2?number=3", roomPost);

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    expect(wrapper.get(".review-anchor").attributes("href")).toBe(
      "/game/abcde/rooms/2?number=3&review=r-new#post-p-1",
    );
  });

  it("opens the reviews on arrival and marks the one the address names", async () => {
    const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-1");

    expect(wrapper.get(".reviews-collapse").classes()).toContain("open");
    expect(reviewsOf(wrapper).map((item) => item.props("highlight"))).toEqual([
      false,
      true,
    ]);
  });

  it("leaves the posts the address does not name collapsed", async () => {
    // The anchor tells the posts of a room apart: a link into a neighbour opens nothing.
    const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-9");

    expect(wrapper.get(".reviews-collapse").classes()).not.toContain("open");
    expect(postApi.getPostReviews).not.toHaveBeenCalled();
  });
});

/** The sum opens the reviews; for a reader who may rate, the "+0" opens the first form. */
describe("GamePost review sum", () => {
  const unrated = { ...roomPost, reviewCount: 0, rating: 0 } as unknown as Post;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const renderFor = (viewer: string | null, subject: Post) =>
    render(ROOM, subject, { viewer });

  it("opens the form of the first review from the +0 of a reader who may rate", async () => {
    const wrapper = await renderFor("Reader", answered(unrated));

    const sum = wrapper.get("button.rating-value");
    expect(sum.text()).toBe("+0");
    await sum.trigger("click");
    await flushPromises();

    expect(wrapper.get(".reviews-collapse").classes()).toContain("open");
    expect(wrapper.find(".review-form .review-input").exists()).toBe(true);
  });

  // The server says no to the author; a guest gets nothing even with an old yes.
  it("leaves the +0 plain text for a guest and for the author", async () => {
    for (const viewer of [null, "Author"]) {
      const wrapper = await renderFor(viewer, answered(unrated, !viewer));
      expect(wrapper.find("button.rating-value").exists()).toBe(false);
      expect(wrapper.get("span.rating-value").text()).toBe("+0");
      wrapper.unmount();
    }
  });

  it("keeps a negative sum a toggle in its colour", async () => {
    const wrapper = await renderFor(null, {
      ...roomPost,
      reviewCount: 2,
      rating: -2,
    } as unknown as Post);

    const sum = wrapper.get("button.rating-value");
    expect(sum.text()).toBe("-2");
    expect(sum.classes()).toContain("negative");
    await sum.trigger("click");
    await flushPromises();
    expect(wrapper.get(".reviews-collapse").classes()).toContain("open");
  });
});

/** Who may not rate a post gets the reviews and nothing more. */
describe("GamePost says no refusal under the reviews", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function openReviewsAs(
    viewer: Record<string, unknown>,
    canReview: boolean,
  ) {
    const wrapper = await render(ROOM, answered(roomPost, canReview), {
      viewer,
    });
    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();
    return wrapper;
  }

  // The server says no to both, and the page says nothing in place of the form.
  it.each([
    ["the author of the post", "Author"],
    ["a reader who has rated it", "Vasya"],
  ])("gives %s the reviews alone", async (_, username) => {
    const wrapper = await openReviewsAs({ username }, false);

    expect(wrapper.findAll(".reviews-section > *")).toHaveLength(
      REVIEWS.length,
    );
    expect(wrapper.find(".review-form").exists()).toBe(false);
  });

  // The single "=" is the whole answer: it shows the sign a newbie may give.
  it("offers a newbie the neutral sign without a word about the rest", async () => {
    const wrapper = await openReviewsAs(
      { username: "Reader", isNewbie: true },
      true,
    );

    expect(
      wrapper.findAll(".review-form .segment").map((s) => s.text()),
    ).toEqual(["="]);
  });
});
