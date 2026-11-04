/**
 * @vitest-environment jsdom
 */

/** The edit line of a post and the question before a delete. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { postApi } from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  REVIEWS,
  copied,
  lineOf,
  renderAs,
  stubResizeObserver,
  type PostWrapper,
} from "./GamePost.fixtures";

enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

/** The edit line under the text: "Отредактировано ... | Редактировать | Удалить". */
describe("GamePost edit line", () => {
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

  const EDITED = { edits: [{ id: "e-1", modifiedUtc: "2026-07-26T15:30:00" }] };

  it("says when the post was edited, then what the viewer may do", async () => {
    const wrapper = await renderAs("Author", EDITED);

    expect(copied(lineOf(wrapper).element)).toBe(
      "Отредактировано 26.07.2026 в 15:30 | Редактировать | Удалить",
    );
    wrapper.unmount();
  });

  it("stands under the text in the right column, and leaves the number alone in the footer", async () => {
    const wrapper = await renderAs("Author", EDITED);

    const body = wrapper.get(".post-body").element;
    expect(lineOf(wrapper).element.parentElement).toBe(body);
    expect(body.lastElementChild).toBe(lineOf(wrapper).element);
    expect(copied(wrapper.get(".post-footer").element)).toBe("7");
    expect(wrapper.find(".post-footer button").exists()).toBe(false);
    wrapper.unmount();
  });

  it("offers the actions alone on a post nobody edited", async () => {
    const wrapper = await renderAs("Author");

    expect(copied(lineOf(wrapper).element)).toBe("Редактировать | Удалить");
    wrapper.unmount();
  });

  it("says only that it was edited where the viewer may do nothing", async () => {
    const wrapper = await renderAs("Reader", EDITED, false);

    expect(copied(lineOf(wrapper).element)).toBe(
      "Отредактировано 26.07.2026 в 15:30",
    );
    wrapper.unmount();
  });

  it("draws no line when there is nothing to say", async () => {
    const wrapper = await renderAs("Reader", {}, false);

    expect(lineOf(wrapper).exists()).toBe(false);
    wrapper.unmount();
  });

  /** Every part at every width, the date of the edit with its time in one part. */
  it("keeps every part, the date of the edit with its time in one part", async () => {
    const wrapper = await renderAs("Author", EDITED);
    const line = lineOf(wrapper);

    const edited = line.findAll(".caption-line__part")[0];
    expect(copied(edited.element)).toBe("Отредактировано 26.07.2026 в 15:30");
    // One text, not the day and a span of its time apart.
    expect(edited.element.firstElementChild?.children.length).toBe(0);
    wrapper.unmount();
  });
});

/** The question before a delete waits for the answer and says a refusal in itself. */
describe("GamePost asks before it deletes", () => {
  const toast = useToast();

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
    document.body.innerHTML = "";
  });

  const question = () => document.body.querySelector(".dialog-container");

  async function confirmDelete(wrapper: PostWrapper) {
    await wrapper
      .findAll(".post-edit-line .caption-action")
      .find((action) => action.text() === "Удалить")!
      .trigger("click");
    question()?.querySelector<HTMLButtonElement>(".dialog-btn-submit")?.click();
    await flushPromises();
  }

  it("keeps the question open with the refusal in it, and the post in place", async () => {
    const remove = vi.spyOn(postApi, "deletePost").mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    } as never);
    const wrapper = await renderAs("Author");

    await confirmDelete(wrapper);

    expect(remove).toHaveBeenCalledWith("p-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(
      question()?.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Не удалось удалить пост");
    expect(toast.toasts.value).toEqual([]);
    expect(wrapper.find(".post-deleted").exists()).toBe(false);
    expect(wrapper.emitted("deleted")).toBeUndefined();
    wrapper.unmount();
  });

  it("takes the post off and closes once the server has", async () => {
    vi.spyOn(postApi, "deletePost").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = await renderAs("Author");

    await confirmDelete(wrapper);

    expect(wrapper.find(".post-deleted").exists()).toBe(true);
    expect(wrapper.emitted("deleted")).toEqual([["p-1"]]);
    expect(question()).toBeNull();
    wrapper.unmount();
  });
});
