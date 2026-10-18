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
 *
 * Nobody but the owner and the assistants has this page: anybody else, a guest
 * included, gets the site's own "not found" at its address, with no refusal
 * drawn, once the session is settled.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { useAuthStore, type User } from "@/entities/user";
import PublicationCreate from "./PublicationCreate.vue";

const { clearDraft, push, replace, session } = vi.hoisted(() => ({
  clearDraft: vi.fn(),
  push: vi.fn(),
  replace: vi.fn(),
  session: { settled: Promise.resolve() as Promise<void> },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({
    params: { id: "the-blog" },
    path: "/blogs/the-blog/feed/create",
    query: {},
    hash: "",
  }),
  useRouter: () => ({ push, replace }),
}));

// The answer about the session the start of the page asks for, held by the
// test: settled at once unless a test keeps it out.
vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

const NOT_FOUND = {
  name: "not-found",
  params: { pathMatch: ["blogs", "the-blog", "feed", "create"] },
  query: {},
  hash: "",
};

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

function render(viewer: string | null = "SolohinLex") {
  if (viewer)
    localStorage.setItem("user", JSON.stringify({ username: viewer }));
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
    session.settled = Promise.resolve();
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

  it("opens the site's not found for a reader of the blog, with no refusal drawn", async () => {
    const wrapper = render("a-reader");
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const wrapper = render(null);
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("keeps the owner of the blog on the page", async () => {
    render();
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    // A session the browser kept no copy of: the store holds nobody until the
    // server names the viewer, and the owner then stays and gets the form.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const wrapper = render(null);
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();

    useAuthStore().user = { username: "SolohinLex" } as User;
    answer();
    await flushPromises();
    expect(replace).not.toHaveBeenCalled();
    expect(wrapper.find(".submit").exists()).toBe(true);
  });
});
