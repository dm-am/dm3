/**
 * @vitest-environment jsdom
 */

/**
 * The blog settings page is the twin of the game one, and it has the same
 * problem: the sections behind it do not share an audience, so a single page
 * flag would either lock people out of what they may do or offer them saves
 * the server answers with 403. This spec pins each section to the intention
 * that enforces it:
 *  - BlogIntention.EditSettings (the information form, the invitation list,
 *    and admission to the page): owner, assistants, senior moderation;
 *  - BlogIntention.Edit (the blacklist including its read, and assistant
 *    removal): the owner and administration — no senior-moderator clause on
 *    this one, unlike the game side;
 *  - CreateRubric / InviteAssistant / CancelInvitation: the owner alone;
 *  - InviteReader: the owner and the assistants.
 *
 * The roles are read from useBlogDetailsStore (derived from the blog's author
 * and assistants) plus the viewer's site role — never a check invented here.
 */
import { describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import BlogSettings from "./BlogSettings.vue";

// Only the navigation the delete action holds; the rest of the router stays
// real, as in the game settings spec.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-blog" }, query: {} }),
  useRouter: () => ({ push: vi.fn() }),
}));

/** A section that is either drawn or not; nothing else is asked of it. */
const marker = (className: string) => ({
  setup: () => () => h("div", { class: className }),
});

// The DOM lowercases attribute names, so the recorded flags go out in kebab
// case or `attributes()` would never find them again.
const kebab = (name: string) =>
  name.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`);

/**
 * A section drawn for a wider audience than it accepts writes from: the stub
 * records the two flags the page hands it.
 */
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
  ConfirmDialog: true,
  RouterLink: { template: "<a><slot /></a>" },
};

const THE_VIEWER = "the-viewer";

/** How the viewer takes part in the blog the page is drawn for. */
type Standing = "owner" | "assistant" | "stranger";

function render(standing: Standing, viewer?: UserRole) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = viewer
    ? ({ username: THE_VIEWER, role: viewer } as User)
    : null;
  const store = useBlogDetailsStore();
  // The blog roles are not served as flags: the store derives them by matching
  // the signed-in username against the blog's author and assistants.
  store.blog = {
    id: "the-blog",
    title: "Дневник",
    author: {
      username: standing === "owner" ? THE_VIEWER : "the-blog-owner",
    },
    assistants: standing === "assistant" ? [{ username: THE_VIEWER }] : [],
  } as unknown as Blog;

  return mount(BlogSettings, { global: { plugins: [pinia], stubs } });
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

const seenBy = (standing: Standing, viewer: UserRole = UserRole.RegularUser) =>
  sectionsOf(render(standing, viewer));

describe("BlogSettings admission", () => {
  it("keeps the page away from a stranger", () => {
    expect(seenBy("stranger").info).toBe(false);
  });

  it("keeps it away from a junior moderator, whom EditSettings does not admit", () => {
    expect(seenBy("stranger", UserRole.Moderator).info).toBe(false);
  });

  it("invites a guest to sign in instead of refusing an identity it never saw", () => {
    const wrapper = render("stranger");

    expect(wrapper.find(".login-prompt").exists()).toBe(true);
    expect(sectionsOf(wrapper).info).toBe(false);
  });

  it("names the blog roles when it refuses", () => {
    expect(render("stranger", UserRole.RegularUser).text()).toBe(
      "Настройки блога доступны мастеру блога и ассистентам.",
    );
  });
});

describe("BlogSettings sections", () => {
  it("gives the owner every section", () => {
    expect(seenBy("owner")).toEqual({
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

  it("gives the assistant the settings and the reader invites, nothing else", () => {
    // Rubrics, the blacklist, assistant management and cancelling an
    // invitation are all the owner's; the assistant would collect a 403 on
    // every one of them, the blacklist READ included.
    expect(seenBy("assistant")).toEqual({
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

  it("gives a senior moderator the settings without the blacklist", () => {
    // BlogIntention.Edit stops at the administration here, so the section the
    // game side opens for senior moderation stays shut on the blog.
    expect(seenBy("stranger", UserRole.SeniorModerator)).toEqual({
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

  it("adds the blacklist and assistant removal for an administrator", () => {
    expect(seenBy("stranger", UserRole.Admin)).toEqual({
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
