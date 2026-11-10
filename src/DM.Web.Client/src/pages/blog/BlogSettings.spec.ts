/**
 * @vitest-environment jsdom
 */

// Each section pinned to the intention that enforces it: EditSettings (owner,
// assistants, senior moderation; also admission), Edit (owner and admins, no
// senior-moderator clause, unlike the game), ManageRubrics, InviteAssistant and
// CancelInvitation (owner), InviteReader (owner and assistants). A viewer
// admitted to nothing gets "not found" once the session is settled.
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { Router } from "vue-router";
import { useBlogDetailsStore } from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError, Username } from "@/shared/api/models/common";
import BlogSettings from "./BlogSettings.vue";

const { session } = vi.hoisted(() => ({
  session: { settled: Promise.resolve() as Promise<void> },
}));

// The session answer, settled at once unless a test keeps it out.
vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

const SETTINGS = "/blogs/the-blog/settings";

let router: Router;
/** Where the page stands now: the route name and the address. */
const standing = () => ({
  name: router.currentRoute.value.name,
  fullPath: router.currentRoute.value.fullPath,
});
// The site's not found keeps the address it was opened at.
const NOT_FOUND = { name: "not-found", fullPath: SETTINGS };
const STAYED = { name: "blog-settings", fullPath: SETTINGS };

/** A section that is either drawn or not; nothing else is asked of it. */
const marker = (className: string) => ({
  setup: () => () => h("div", { class: className }),
});

// The DOM lowercases attribute names, so the flags go out in kebab case.
const kebab = (name: string) =>
  name.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`);

/** A section drawn for a wider audience than it accepts writes from: the stub records both flags. */
const gatedMarker = (className: string, first: string, second: string) =>
  defineComponent({
    props: {
      [first]: { type: Boolean, default: false },
      [second]: { type: Boolean, default: false },
    },
    setup: (props) => () =>
      h("div", {
        class: className,
        [`data-${kebab(first)}`]: String(props[first]),
        [`data-${kebab(second)}`]: String(props[second]),
      }),
  });

const stubs = {
  BlogInfoSection: marker("info-section-stub"),
  RubricsSection: marker("rubrics-section-stub"),
  RolesSection: gatedMarker("roles-section-stub", "canInvite", "canRemove"),
  BlacklistSection: marker("blacklist-section-stub"),
  InvitationsSection: gatedMarker(
    "invitations-section-stub",
    "canInvite",
    "canCancel",
  ),
  // The confirmation is the real window, drawn in place.
  Teleport: true,
};

const THE_VIEWER = "the-viewer";

/** How the viewer takes part in the blog the page is drawn for. */
type Standing = "owner" | "assistant" | "stranger";

async function render(standing: Standing, viewer?: UserRole) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = viewer
    ? ({ username: THE_VIEWER, role: viewer } as User)
    : null;
  const store = useBlogDetailsStore();
  // The store derives the roles from the blog's author and assistants.
  const blog = createMockBlog("the-blog", "Дневник");
  const viewerRef = {
    id: `u-${THE_VIEWER}`,
    username: THE_VIEWER as Username,
    lastActivityUtc: null,
    role: UserRole.RegularUser,
    isNewbie: false,
  };
  store.blog = {
    ...blog,
    author: standing === "owner" ? viewerRef : blog.author,
    assistants: standing === "assistant" ? [viewerRef] : [],
  };

  const mounted = await mountPage(BlogSettings, SETTINGS, {
    routes: [
      ["blog-settings", "/blogs/:id/settings"],
      ["profile", "/users/:username"],
      ["not-found", "/:pathMatch(.*)*"],
    ],
    pinia,
    stubs,
  });
  router = mounted.router;
  return mounted.wrapper;
}

const flag = (wrapper: VueWrapper, selector: string, name: string) => {
  const el = wrapper.find(selector);
  return el.exists() && el.attributes(`data-${kebab(name)}`) === "true";
};

const sectionsOf = (wrapper: VueWrapper) => ({
  info: wrapper.find(".info-section-stub").exists(),
  rubrics: wrapper.find(".rubrics-section-stub").exists(),
  roles: wrapper.find(".roles-section-stub").exists(),
  inviteAssistant: flag(wrapper, ".roles-section-stub", "canInvite"),
  removeAssistant: flag(wrapper, ".roles-section-stub", "canRemove"),
  blacklist: wrapper.find(".blacklist-section-stub").exists(),
  invitations: wrapper.find(".invitations-section-stub").exists(),
  inviteReader: flag(wrapper, ".invitations-section-stub", "canInvite"),
  cancelInvitation: flag(wrapper, ".invitations-section-stub", "canCancel"),
  danger: wrapper.find(".danger-zone").exists(),
});

const seenBy = async (
  standing: Standing,
  viewer: UserRole = UserRole.RegularUser,
) => sectionsOf(await render(standing, viewer));

describe("BlogSettings admission", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    session.settled = Promise.resolve();
  });

  it("keeps the page away from a stranger", async () => {
    expect((await seenBy("stranger")).info).toBe(false);
  });

  it("keeps it away from a junior moderator, whom EditSettings does not admit", async () => {
    expect((await seenBy("stranger", UserRole.Moderator)).info).toBe(false);
  });

  it("opens the site's not found for a viewer it refuses, with no refusal drawn", async () => {
    const wrapper = await render("stranger", UserRole.RegularUser);
    await flushPromises();

    expect(standing()).toEqual(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const wrapper = await render("stranger");
    await flushPromises();

    expect(standing()).toEqual(NOT_FOUND);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
  });

  it("keeps the assistant on his page", async () => {
    await render("assistant", UserRole.RegularUser);
    await flushPromises();

    expect(standing()).toEqual(STAYED);
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    // A session the browser kept no copy of: the owner stays once the server names him.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    await render("owner");
    await flushPromises();

    expect(standing()).toEqual(STAYED);

    useAuthStore().user = { username: THE_VIEWER } as User;
    answer();
    await flushPromises();
    expect(standing()).toEqual(STAYED);
  });
});

describe("BlogSettings sections", () => {
  it("gives the owner every section", async () => {
    expect(await seenBy("owner")).toEqual({
      info: true,
      rubrics: true,
      roles: true,
      inviteAssistant: true,
      removeAssistant: true,
      blacklist: true,
      invitations: true,
      inviteReader: true,
      cancelInvitation: true,
      danger: true,
    });
  });

  it("gives the assistant the settings and the reader invites, nothing else", async () => {
    // Rubrics, the blacklist, assistants and cancelling are the owner's.
    expect(await seenBy("assistant")).toEqual({
      info: true,
      rubrics: false,
      roles: true,
      inviteAssistant: false,
      removeAssistant: false,
      blacklist: false,
      invitations: true,
      inviteReader: true,
      cancelInvitation: false,
      danger: false,
    });
  });

  it("gives a senior moderator the settings without the blacklist", async () => {
    // BlogIntention.Edit stops at the administration here.
    expect(await seenBy("stranger", UserRole.SeniorModerator)).toEqual({
      info: true,
      rubrics: false,
      roles: true,
      inviteAssistant: false,
      removeAssistant: false,
      blacklist: false,
      invitations: true,
      inviteReader: false,
      cancelInvitation: false,
      danger: false,
    });
  });

  it("adds the blacklist and assistant removal for an administrator", async () => {
    expect(await seenBy("stranger", UserRole.Admin)).toEqual({
      info: true,
      rubrics: false,
      roles: true,
      inviteAssistant: false,
      removeAssistant: true,
      blacklist: true,
      invitations: true,
      inviteReader: false,
      cancelInvitation: false,
      danger: false,
    });
  });
});

// The author lands on his own list of blogs: the missing row there is the news.
describe("BlogSettings — after the blog is deleted", () => {
  const toast = useToast();
  const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

  beforeEach(() => {
    vi.clearAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  /** Runs the whole gesture: the danger button, then the confirmation. */
  async function deleteBlog(answer: GeneralError | null) {
    const wrapper = await render("owner", UserRole.RegularUser);
    const remove = vi
      .spyOn(useBlogDetailsStore(), "deleteBlog")
      .mockResolvedValue(answer);

    await wrapper.find(".delete-blog").trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, remove };
  }

  it("leaves for the author's own list of blogs", async () => {
    const { wrapper, remove } = await deleteBlog(null);

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(remove).toHaveBeenCalledWith({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
    expect(standing()).toEqual({
      name: "profile",
      fullPath: `/users/${THE_VIEWER}?tab=blogs`,
    });
  });

  it("says nothing on top of it: the missing blog is the answer", async () => {
    await deleteBlog(null);

    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps the author on the settings and names the refusal in the window", async () => {
    const { wrapper } = await deleteBlog({
      status: 400,
      title: "",
    } as GeneralError);

    expect(standing()).toEqual(STAYED);
    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить блог",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
    expect(wrapper.find(".delete-blog").exists()).toBe(true);
  });
});
