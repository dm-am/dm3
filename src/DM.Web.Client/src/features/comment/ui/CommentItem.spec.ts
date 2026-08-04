/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import CommentItem from "./CommentItem.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import type { Comment } from "@/shared/api/models/common/comment";

vi.mock("vue-router", () => ({
  useRoute: () => ({
    query: {},
  }),
  RouterLink: { template: "<a><slot /></a>" },
}));

// The editor is a heavy tiptap component; the edit-seeding contract only
// needs its modelValue.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue"],
  },
}));

const comment = (over: Partial<Comment> = {}): Comment =>
  ({
    id: "c-1",
    // Server-rendered Display HTML — what the list shows, never edit source.
    text: "<strong>Правила</strong>",
    createdUtc: new Date().toISOString(),
    modifiedUtc: null,
    author: {
      username: "SolohinLex",
      role: "Player",
      lastActivityUtc: null,
    },
    likes: [],
    isRemoved: false,
    ...over,
  }) as unknown as Comment;

describe("CommentItem edit seeding", () => {
  let submitEdit: ReturnType<typeof vi.fn>;
  let submitDelete: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    localStorage.clear();
    // The signed-in author (auth store seeds itself from localStorage).
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    submitEdit = vi.fn().mockResolvedValue({ error: null });
    submitDelete = vi.fn().mockResolvedValue({ error: null });
    // Error toasts never auto-dismiss and deduplicate by text.
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  const mountComponent = (
    fetchEditSource: (id: string) => Promise<unknown>,
    over: Partial<Comment> = {},
  ) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: { template: "<a><slot /></a>" } },
      },
      props: {
        comment: comment(over),
        fetchEditSource: fetchEditSource as never,
        submitEdit: submitEdit as never,
        submitDelete: submitDelete as never,
      },
    });

  it("seeds the editor from the fetched raw BBCode source, not the displayed HTML", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await wrapper.find(".action-btn").trigger("click");
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith("c-1");
    const editor = wrapper.find(".editor-stub");
    expect(editor.exists()).toBe(true);
    expect((editor.element as HTMLTextAreaElement).value).toBe(
      "[b]Правила[/b]",
    );
  });

  it("does not open the editor when the source fetch fails", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: null,
      error: { message: "network" },
    });
    const wrapper = mountComponent(fetch);

    await wrapper.find(".action-btn").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  it("hands the edited raw BBCode to the save function", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await wrapper.find(".action-btn").trigger("click");
    await flushPromises();
    await wrapper.find(".save-btn").trigger("click");
    await flushPromises();

    expect(submitEdit).toHaveBeenCalledWith("c-1", "[b]Правила[/b]");
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  /**
   * The editor used to close on the emit, whatever the server answered: the
   * rejected text was gone, the old text was back, and nothing was said.
   */
  it("keeps the editor open and names the refusal when the save fails", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    submitEdit.mockResolvedValue({
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = mountComponent(fetch);

    await wrapper.find(".action-btn").trigger("click");
    await flushPromises();
    await wrapper.find(".save-btn").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").exists()).toBe(true);
    expect(useToast().toasts.value.map((t) => t.message)).toEqual([
      "Не удалось сохранить комментарий",
    ]);
  });

  it("names the refusal when the delete fails", async () => {
    submitDelete.mockResolvedValue({
      error: { type: "", title: "", status: 404, traceId: "t" },
    });
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(".delete-btn").trigger("click");
    wrapper.findComponent(ConfirmDialog).vm.$emit("confirm");
    await flushPromises();

    expect(submitDelete).toHaveBeenCalledWith("c-1");
    expect(useToast().toasts.value.map((t) => t.message)).toEqual([
      "Не удалось удалить комментарий",
    ]);
  });

  /**
   * The footer packs "Редактировать", "Удалить" and "Предупреждение" at a $small
   * step, and the middle one deleted on the first click with nothing to undo
   * it. The game post and the forum topic both ask first, through this dialog.
   */
  it("asks before deleting", async () => {
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(".delete-btn").trigger("click");

    expect(submitDelete).not.toHaveBeenCalled();
    expect(wrapper.findComponent(ConfirmDialog).props("show")).toBe(true);
  });

  it("deletes nothing when the question is answered no", async () => {
    const wrapper = mountComponent(vi.fn());

    await wrapper.find(".delete-btn").trigger("click");
    wrapper.findComponent(ConfirmDialog).vm.$emit("cancel");
    await flushPromises();

    expect(submitDelete).not.toHaveBeenCalled();
  });
});
