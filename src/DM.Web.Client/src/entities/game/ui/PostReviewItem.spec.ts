/**
 * @vitest-environment jsdom
 */

// The raw textContent is asserted: a separator drawn by a margin renders but does not copy.
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { createMemoryHistory, createRouter } from "vue-router";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import PostReviewItem from "./PostReviewItem.vue";
import type { PostReview } from "../model/postTypes";

vi.mock("../api/postApi", () => ({
  default: {
    getPostReviewForEdit: vi.fn(),
    updatePostReview: vi.fn(),
    deletePostReview: vi.fn(),
  },
}));

const { default: postApi } = await import("../api/postApi");

/**
 * No trailing "Z": formatDateFull renders local time, and a UTC stamp would
 * make the expected line depend on the timezone the suite runs in.
 */
const review = {
  id: "r-1",
  postId: "p-1",
  text: "<p>Отличный пост</p>",
  sign: "Positive",
  createdUtc: "2026-07-26T16:00:00",
  author: { id: "u-author", username: "Тест Елки", role: "Player" },
} as unknown as PostReview;

/** The same review, published just now, so the edit window is open. */
const freshReview = {
  ...review,
  createdUtc: new Date().toISOString(),
} as unknown as PostReview;

/**
 * The room page with the post anchored and this review named, as GamePost
 * hands it down.
 */
const ROUTE = {
  name: "game-room",
  params: { id: "abcde", num: 2 },
  query: { review: "r-1" },
  hash: "#post-p-1",
};

const ROUTES = [
  { path: "/", name: "home", component: { template: "<div />" } },
  // The author of the review is a link to their profile.
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

type Viewer = {
  id: string;
  username: string;
  role?: string;
  isNewbie?: boolean;
};

/** Signs the viewer in: the auth store seeds itself from localStorage. */
function signIn(viewer: Viewer | null) {
  localStorage.clear();
  if (viewer) localStorage.setItem("user", JSON.stringify(viewer));
}

const mountItem = (
  props: Partial<{
    review: PostReview;
    editable: boolean;
    canWarn: boolean;
  }> = {},
) => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  return mount(PostReviewItem, {
    props: { review, number: 1, to: ROUTE, ...props },
    global: { plugins: [router, createPinia()] },
  });
};

/** A part of the delete question, which stands in <body> and not in the card. */
const inQuestion = (selector: string) => {
  const found = document.body.querySelector<HTMLElement>(
    `.dialog-container ${selector}`,
  );
  if (!found) throw new Error(`${selector} is not in the question`);
  return found;
};

/** The captions of the controls the card is currently offering. */
const actions = (wrapper: ReturnType<typeof mountItem>) =>
  wrapper.findAll(".review-meta .caption-action").map((b) => b.text());

/** The control of the caption line with this caption. */
const action = (wrapper: ReturnType<typeof mountItem>, caption: string) => {
  const found = wrapper
    .findAll(".review-meta .caption-action")
    .find((button) => button.text() === caption);
  if (!found) throw new Error(`no "${caption}" on the card`);
  return found;
};

/** The signs the edit form offers, as its segments read. */
const signs = (wrapper: ReturnType<typeof mountItem>) =>
  wrapper.findAll(".review-edit .segment").map((b) => b.text());

/** The segment of the sign toggle that reads `label`. */
const sign = (wrapper: ReturnType<typeof mountItem>, label: string) => {
  const found = wrapper
    .findAll(".review-edit .segment")
    .find((button) => button.text() === label);
  if (!found) throw new Error(`no "${label}" among the signs`);
  return found;
};

// A question left open by one test would answer the next one's clicks.
enableAutoUnmount(afterEach);

beforeEach(() => {
  signIn(null);
  vi.mocked(postApi.getPostReviewForEdit).mockReset();
  vi.mocked(postApi.updatePostReview).mockReset();
  vi.mocked(postApi.deletePostReview).mockReset();
});

describe("PostReviewItem", () => {
  it("copies as one human line, with the comma and the space before the number", () => {
    const row = mountItem().get(".review-meta").element.textContent;

    expect(row).toBe("+1 от Тест Елки, 26.07.2026 в\u00a016:00, #1");
  });

  it("keeps the number in the caption line, right after the date", () => {
    const wrapper = mountItem();

    expect(wrapper.find(".review-footer").exists()).toBe(false);
    const anchor = wrapper.get(".review-meta .review-anchor");
    expect(anchor.text()).toBe("#1");
  });

  it("describes the anchor with the project tooltip instead of a native title", () => {
    const wrapper = mountItem();
    const anchor = wrapper.get(".review-anchor");

    expect(anchor.attributes("title")).toBeUndefined();
    expect(anchor.attributes("aria-label")).toBe("Перейти к оценке поста");
    expect(wrapper.findComponent(Tooltip).props("text")).toBe(
      "Перейти к оценке поста",
    );
  });

  it("leads to the address it was handed instead of copying it", () => {
    const wrapper = mountItem();

    // A link, so the address is in the markup: the browser's own "copy link"
    // still reaches it, and a middle click opens it in a tab — neither of
    // which a button that wrote to the clipboard could do.
    expect(wrapper.find("button.review-anchor").exists()).toBe(false);
    expect(wrapper.get(".review-anchor").attributes("href")).toBe(
      "/game/abcde/rooms/2?review=r-1#post-p-1",
    );
  });
});

describe("PostReviewItem controls", () => {
  it("offers the author both controls while the window is open", () => {
    signIn({ id: "u-author", username: "Тест Елки", role: "RegularUser" });

    expect(actions(mountItem({ review: freshReview, editable: true }))).toEqual(
      ["Редактировать", "Удалить"],
    );
  });

  it("draws the controls as words of the signature, after the date", () => {
    signIn({ id: "u-author", username: "Тест Елки", role: "RegularUser" });

    const line = mountItem({ review: freshReview, editable: true })
      .get(".review-meta")
      .text()
      .replace(/\s+/g, " ");

    // The bars start after the number, which follows the date.
    expect(line).toMatch(
      /^\+1 от Тест Елки, \d\d\.\d\d\.\d{4} в \d\d:\d\d, #1 \| Редактировать \| Удалить$/,
    );
  });

  it("keeps the delete and drops the edit once the window has closed", () => {
    signIn({ id: "u-author", username: "Тест Елки", role: "RegularUser" });

    // The server refuses a late PATCH and takes a DELETE at any time, so the
    // author keeps the way out and loses only the correction.
    expect(actions(mountItem({ editable: true }))).toEqual(["Удалить"]);
  });

  it("offers nothing to somebody else's regular reader", () => {
    signIn({ id: "u-passerby", username: "Прохожий", role: "RegularUser" });

    expect(actions(mountItem({ review: freshReview, editable: true }))).toEqual(
      [],
    );
  });

  it("offers nothing to a moderator: the rank that may take a review down is above them", () => {
    signIn({ id: "u-moderator", username: "Модератор", role: "Moderator" });

    expect(actions(mountItem({ review: freshReview, editable: true }))).toEqual(
      [],
    );
  });

  it("offers a senior moderator the delete alone, never the edit", () => {
    signIn({ id: "u-senior", username: "Старший", role: "SeniorModerator" });

    // A review is signed: the way to deal with a bad one is to take it down,
    // not to put different words under somebody's name.
    expect(actions(mountItem({ review: freshReview, editable: true }))).toEqual(
      ["Удалить"],
    );
  });

  it("shows no controls at all on a read-only surface", () => {
    signIn({ id: "u-author", username: "Тест Елки", role: "RegularUser" });

    // The home page, the pulse and the rated lists draw the same card; the
    // game room is what opts the controls in.
    expect(actions(mountItem({ review: freshReview }))).toEqual([]);
  });

  it("shows a guest nothing", () => {
    expect(actions(mountItem({ review: freshReview, editable: true }))).toEqual(
      [],
    );
  });

  it("seeds the editor from the author's own text and saves sign and text together", async () => {
    signIn({ id: "u-author", username: "Тест Елки", role: "RegularUser" });
    vi.mocked(postApi.getPostReviewForEdit).mockResolvedValue({
      data: {
        resource: {
          ...freshReview,
          text: "[b]Отличный[/b] пост, Том &amp; Джерри",
        },
      },
      error: undefined,
    } as never);
    vi.mocked(postApi.updatePostReview).mockResolvedValue({
      data: { resource: { ...freshReview, sign: "Negative" } },
      error: undefined,
    } as never);
    const wrapper = mountItem({ review: freshReview, editable: true });

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(postApi.getPostReviewForEdit).toHaveBeenCalledWith("p-1", "r-1");
    const editor = wrapper.get("textarea.review-input");
    expect((editor.element as HTMLTextAreaElement).value).toBe(
      "[b]Отличный[/b] пост, Том &amp; Джерри",
    );

    // The sign the review had is the one pressed when the form opens.
    expect(signs(wrapper)).toEqual(["+", "=", "−"]);
    expect(sign(wrapper, "+").attributes("aria-pressed")).toBe("true");

    await sign(wrapper, "−").trigger("click");
    await wrapper.get(".submit-btn").trigger("click");
    await flushPromises();

    expect(postApi.updatePostReview).toHaveBeenCalledWith("p-1", "r-1", {
      sign: -1,
      text: "[b]Отличный[/b] пост, Том &amp; Джерри",
    });
    // The card shows what the server answered, without a refetch of the list.
    expect(wrapper.get(".review-sign").text()).toBe("-1");
    expect(wrapper.emitted("updated")).toEqual([[{ id: "r-1", sign: -1 }]]);
  });

  // The card's text is drawn: an editor opened on it would save the drawing.
  it("opens no editor when the text as typed does not come, and says so", async () => {
    signIn({ id: "u-author", username: "Тест Елки", role: "RegularUser" });
    vi.mocked(postApi.getPostReviewForEdit).mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    } as never);
    const wrapper = mountItem({ review: freshReview, editable: true });

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(wrapper.find("textarea.review-input").exists()).toBe(false);
    expect(wrapper.get(".review-refusal").text()).toBe(
      "Не удалось загрузить оценку",
    );
  });

  it("asks before it deletes, and reports the sign it took away", async () => {
    signIn({ id: "u-senior", username: "Старший", role: "SeniorModerator" });
    vi.mocked(postApi.deletePostReview).mockResolvedValue({
      data: undefined,
      error: undefined,
    } as never);
    const wrapper = mountItem({ review: freshReview, editable: true });

    await action(wrapper, "Удалить").trigger("click");

    // Nothing is gone yet: the dialog is the question, not the answer.
    expect(postApi.deletePostReview).not.toHaveBeenCalled();
    const dialog = wrapper.findComponent(ConfirmDialog);
    expect(dialog.props("show")).toBe(true);

    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(postApi.deletePostReview).toHaveBeenCalledWith("p-1", "r-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    // The sign travels with the event: the widget that owns the post subtracts
    // it from the rating instead of refetching the page to find out.
    expect(wrapper.emitted("deleted")).toEqual([[{ id: "r-1", sign: 1 }]]);
    expect(wrapper.text()).toContain("Оценка удалена");
    expect(dialog.props("show")).toBe(false);
  });

  /**
   * A refused delete stays in the question, over its buttons: the review is
   * still on the post, and the window that asked is where the reader looks.
   */
  it("says a refused delete in the question and keeps the review", async () => {
    signIn({ id: "u-senior", username: "Старший", role: "SeniorModerator" });
    vi.mocked(postApi.deletePostReview).mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    } as never);
    const wrapper = mountItem({ review: freshReview, editable: true });

    await action(wrapper, "Удалить").trigger("click");
    inQuestion(".dialog-btn-submit").click();
    await flushPromises();

    expect(inQuestion(".dialog-refusal").textContent?.trim()).toBe(
      "Не удалось удалить оценку",
    );
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(true);
    expect(wrapper.emitted("deleted")).toBeUndefined();
    expect(wrapper.text()).not.toContain("Оценка удалена");
  });

  it("hides the signed choices from a newbie, as the create form does", async () => {
    signIn({
      id: "u-author",
      username: "Тест Елки",
      role: "RegularUser",
      isNewbie: true,
    });
    vi.mocked(postApi.getPostReviewForEdit).mockResolvedValue({
      data: { resource: freshReview },
      error: undefined,
    } as never);
    const wrapper = mountItem({ review: freshReview, editable: true });

    await action(wrapper, "Редактировать").trigger("click");
    await flushPromises();

    expect(signs(wrapper)).toEqual(["="]);
  });
});

/**
 * The text of a review is a place a warning names. Whether the reader may warn its author is the list's
 * to say (the ranks), and the card offers the control by that word alone.
 */
describe("PostReviewItem warning", () => {
  it("offers Предупреждение where the list allows it, and hands the review up", async () => {
    const wrapper = mountItem({ canWarn: true });

    await action(wrapper, "Предупреждение").trigger("click");

    expect(wrapper.emitted("warn")).toEqual([[review]]);
  });

  it("offers no Предупреждение where the list does not allow it", () => {
    expect(actions(mountItem())).not.toContain("Предупреждение");
  });

  it("offers no Предупреждение for a review without a text", () => {
    expect(
      actions(mountItem({ canWarn: true, review: { ...review, text: "" } })),
    ).not.toContain("Предупреждение");
  });
});
