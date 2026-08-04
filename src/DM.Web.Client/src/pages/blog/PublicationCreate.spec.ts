/**
 * @vitest-environment jsdom
 */

/**
 * A draft of a text that has been published is not a draft.
 *
 * The editor keeps one for seven days and offers to restore it on the next
 * visit. Every other composer in the app clears it on success — the game room,
 * both comment lists, the topic, the support form, both chats. This page
 * navigated away instead, so the author came back a day later, was offered the
 * record they had already published, accepted, and posted it twice.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import PublicationCreate from "./PublicationCreate.vue";

const { clearDraft, push } = vi.hoisted(() => ({
  clearDraft: vi.fn(),
  push: vi.fn(),
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "the-blog" } }),
  useRouter: () => ({ push }),
}));

// The real form pulls TipTap; what matters here is that the page reaches for
// the draft the form owns.
vi.mock("@/features/publication", () => ({
  PublicationForm: defineComponent({
    emits: ["submit", "cancel"],
    setup(_props, { expose, emit }) {
      expose({ clearDraft });
      return () =>
        h("button", { class: "submit", onClick: () => emit("submit") });
    },
  }),
}));

function render() {
  localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  store.blog = {
    id: "the-blog",
    title: "Записки",
    author: { username: "SolohinLex" },
    assistants: [],
  } as never;
  vi.spyOn(store, "loadBlog").mockResolvedValue(undefined as never);

  return mount(PublicationCreate, { global: { plugins: [pinia] } });
}

describe("PublicationCreate", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it("clears the draft of a text it has just published", async () => {
    vi.spyOn(blogApi, "createPublication").mockResolvedValue({
      data: { resource: { id: "p-1", rubric: null } },
      error: null,
    } as never);
    const wrapper = render();

    await wrapper.find(".submit").trigger("click");
    await flushPromises();

    expect(clearDraft).toHaveBeenCalled();
    expect(push).toHaveBeenCalled();
  });

  it("keeps the draft when the publication was refused", async () => {
    vi.spyOn(blogApi, "createPublication").mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 500, traceId: "t" },
    } as never);
    const wrapper = render();

    await wrapper.find(".submit").trigger("click");
    await flushPromises();

    expect(clearDraft).not.toHaveBeenCalled();
    expect(push).not.toHaveBeenCalled();
  });
});
