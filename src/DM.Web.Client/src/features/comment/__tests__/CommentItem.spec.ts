/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import CommentItem from "../ui/CommentItem.vue";
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
  beforeEach(() => {
    localStorage.clear();
    // The signed-in author (auth store seeds itself from localStorage).
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
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

  it("emits the edited raw BBCode on save", async () => {
    const fetch = vi.fn().mockResolvedValue({
      data: { resource: comment({ text: "[b]Правила[/b]" }) },
      error: undefined,
    });
    const wrapper = mountComponent(fetch);

    await wrapper.find(".action-btn").trigger("click");
    await flushPromises();
    await wrapper.find(".save-btn").trigger("click");

    expect(wrapper.emitted("edit")).toEqual([["c-1", "[b]Правила[/b]"]]);
  });
});
