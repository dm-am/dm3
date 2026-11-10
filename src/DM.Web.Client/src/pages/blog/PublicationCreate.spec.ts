/**
 * @vitest-environment jsdom
 */

// A published text clears its draft, as every composer does, or the next visit
// offers it again and posts it twice. Anybody but the owner and the assistants
// gets "not found" once the session is settled.
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { Router } from "vue-router";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { UserRole, type Username } from "@/shared/api/models/common";
import { useAuthStore, type User } from "@/entities/user";
import PublicationCreate from "./PublicationCreate.vue";

const { clearDraft, session } = vi.hoisted(() => ({
  clearDraft: vi.fn(),
  session: { settled: Promise.resolve() as Promise<void> },
}));

// The session answer, settled at once unless a test keeps it out.
vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

const CREATE = "/blogs/the-blog/feed/create";

let router: Router;
/** Where the page stands now: the route name and the address. */
const standing = () => ({
  name: router.currentRoute.value.name,
  fullPath: router.currentRoute.value.fullPath,
});
// The site's not found keeps the address it was opened at.
const NOT_FOUND = { name: "not-found", fullPath: CREATE };
const STAYED = { name: "blog-publication-create", fullPath: CREATE };

// The real form pulls TipTap; the page reaches for the draft the form owns.
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

async function render(viewer: string | null = "SolohinLex") {
  if (viewer)
    localStorage.setItem("user", JSON.stringify({ username: viewer }));
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  const blog = createMockBlog("the-blog", "Записки");
  store.blog = {
    ...blog,
    author: {
      id: "u-owner",
      username: "SolohinLex" as Username,
      lastActivityUtc: null,
      role: UserRole.RegularUser,
      isNewbie: false,
    },
    assistants: [],
  };
  vi.spyOn(store, "loadBlog").mockResolvedValue(undefined as never);

  const mounted = await mountPage(PublicationCreate, CREATE, {
    routes: [
      ["blog-publication-create", "/blogs/:id/feed/create"],
      ["blog-feed", "/blogs/:id/feed"],
      ["not-found", "/:pathMatch(.*)*"],
    ],
    pinia,
  });
  router = mounted.router;
  return mounted.wrapper;
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
    const wrapper = await render();

    await wrapper.find(".submit").trigger("click");
    await flushPromises();

    expect(clearDraft).toHaveBeenCalled();
    expect(standing()).toEqual({
      name: "blog-feed",
      fullPath: "/blogs/the-blog/feed",
    });
  });

  it("keeps the draft when the publication was refused", async () => {
    vi.spyOn(blogApi, "createPublication").mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 500, traceId: "t" },
    } as never);
    const wrapper = await render();

    await wrapper.find(".submit").trigger("click");
    await flushPromises();

    expect(clearDraft).not.toHaveBeenCalled();
    expect(standing()).toEqual(STAYED);
  });

  it("opens the site's not found for a reader of the blog, with no refusal drawn", async () => {
    const wrapper = await render("a-reader");
    await flushPromises();

    expect(standing()).toEqual(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const wrapper = await render(null);
    await flushPromises();

    expect(standing()).toEqual(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("keeps the owner of the blog on the page", async () => {
    await render();
    await flushPromises();

    expect(standing()).toEqual(STAYED);
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    // A session the browser kept no copy of: the owner stays once the server names him.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const wrapper = await render(null);
    await flushPromises();

    expect(standing()).toEqual(STAYED);

    useAuthStore().user = { username: "SolohinLex" } as User;
    answer();
    await flushPromises();
    expect(standing()).toEqual(STAYED);
    expect(wrapper.find(".submit").exists()).toBe(true);
  });
});
