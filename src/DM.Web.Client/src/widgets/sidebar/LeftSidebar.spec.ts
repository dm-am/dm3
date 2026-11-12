/**
 * @vitest-environment jsdom
 */

/**
 * The left column, every block stood in for by an item that names it: the
 * order of the blocks, and "Меню игры" and "Меню блога" heading the column
 * while their chunk still loads.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { defineComponent, h } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import { useGameDetailsStore } from "@/entities/game";
import { useBlogDetailsStore } from "@/entities/blog";
import LeftSidebar from "./LeftSidebar.vue";
import ContextPanelSkeleton from "./ContextPanelSkeleton.vue";

const { chunks, block } = vi.hoisted(() => ({
  /** The context panels' chunks, each let in when the test wants. */
  chunks: Object.fromEntries(
    ["GamePanel", "BlogPanel"].map((name) => {
      let release = () => {};
      const arrived = new Promise<void>((resolve) => (release = resolve));
      return [name, { arrived, release: () => release() }];
    }),
  ) as Record<
    "GamePanel" | "BlogPanel",
    { arrived: Promise<void>; release: () => void }
  >,
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

vi.mock("./GamePanel.vue", async () => {
  await chunks.GamePanel.arrived;
  return block("GamePanel");
});
vi.mock("./BlogPanel.vue", async () => {
  await chunks.BlogPanel.arrived;
  return block("BlogPanel");
});
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
  {
    path: "/game/:id",
    name: "game",
    meta: { gameZone: true },
    component: Empty,
  },
  {
    path: "/blogs/:id",
    name: "blog",
    meta: { blogZone: true },
    component: Empty,
  },
];

/** The column on a page, for a viewer of that role or a guest. */
async function mountColumnAt(location: string, role?: UserRole) {
  useAuthStore().user = role ? ({ username: "Читатель", role } as User) : null;
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();
  const wrapper = mount(LeftSidebar, { global: { plugins: [router] } });
  await flushPromises();
  return wrapper;
}

/** The blocks of the column, top to bottom. */
const columnAt = async (location: string, role?: UserRole) =>
  (await mountColumnAt(location, role))
    .findAll("[data-block]")
    .map((el) => el.attributes("data-block") ?? "");

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

/** The first block of the column: its title, and its skeleton rows if any. */
function head(wrapper: Awaited<ReturnType<typeof mountColumnAt>>) {
  const first = wrapper.get("ul.blocks > li");
  return {
    title: first.find(".sidebar-title").exists()
      ? first.get(".sidebar-title").text()
      : null,
    skeletonRows: first.findAll(".sidebar-skeleton .skeleton-row").length,
    block: first.attributes("data-block") ?? null,
  };
}

describe("the context menu of the left column while its chunk loads", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it.each([
    ["/game/aaabp", "GamePanel", "Меню игры"],
    ["/blogs/aaaab", "BlogPanel", "Меню блога"],
  ] as const)(
    "stands at the top of the column on %s from the first frame",
    async (location, panel, title) => {
      const wrapper = await mountColumnAt(location);

      expect(head(wrapper)).toEqual({
        title,
        skeletonRows: 8,
        block: null,
      });

      chunks[panel].release();
      await flushPromises();
      await vi.dynamicImportSettled();
      await flushPromises();

      expect(head(wrapper).block).toBe(panel);
      expect(wrapper.findAll(".sidebar-skeleton")).toHaveLength(0);
    },
  );
});

/**
 * The skeleton is as tall as the one the panel draws while its game or blog
 * loads, so nothing moves when the chunk swaps in.
 */
describe("the context menu skeleton", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const skeletonRows = (component: unknown, props: Record<string, unknown>) =>
    mount(
      defineComponent({
        setup: () => () => h("ul", [h(component as never, props)]),
      }),
      { global: { stubs: { RouterLink: true, ConfirmDialog: true } } },
    ).findAll(".sidebar-skeleton .skeleton-row").length;

  it("has the rows of the loading game panel", async () => {
    const { default: GamePanel } = await vi.importActual<{
      default: unknown;
    }>("./GamePanel.vue");
    const store = useGameDetailsStore();
    store.game = null;
    store.gameLoading = true;

    expect(
      skeletonRows(ContextPanelSkeleton, {
        token: "GamePanel",
        title: "Меню игры",
      }),
    ).toBe(skeletonRows(GamePanel, { gameId: "g-1" }));
  });

  it("has the rows of the loading blog panel", async () => {
    const { default: BlogPanel } = await vi.importActual<{
      default: unknown;
    }>("./BlogPanel.vue");
    const store = useBlogDetailsStore();
    store.blog = null;
    store.blogLoading = true;

    expect(
      skeletonRows(ContextPanelSkeleton, {
        token: "BlogPanel",
        title: "Меню блога",
      }),
    ).toBe(skeletonRows(BlogPanel, { blogId: "b-1" }));
  });
});
