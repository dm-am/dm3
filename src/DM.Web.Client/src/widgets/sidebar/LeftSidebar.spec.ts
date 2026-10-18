/**
 * @vitest-environment jsdom
 */

/**
 * The rule is written in LeftSidebar itself: "Conditional context panels:
 * lazy-loaded so their entity stores/features stay out of the main bundle
 * (LeftSidebar itself is statically imported by the router)". Three of the four
 * panels followed it and the largest one, GamePanel, did not - 484 lines,
 * shown only on a game route, pulling the game details store and the
 * game-actions feature into the chunk every guest downloads on the home page.
 *
 * Read from the source rather than from a mount: what is asserted is how the
 * component is imported, and a mounted component has already resolved that.
 *
 * The order of the column is asserted on a mount, with every block stood in
 * for by a list item that names it. The menu of a zone leads the column, as
 * the game menu does on a game's pages. The forum has no menu in the column:
 * its boards are the strip under the heading of every forum page
 * (pages/forum/BoardNavigation.vue), and the forum block closes the column on
 * every page, the forum's included, as it did before 5 October 2026 (the
 * owner turned the forum menu of the column down on 12 October).
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
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
vi.mock("./MentorPanel.vue", () => block("MentorPanel"));
vi.mock("./OwnedGames.vue", () => block("OwnedGames"));
vi.mock("./OwnedBlogs.vue", () => block("OwnedBlogs"));
vi.mock("./RecruitingGames.vue", () => block("RecruitingGames"));
vi.mock("./ActiveGames.vue", () => block("ActiveGames"));
vi.mock("./FinishedGames.vue", () => block("FinishedGames"));
vi.mock("./ActiveBlogs.vue", () => block("ActiveBlogs"));
vi.mock("./ForumBoards.vue", () => block("ForumBoards"));

const HERE = dirname(fileURLToPath(import.meta.url));
const SOURCE = readFileSync(join(HERE, "LeftSidebar.vue"), "utf8");

/** The panels that mount only on their own zone or for their own role. */
const CONTEXT_PANELS = [
  "GamePanel",
  "BlogPanel",
  "ModerationPanel",
  "MentorPanel",
];

describe("LeftSidebar", () => {
  for (const panel of CONTEXT_PANELS) {
    it(`loads ${panel} lazily`, () => {
      expect(SOURCE).toContain(
        `const ${panel} = defineAsyncComponent(() => import("./${panel}.vue"));`,
      );
      expect(SOURCE).not.toContain(`import ${panel} from "./${panel}.vue";`);
    });
  }

  it("renders every panel it lazy-loads", () => {
    // A panel dropped from the template but left in the imports would keep
    // passing the check above while carrying no weight at all.
    for (const panel of CONTEXT_PANELS) {
      expect(SOURCE).toContain(`<${panel}`);
    }
  });
});

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

  for (const location of ["/forum", "/forum/general", "/forum/general/5"]) {
    it(`closes the column with the one forum block on ${location}`, async () => {
      const blocks = await columnAt(location, UserRole.RegularUser);

      expect(blocks.at(-1)).toBe("ForumBoards");
      expect(blocks.filter((b) => b.startsWith("ForumBoards"))).toEqual([
        "ForumBoards",
      ]);
    });
  }

  it("closes the column with the forum block for a guest on the forum", async () => {
    const blocks = await columnAt("/forum/general");

    expect(blocks.at(-1)).toBe("ForumBoards");
    expect(blocks.filter((b) => b.startsWith("ForumBoards"))).toEqual([
      "ForumBoards",
    ]);
  });

  it("draws the forum block once, on every page, the forum's included", () => {
    // Read from the source: on a mount a block kept off the forum alone looks
    // the same as one kept everywhere, until some route says it is the
    // forum's.
    expect(SOURCE.match(/<ForumBoards\b[^>]*>/g)).toEqual(["<ForumBoards />"]);
    expect(SOURCE).toMatch(/<ForumBoards \/>\s*<\/ul>/);
  });

  it("closes the column with the forum block off the forum", async () => {
    const blocks = await columnAt("/", UserRole.RegularUser);

    expect(blocks.at(-1)).toBe("ForumBoards");
    expect(blocks.filter((b) => b.startsWith("ForumBoards"))).toHaveLength(1);
  });

  it("leads with the moderation menu in moderation, the forum at the bottom", async () => {
    const blocks = await columnAt("/moderation/bans", UserRole.Moderator);

    expect(blocks[0]).toBe("ModerationPanel");
    expect(blocks.at(-1)).toBe("ForumBoards");
  });
});
