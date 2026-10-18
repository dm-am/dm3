/**
 * @vitest-environment jsdom
 */

/**
 * The pencil and the trash of a game review repeat what the server decides.
 * GameReviewIntentionResolver admits Edit to the author alone and Delete to the
 * author or to SeniorModerator and above; GameReviewService closes the author's
 * edit window a day after publication and lets an admin past it. An icon the
 * server would refuse is a promise the page cannot keep, and one it would allow
 * but the page hides is a right nobody can use, so the ladder is walked whole.
 *
 * The rest of the file is the two actions themselves: the edit goes to the
 * review's own address with the text the editor holds and hands the answer up,
 * and the delete asks first, then waits in the question for the server.
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import GameReviewEditable from "./GameReviewEditable.vue";

const { getGameReviewForEdit, updateGameReview, deleteGameReview, notify } =
  vi.hoisted(() => ({
    getGameReviewForEdit: vi.fn(),
    updateGameReview: vi.fn(),
    deleteGameReview: vi.fn(),
    notify: vi.fn(),
  }));

// Only the three endpoints are replaced; the card and the stores the module
// exports stay real.
vi.mock("@/entities/game", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/game")>()),
  gameApi: { getGameReviewForEdit, updateGameReview, deleteGameReview },
}));

vi.mock("@/shared/lib/errors", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/lib/errors")>()),
  notifyFailure: notify,
}));

const AUTHOR_ID = "u-author";
const STRANGER_ID = "u-stranger";

const hoursAgo = (hours: number) =>
  new Date(Date.now() - hours * 3_600_000).toISOString();

const review = (createdUtc: string) =>
  ({
    id: "r-1",
    gameId: "g-guid",
    gameTitle: "Песочница",
    author: { id: AUTHOR_ID, username: "Solohin" },
    text: "<p>Отличная игра</p>",
    createdUtc,
  }) as unknown as GameReview;

const FRESH = review(hoursAgo(1));
/** An hour past the day the author has to correct the text. */
const STALE = review(hoursAgo(25));

const viewer = (id: string, role: UserRole) =>
  ({ id, username: id, role }) as unknown as User;

const PENCIL = 'button[aria-label="Редактировать"]';
const TRASH = 'button[aria-label="Удалить"]';

/**
 * The card is stubbed down to its #actions slot, the corner the icons stand
 * in, and the editor down to a field: the real ones drag in the bubble, the
 * router and tiptap, and this file is about who is offered what.
 */
const render = (shown: GameReview = FRESH) =>
  mount(GameReviewEditable, {
    props: { review: shown, showGame: false },
    global: {
      stubs: {
        GameReviewCard: {
          template: '<div class="card-stub"><slot name="actions" /></div>',
        },
        BBCodeEditor: {
          props: ["modelValue"],
          emits: ["update:modelValue", "submit"],
          template:
            '<textarea :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
        },
      },
    },
  });

/** The icons a viewer is offered, by the name a screen reader reads. */
const offered = (shown: GameReview = FRESH) => {
  const wrapper = render(shown);
  return {
    edit: wrapper.find(PENCIL).exists(),
    delete: wrapper.find(TRASH).exists(),
  };
};

/** A part of the delete question, which stands in <body> and not in the card. */
const inQuestion = (selector: string) => {
  const found = document.body.querySelector<HTMLElement>(
    `.dialog-container ${selector}`,
  );
  if (!found) throw new Error(`${selector} is not in the question`);
  return found;
};

// A question left open by one test would answer the next one's clicks.
enableAutoUnmount(afterEach);

beforeEach(() => {
  setActivePinia(createPinia());
  vi.clearAllMocks();
});

describe("GameReviewEditable access", () => {
  it("offers nothing to a guest", () => {
    useAuthStore().user = null;
    expect(offered()).toEqual({ edit: false, delete: false });
  });

  it.each([UserRole.RegularUser, UserRole.Mentor, UserRole.Moderator])(
    "offers nothing to a stranger with role %s",
    (role) => {
      useAuthStore().user = viewer(STRANGER_ID, role);
      expect(offered()).toEqual({ edit: false, delete: false });
    },
  );

  it.each([UserRole.SeniorModerator, UserRole.Admin])(
    "offers %s who did not write it the trash and not the pencil",
    (role) => {
      // A moderator does not rewrite somebody else's opinion; taking it down
      // is the moderation tool.
      useAuthStore().user = viewer(STRANGER_ID, role);
      expect(offered()).toEqual({ edit: false, delete: true });
    },
  );

  it("offers the author both within the day", () => {
    useAuthStore().user = viewer(AUTHOR_ID, UserRole.RegularUser);
    expect(offered(FRESH)).toEqual({ edit: true, delete: true });
  });

  it("takes the pencil from the author once the day is out", () => {
    useAuthStore().user = viewer(AUTHOR_ID, UserRole.RegularUser);
    expect(offered(STALE)).toEqual({ edit: false, delete: true });
  });

  it("lets an admin past the day on their own review", () => {
    useAuthStore().user = viewer(AUTHOR_ID, UserRole.Admin);
    expect(offered(STALE)).toEqual({ edit: true, delete: true });
  });
});

describe("GameReviewEditable editing", () => {
  beforeEach(() => {
    useAuthStore().user = viewer(AUTHOR_ID, UserRole.RegularUser);
  });

  it("opens the editor on the source and saves to the review's address", async () => {
    const source = "<p><strong>Песочница</strong> без рельсов</p>";
    getGameReviewForEdit.mockResolvedValue({
      data: { resource: { ...FRESH, text: source } },
      error: null,
    });
    const answer = { ...FRESH, text: "<p>Переписано целиком</p>" };
    updateGameReview.mockResolvedValue({
      data: { resource: answer },
      error: null,
    });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();

    expect(getGameReviewForEdit).toHaveBeenCalledWith("g-guid", "r-1");
    const field = wrapper.find("textarea");
    // The author's source, not the display text the card was showing.
    expect((field.element as HTMLTextAreaElement).value).toBe(
      htmlToBbcode(source),
    );
    expect(htmlToBbcode(source)).not.toBe(htmlToBbcode(FRESH.text));

    await field.setValue("Переписано целиком");
    await wrapper.find("button.save-btn").trigger("click");
    await flushPromises();

    expect(updateGameReview).toHaveBeenCalledWith("g-guid", "r-1", {
      text: "Переписано целиком",
    });
    // The page swaps the row for what the server answered.
    expect(wrapper.emitted("updated")).toEqual([[answer]]);
    expect(wrapper.find("textarea").exists()).toBe(false);
  });

  it("holds a text shorter than the server keeps", async () => {
    getGameReviewForEdit.mockResolvedValue({ data: null, error: null });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();
    await wrapper.find("textarea").setValue("Коротко");

    const save = wrapper.find("button.save-btn");
    expect(save.attributes("disabled")).toBeDefined();
    await save.trigger("click");
    expect(updateGameReview).not.toHaveBeenCalled();
  });

  it("keeps the editor and the text when the server refuses", async () => {
    getGameReviewForEdit.mockResolvedValue({ data: null, error: null });
    const refusal = {
      type: "",
      title: "Рецензию можно править в течение суток после публикации",
      status: 403,
      traceId: "t",
    };
    updateGameReview.mockResolvedValue({ data: null, error: refusal });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();
    await wrapper.find("textarea").setValue("Длинный новый текст рецензии");
    await wrapper.find("button.save-btn").trigger("click");
    await flushPromises();

    // Under the buttons of the editor, in the server's words; no plate.
    expect(wrapper.get(".edit-refusal").text()).toBe(
      "Рецензию можно править в течение суток после публикации",
    );
    expect(notify).not.toHaveBeenCalled();
    expect(
      (wrapper.find("textarea").element as HTMLTextAreaElement).value,
    ).toBe("Длинный новый текст рецензии");
    expect(wrapper.emitted("updated")).toBeUndefined();
  });

  it("asks the server for nothing when the edit is cancelled", async () => {
    getGameReviewForEdit.mockResolvedValue({ data: null, error: null });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();
    await wrapper.find("textarea").setValue("Передумал переписывать");
    await wrapper.find("textarea").trigger("keydown", { key: "Escape" });

    expect(updateGameReview).not.toHaveBeenCalled();
    expect(wrapper.find("textarea").exists()).toBe(false);
    expect(wrapper.find(PENCIL).exists()).toBe(true);
  });
});

describe("GameReviewEditable deleting", () => {
  beforeEach(() => {
    useAuthStore().user = viewer(STRANGER_ID, UserRole.SeniorModerator);
  });

  it("asks before it deletes, and reports the review gone", async () => {
    deleteGameReview.mockResolvedValue({ data: undefined, error: null });
    const wrapper = render();

    await wrapper.find(TRASH).trigger("click");

    // Nothing is gone yet: the window is the question, not the answer.
    expect(deleteGameReview).not.toHaveBeenCalled();
    const dialog = wrapper.findComponent(ConfirmDialog);
    expect(dialog.props("show")).toBe(true);

    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    // The question prints a refusal in itself, so the interceptor stays quiet.
    expect(deleteGameReview).toHaveBeenCalledWith("g-guid", "r-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.emitted("deleted")).toEqual([["r-1"]]);
    expect(dialog.props("show")).toBe(false);
  });

  it("says a refused delete in the question and keeps the review", async () => {
    deleteGameReview.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = render();

    await wrapper.find(TRASH).trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(inQuestion(".dialog-refusal").textContent?.trim()).toBe(
      "Не удалось удалить рецензию",
    );
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(true);
    expect(wrapper.emitted("deleted")).toBeUndefined();
  });
});
