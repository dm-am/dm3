/**
 * @vitest-environment jsdom
 */

/** The order of the left column, every block stood in for by an item that names it. */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { defineComponent } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import LeftSidebar from "./LeftSidebar.vue";

const { block } = vi.hoisted(() => ({
  /** A block of the column as a list item that says which one it is. */
  block: async (name: string) => {
    const { defineComponent, h } = await import("vue");
    return {
      // Read as a module by the lazy loaders, which unwrap its default.
      __esModule: true,
      default: defineComponent({
        name,
        props: { context: Boolean },
        setup: (props) => () =>
          h("li", {
            "data-block": props.context ? `${name}:context` : name,
          }),
      }),
    };
  },
}));

vi.mock("./GamePanel.vue", () => block("GamePanel"));
vi.mock("./BlogPanel.vue", () => block("BlogPanel"));
vi.mock("./ModerationPanel.vue", () => block("ModerationPanel"));
vi.mock("./OwnedGames.vue", () => block("OwnedGames"));
vi.mock("./OwnedBlogs.vue", () => block("OwnedBlogs"));
vi.mock("./RecruitingGames.vue", () => block("RecruitingGames"));
vi.mock("./ActiveGames.vue", () => block("ActiveGames"));
vi.mock("./FinishedGames.vue", () => block("FinishedGames"));
vi.mock("./ActiveBlogs.vue", () => block("ActiveBlogs"));
vi.mock("./ForumBoards.vue", () => block("ForumBoards"));

const Empty = defineComponent({ render: () => null });

const ROUTES = [
  { path: "/", name: "home", component: Empty },
  {
    path: "/forum",
    component: Empty,
    children: [
      { path: "", name: "forum-index", component: Empty },
      { path: ":alias", name: "forum", component: Empty },
      { path: ":alias/:num", name: "topic", component: Empty },
    ],
  },
  {
    path: "/moderation/bans",
    name: "moderation-bans",
    meta: { moderationZone: true },
    component: Empty,
  },
];

/** The blocks of the column, top to bottom. */
async function columnAt(location: string, role?: UserRole) {
  useAuthStore().user = role ? ({ username: "Читатель", role } as User) : null;
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();
  const wrapper = mount(LeftSidebar, { global: { plugins: [router] } });
  await flushPromises();
  return wrapper
    .findAll("[data-block]")
    .map((el) => el.attributes("data-block") ?? "");
}

describe("the order of the left column", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it.each([
    ["/forum", "a member", UserRole.RegularUser],
    ["/forum/general", "a member", UserRole.RegularUser],
    ["/forum/general/5", "a member", UserRole.RegularUser],
    ["/forum/general", "a guest", undefined],
    ["/", "a member", UserRole.RegularUser],
  ])(
    "closes the column with the one forum block on %s for %s",
    async (location, _who, role) => {
      const blocks = await columnAt(location, role);

      expect(blocks.at(-1)).toBe("ForumBoards");
      expect(blocks.filter((b) => b.startsWith("ForumBoards"))).toEqual([
        "ForumBoards",
      ]);
    },
  );

  it("leads with the moderation menu in moderation, the forum at the bottom", async () => {
    const blocks = await columnAt("/moderation/bans", UserRole.Moderator);

    expect(blocks[0]).toBe("ModerationPanel");
    expect(blocks.at(-1)).toBe("ForumBoards");
  });

  // The mentor has no block of his own: the games and blogs he curates are in
  // "Мои игры" and "Мои блоги".
  it("leads a mentor's column with Мои игры", async () => {
    const blocks = await columnAt("/", UserRole.Mentor);

    expect(blocks[0]).toBe("OwnedGames");
  });

  // The ban list is open to all, the rest of the zone is not: no menu of
  // sections that would answer 403.
  it.each([
    ["a guest", undefined],
    ["a member", UserRole.RegularUser],
  ])(
    "offers %s no moderation menu on the open ban list",
    async (_who, role) => {
      const blocks = await columnAt("/moderation/bans", role);

      expect(blocks).not.toContain("ModerationPanel");
    },
  );
});
