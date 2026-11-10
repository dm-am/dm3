/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import GameReviewEditable from "./GameReviewEditable.vue";

const { getGameReviewForEdit, updateGameReview, deleteGameReview, notify } =
  vi.hoisted(() => ({
    getGameReviewForEdit: vi.fn(),
    updateGameReview: vi.fn(),
    deleteGameReview: vi.fn(),
    notify: vi.fn(),
  }));

vi.mock("@/entities/game", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/game")>()),
  gameReviewApi: { getGameReviewForEdit, updateGameReview, deleteGameReview },
}));

vi.mock("@/shared/lib/errors", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/lib/errors")>()),
  notifyFailure: notify,
}));

const AUTHOR_ID = "u-author";

/** What the server answers the viewer may do with the review. */
type Offer = { canEdit: boolean; canDelete: boolean };

const review = (viewer: Offer) =>
  ({
    id: "r-1",
    gameId: "g-guid",
    gameTitle: "Песочница",
    author: { id: AUTHOR_ID, username: "Solohin" },
    text: "<p>Отличная игра</p>",
    createdUtc: "2026-08-01T10:00:00Z",
    viewer,
  }) as unknown as GameReview;

const FRESH = review({ canEdit: true, canDelete: true });

const reader = (id: string, role: UserRole): User => ({
  ...createMockUser(id, id),
  role,
});

const PENCIL = 'button[aria-label="Редактировать"]';
const TRASH = 'button[aria-label="Удалить"]';

// The card is stubbed down to its #actions slot and the editor to a field.
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

const offered = (shown: GameReview = FRESH) => {
  const wrapper = render(shown);
  return {
    edit: wrapper.find(PENCIL).exists(),
    delete: wrapper.find(TRASH).exists(),
  };
};

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
  it.each([
    { canEdit: false, canDelete: false },
    { canEdit: false, canDelete: true },
    { canEdit: true, canDelete: true },
  ])("offers what the server answers: %o", (offer) => {
    expect(offered(review(offer))).toEqual({
      edit: offer.canEdit,
      delete: offer.canDelete,
    });
  });

  // The window, the author and the role are the server's to count.
  it("offers its own admin author nothing the server did not answer", () => {
    useAuthStore().user = reader(AUTHOR_ID, UserRole.Admin);
    expect(offered(review({ canEdit: false, canDelete: false }))).toEqual({
      edit: false,
      delete: false,
    });
  });
});

describe("GameReviewEditable editing", () => {
  const saveButton = (wrapper: ReturnType<typeof render>) =>
    wrapper.findAll("button").find((b) => b.text() === "Сохранить")!;

  it("opens the editor on the source and saves to the review's address", async () => {
    // "&amp;" as typed: read as HTML, it would come back as "&".
    const source = "[b]Песочница[/b] без рельсов, Том &amp; Джерри";
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
    expect((field.element as HTMLTextAreaElement).value).toBe(source);

    await field.setValue("Переписано целиком");
    await wrapper
      .findAll("button")
      .find((b) => b.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();

    expect(updateGameReview).toHaveBeenCalledWith("g-guid", "r-1", {
      text: "Переписано целиком",
    });
    expect(wrapper.emitted("updated")).toEqual([[answer]]);
    expect(wrapper.find("textarea").exists()).toBe(false);
  });

  it("saves a text of one character", async () => {
    getGameReviewForEdit.mockResolvedValue({ data: null, error: null });
    updateGameReview.mockResolvedValue({
      data: { resource: FRESH },
      error: null,
    });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();
    expect(wrapper.find("textarea").attributes("placeholder")).toBe(
      "Текст рецензии",
    );
    await wrapper.find("textarea").setValue("Я");
    await saveButton(wrapper).trigger("click");
    await flushPromises();

    expect(updateGameReview).toHaveBeenCalledWith("g-guid", "r-1", {
      text: "Я",
    });
  });

  it("holds a text of nothing but spaces", async () => {
    getGameReviewForEdit.mockResolvedValue({ data: null, error: null });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();
    await wrapper.find("textarea").setValue("   ");

    expect(saveButton(wrapper).attributes("disabled")).toBeDefined();
    await saveButton(wrapper).trigger("click");
    expect(updateGameReview).not.toHaveBeenCalled();
  });

  // The card's text is drawn: an editor opened on it would save the drawing.
  it("opens no editor when the text as typed does not come, and says so", async () => {
    getGameReviewForEdit.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });

    const wrapper = render();
    await wrapper.find(PENCIL).trigger("click");
    await flushPromises();

    expect(wrapper.find("textarea").exists()).toBe(false);
    expect(wrapper.get(".edit-refusal").text()).toBe(
      "Не удалось загрузить рецензию",
    );
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
    await wrapper
      .findAll("button")
      .find((b) => b.text() === "Сохранить")!
      .trigger("click");
    await flushPromises();

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
  it("asks before it deletes, and reports the review gone", async () => {
    deleteGameReview.mockResolvedValue({ data: undefined, error: null });
    const wrapper = render();

    await wrapper.find(TRASH).trigger("click");

    expect(deleteGameReview).not.toHaveBeenCalled();
    const dialog = wrapper.findComponent(ConfirmDialog);
    expect(dialog.props("show")).toBe(true);

    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

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

/**
 * A review is a place a warning names. Whether the reader may warn its author is the page's to say (the
 * ranks), and the card offers the control by that word alone, beside the pencil and the trash.
 */
describe("GameReviewEditable warning", () => {
  const WARN = 'button[aria-label="Оставить предупреждение"]';
  const NOTHING = review({ canEdit: false, canDelete: false });

  const withWarn = (canWarn: boolean) =>
    mount(GameReviewEditable, {
      props: { review: NOTHING, showGame: false, canWarn },
      global: {
        stubs: {
          GameReviewCard: {
            template: '<div class="card-stub"><slot name="actions" /></div>',
          },
        },
      },
    });

  it("offers the warning where the page allows it, and hands the review up", async () => {
    const wrapper = withWarn(true);

    await wrapper.get(WARN).trigger("click");

    expect(wrapper.emitted("warn")).toEqual([[NOTHING]]);
  });

  it("offers no warning where the page does not allow it", () => {
    expect(withWarn(false).find(WARN).exists()).toBe(false);
  });
});
