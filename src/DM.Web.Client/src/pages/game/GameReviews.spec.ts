/**
 * @vitest-environment jsdom
 */

/**
 * The reviews page had a page's data and none of a page's frame.
 *
 * The form was a bare <textarea> with a bare button while every other composer
 * on the site is the BBCode editor with a draft that outlives a closed tab; a
 * failed load left the page silently empty; and a guest saw no form and no
 * reason for its absence. The row heading joined author and date with an em
 * dash, which is out of interface copy.
 *
 * Eligibility is the server's word, and the form is offered by it
 * (game.viewer.canReview): a newbie and a reader with no post in the game
 * typed a review and were refused after "Отправить". The guest's invitation
 * and the author's own review on the page are asserted below with it.
 *
 * Each row is the editable card, and what it hands up lands in the list: an
 * edit replaces the row, a delete removes it and brings the form back to the
 * author who can now write a new one.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore, type Game } from "@/entities/game";
import { useAuthStore } from "@/shared/stores/auth";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LoginPrompt } from "@/features/auth";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import GameReviews from "./GameReviews.vue";

const { getGameReviews, createGameReview, getGame } = vi.hoisted(() => ({
  getGameReviews: vi.fn(),
  createGameReview: vi.fn(),
  getGame: vi.fn(),
}));

// Only the endpoint pair is replaced; the stores and types the page imports
// from the same module stay real. The details store reads the game through
// the real API object, and that one read is answered here.
vi.mock("@/entities/game", async (importOriginal) => {
  const real = await importOriginal<typeof import("@/entities/game")>();
  vi.spyOn(real.gameApi, "getGame").mockImplementation(getGame);
  return { ...real, gameApi: { getGameReviews, createGameReview } };
});

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" }, query: {} }),
}));

/** The fields of a review row the page reads. */
function review(username: string) {
  return {
    id: `review-of-${username}`,
    gameId: "the-game-guid",
    author: { username },
    text: "<p>Отличная игра</p>",
    createdUtc: "2026-08-01T10:00:00Z",
  };
}

/** The game as the server answers it to a viewer who may or may not review it. */
const theGame = (blacklisted: boolean, canReview: boolean) =>
  ({
    id: "the-game-guid",
    publicId: "the-game",
    viewerBlacklisted: blacklisted,
    viewer: { canComment: true, canReview },
  }) as unknown as Game;

async function render(
  viewer: { username: string } | null,
  reviews: ReturnType<typeof review>[] = [],
  failed = false,
  blacklisted = false,
  // The server says no to whoever has a review there already.
  canReview = !reviews.some((r) => r.author.username === viewer?.username),
  stubs: Record<string, object> = {},
) {
  getGameReviews.mockResolvedValue(
    failed
      ? { data: null, error: { status: 500 } }
      : {
          data: {
            resources: reviews,
            paging: {
              current: 1,
              pages: 1,
              skip: 0,
              take: 20,
              total: reviews.length,
            },
          },
          error: null,
        },
  );

  const pinia = createPinia();
  setActivePinia(pinia);
  useGameDetailsStore().game = theGame(blacklisted, canReview);
  useAuthStore().user = viewer as never;

  // Shallow, with the slots of the stubs drawn: the lines the page says in
  // place of the form are the words of its grey-line stubs.
  const wrapper = mount(GameReviews, {
    global: { plugins: [pinia], renderStubDefaultSlot: true, stubs },
    shallow: true,
  });
  await flushPromises();
  return wrapper;
}

describe("GameReviews", () => {
  // The calls are counted below, so every test starts from none.
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("gives a signed-in reader the site's editor with a draft of its own", async () => {
    const wrapper = await render({ username: "reader" });

    const editor = wrapper.findComponent(BBCodeEditor);
    expect(editor.exists()).toBe(true);
    // Built, never spelled — and keyed by the game, so two games do not share
    // one draft (draftKeys.spec.ts holds the rule tree-wide).
    expect(editor.props("draftKey")).toBe(
      composerDraftKey("game", "review", "the-game-guid"),
    );
  });

  it("tells a guest why there is no form", async () => {
    const wrapper = await render(null);

    expect(wrapper.findComponent(LoginPrompt).exists()).toBe(true);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
  });

  it("answers a failed load with the shared error state", async () => {
    const wrapper = await render({ username: "reader" }, [], true);

    expect(wrapper.findComponent(ErrorState).exists()).toBe(true);
  });

  it("draws a review as the editable bubble card, without naming the game", async () => {
    // The card is the one a recommendation uses, with the pencil and the trash
    // in its corner for whoever may act. `showGame: false` because every row
    // here is about the game the reader is already on — the same reason the
    // site testimonials gallery names no recipient.
    const wrapper = await render({ username: "reader" }, [review("critic")]);

    const cards = wrapper.findAllComponents({ name: "GameReviewEditable" });
    expect(cards).toHaveLength(1);
    expect(cards[0].props("review")).toMatchObject({ id: "review-of-critic" });
    expect(cards[0].props("showGame")).toBe(false);
  });

  it("puts an edited review in its row without a refetch", async () => {
    const wrapper = await render({ username: "critic" }, [review("critic")]);
    const card = wrapper.findComponent({ name: "GameReviewEditable" });

    card.vm.$emit("updated", {
      ...review("critic"),
      text: "<p>Переписано</p>",
    });
    await flushPromises();

    expect(getGameReviews).toHaveBeenCalledTimes(1);
    expect(
      wrapper.findComponent({ name: "GameReviewEditable" }).props("review"),
    ).toMatchObject({ id: "review-of-critic", text: "<p>Переписано</p>" });
  });

  it("drops a deleted review and gives its author the form back", async () => {
    const wrapper = await render({ username: "critic" }, [review("critic")]);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);

    // The game read again answers that the author may write one now.
    getGame.mockResolvedValue({
      data: { resource: theGame(false, true) },
      error: null,
    });
    // The refetch that follows answers with the list as the server now has it.
    getGameReviews.mockResolvedValue({
      data: {
        resources: [],
        paging: { current: 1, pages: 0, skip: 0, take: 20, total: 0 },
      },
      error: null,
    });
    wrapper
      .findComponent({ name: "GameReviewEditable" })
      .vm.$emit("deleted", "review-of-critic");
    await flushPromises();

    expect(getGameReviews).toHaveBeenCalledTimes(2);
    expect(getGame).toHaveBeenCalledWith("the-game-guid");
    expect(
      wrapper.findAllComponents({ name: "GameReviewEditable" }),
    ).toHaveLength(0);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
  });

  // A newbie, and a reader with no post in the game: the server refuses their
  // review, so the page has no form for them and no sentence in its place.
  it("offers no form to a reader the server would refuse", async () => {
    const wrapper = await render(
      { username: "newbie" },
      [review("critic")],
      false,
      false,
      false,
    );

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find(".review-form").exists()).toBe(false);
    expect(wrapper.find(".form-separator").exists()).toBe(false);
    expect(wrapper.findComponent(LoginPrompt).exists()).toBe(false);
  });

  // The review sent may land on another page of the list: the game read again
  // is what takes the form away, not the list.
  it("asks again after a review is sent, and the form goes", async () => {
    // The editor drawn as a stub that can be cleared, as the page clears it.
    const wrapper = await render(
      { username: "reader" },
      [review("critic")],
      false,
      false,
      true,
      {
        BBCodeEditor: {
          name: "BBCodeEditor",
          emits: ["update:modelValue", "submit"],
          template: '<div class="editor-stub" />',
          methods: { clear: () => undefined },
        },
      },
    );
    createGameReview.mockResolvedValue({ data: {}, error: null });
    getGame.mockResolvedValue({
      data: { resource: theGame(false, false) },
      error: null,
    });

    const editor = wrapper.findComponent({ name: "BBCodeEditor" });
    editor.vm.$emit("update:modelValue", "Рецензия на всю игру");
    await flushPromises();
    editor.vm.$emit("submit");
    await flushPromises();

    expect(createGameReview).toHaveBeenCalledTimes(1);
    expect(getGame).toHaveBeenCalledWith("the-game-guid");
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.find(".review-form").exists()).toBe(false);
  });

  // The own review standing in the list is the whole answer: no form, and no
  // sentence under it that the review was published, nor the rule above an
  // empty place.
  it("keeps the form off the screen for a reader who already reviewed", async () => {
    const wrapper = await render({ username: "critic" }, [review("critic")]);

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find(".review-form").exists()).toBe(false);
    expect(wrapper.find(".form-separator").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("уже опубликована");
  });

  /**
   * A review is feedback, and the game's blacklist does not close it: the
   * form is the form everybody gets. It used to give way to a sentence about
   * the list.
   */
  it("offers the form to the viewer on the game's list as to anybody", async () => {
    const wrapper = await render(
      { username: "reader" },
      [review("critic")],
      false,
      true,
    );

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
    expect(wrapper.get(".review-form").text()).not.toContain("черном списке");
  });
});
