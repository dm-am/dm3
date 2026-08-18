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
 * Eligibility stays the server's word. The two gates the page can answer by
 * itself — a guest, and an author whose review is already on the page — are the
 * only ones drawn here, and both are asserted below.
 */
import { describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore, type Game } from "@/entities/game";
import { useAuthStore } from "@/shared/stores/auth";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LoginPrompt } from "@/features/auth";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import GameReviews from "./GameReviews.vue";

const { getGameReviews, createGameReview } = vi.hoisted(() => ({
  getGameReviews: vi.fn(),
  createGameReview: vi.fn(),
}));

// Only the endpoint pair is replaced; the stores and types the page imports
// from the same module stay real.
vi.mock("@/entities/game", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/game")>()),
  gameApi: { getGameReviews, createGameReview },
}));

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

async function render(
  viewer: { username: string } | null,
  reviews: ReturnType<typeof review>[] = [],
  failed = false,
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
  useGameDetailsStore().game = {
    id: "the-game-guid",
    publicId: "the-game",
  } as unknown as Game;
  useAuthStore().user = viewer as never;

  const wrapper = mount(GameReviews, {
    global: { plugins: [pinia] },
    shallow: true,
  });
  await flushPromises();
  return wrapper;
}

describe("GameReviews", () => {
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

  it("draws a review as the shared bubble card, without naming the game", async () => {
    // The card is the one a recommendation uses. `showGame: false` because
    // every row here is about the game the reader is already on — the same
    // reason the site testimonials gallery names no recipient.
    const wrapper = await render({ username: "reader" }, [review("critic")]);

    const cards = wrapper.findAllComponents({ name: "GameReviewCard" });
    expect(cards).toHaveLength(1);
    expect(cards[0].props("review")).toMatchObject({ id: "review-of-critic" });
    expect(cards[0].props("showGame")).toBe(false);
  });

  it("keeps the form off the screen for a reader who already reviewed", async () => {
    const wrapper = await render({ username: "critic" }, [review("critic")]);

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
  });
});
