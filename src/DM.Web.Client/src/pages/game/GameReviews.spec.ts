/**
 * @vitest-environment jsdom
 */

// The form is offered by the server's answer (game.viewer.canReview), and
// what an editable card hands up lands in the list.
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore, GameStatus, type Game } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LoginPrompt } from "@/features/auth";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import GameReviews from "./GameReviews.vue";

const { getGameReviews, createGameReview, getGame, query } = vi.hoisted(() => ({
  getGameReviews: vi.fn(),
  createGameReview: vi.fn(),
  getGame: vi.fn(),
  query: {} as Record<string, string>,
}));

// The endpoints alone; the stores and types of the module stay real.
vi.mock("@/entities/game", async (importOriginal) => {
  const real = await importOriginal<typeof import("@/entities/game")>();
  vi.spyOn(real.gameApi, "getGame").mockImplementation(getGame);
  return { ...real, gameReviewApi: { getGameReviews, createGameReview } };
});

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" }, query }),
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

/** The game as the server answers it to a viewer who may or may not review it; closed unless told. */
const theGame = (
  blacklisted: boolean,
  canReview: boolean,
  status: GameStatus = GameStatus.Closed,
) =>
  ({
    id: "the-game-guid",
    publicId: "the-game",
    status,
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
  status: GameStatus = GameStatus.Closed,
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
  useGameDetailsStore().game = theGame(blacklisted, canReview, status);
  useAuthStore().user = viewer as never;

  const wrapper = mount(GameReviews, {
    global: { plugins: [pinia], renderStubDefaultSlot: true, stubs },
    shallow: true,
  });
  await flushPromises();
  return wrapper;
}

describe("GameReviews", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    delete query.number;
  });

  it.each([
    ["3", 3],
    ["nonsense", 1],
  ])("asks for the page the address names as %s", async (number, page) => {
    query.number = number;
    await render({ username: "reader" });

    expect(getGameReviews).toHaveBeenCalledWith("the-game-guid", {
      number: page,
    });
  });

  it("gives a signed-in reader the site's editor with a draft of its own", async () => {
    const wrapper = await render({ username: "reader" });

    const editor = wrapper.findComponent(BBCodeEditor);
    expect(editor.exists()).toBe(true);
    // Keyed by the game, so two games do not share one draft.
    expect(editor.props("draftKey")).toBe(
      composerDraftKey("game", "review", "the-game-guid"),
    );
  });

  it("lets a review of one character go and holds one of spaces", async () => {
    const wrapper = await render({ username: "reader" });
    const editor = wrapper.findComponent(BBCodeEditor);
    const send = () => wrapper.findComponent(Button).props("disabled");
    expect(editor.props("placeholder")).toBe("Напишите рецензию на игру");

    editor.vm.$emit("update:modelValue", "Я");
    await flushPromises();
    expect(send()).toBe(false);

    editor.vm.$emit("update:modelValue", "   ");
    await flushPromises();
    expect(send()).toBe(true);
  });

  it("tells a guest on a closed game why there is no form", async () => {
    const wrapper = await render(null);

    expect(wrapper.findComponent(LoginPrompt).exists()).toBe(true);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
  });

  // A review is written on a closed game alone: elsewhere a guest is not asked to sign in for one.
  it.each([GameStatus.Draft, GameStatus.Active])(
    "asks a guest for nothing on a game that is %s",
    async (status) => {
      const wrapper = await render(null, [], false, false, false, {}, status);

      expect(wrapper.findComponent(LoginPrompt).exists()).toBe(false);
      expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    },
  );

  it("answers a failed load with the shared error state", async () => {
    const wrapper = await render({ username: "reader" }, [], true);

    expect(wrapper.findComponent(ErrorState).exists()).toBe(true);
  });

  it("draws a review as the editable bubble card, without naming the game", async () => {
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

  // The sent review may land on another page; the game read again answers.
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

  it("keeps the form off the screen for a reader who already reviewed", async () => {
    const wrapper = await render({ username: "critic" }, [review("critic")]);

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find(".review-form").exists()).toBe(false);
    expect(wrapper.find(".form-separator").exists()).toBe(false);
  });

  // A review is feedback, and the game's blacklist does not close it.
  it("offers the form to the viewer on the game's list as to anybody", async () => {
    const wrapper = await render(
      { username: "reader" },
      [review("critic")],
      false,
      true,
    );

    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(true);
  });
});
