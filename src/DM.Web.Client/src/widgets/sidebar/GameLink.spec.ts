/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { nextTick } from "vue";
import GameLink from "./GameLink.vue";
import type { GameRef, GameId, GameStatus, GameRecruitment, GameRole } from "@/entities/game";
import type { Served } from "@/shared/api/models";
import type { UserRef } from "@/shared/api/models/common";

// Helper to cast raw values to Served type for test mocks
function asServed<T>(value: T): Served<T> {
  return value as Served<T>;
}

// Stub router-link
config.global.stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: {
    template: '<span class="tooltip"><slot /></span>',
    props: ["text"],
  },
};

// Mock useGameDisplay composable
vi.mock("@/entities/game", async (importOriginal) => {
  const actual = await importOriginal() as any;
  return {
    ...actual,
    useGameDisplay: () => ({
      buildTooltip: (game: any) => `Tooltip for ${game.title}`,
      getUnreadPosts: () => 5,
      getUnreadComments: () => 3,
      formatUnreadPostsTooltip: (count: number) => `${count} непрочитанных постов`,
      formatUnreadCommentsTooltip: (count: number) => `${count} непрочитанных комментариев`,
      isNew: () => false,
    }),
  };
});

const createMockUserRef = (id: string, username: string): UserRef => ({
  id: id,
  username,
  lastActivityUtc: "2024-01-01T00:00:00Z",
} as unknown as UserRef);

const createMockRecruitment = (): GameRecruitment => ({
  isOpen: true,
  pcCount: 0,
  isSubsequent: false,
});

const createMockGame = (overrides: Partial<{ id: string; title: string }> = {}): GameRef => ({
  id: asServed((overrides.id ?? "game-1") as GameId),
  publicId: asServed("abcde"),
  title: overrides.title ?? "Test Game",
  status: "Active" as GameStatus,
  master: asServed(createMockUserRef("user-1", "master")),
  assistants: asServed([]),
  participation: asServed([]),
  subscribersCount: 0,
  recruitment: asServed(createMockRecruitment()),
  unreadPostsCount: asServed(0),
  unreadCommentsCount: asServed(0),
  gameReviewsCount: asServed(0),
  postReviewsCount: asServed(0),
});

describe("GameLink", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders without errors", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false },
      });
      expect(wrapper.exists()).toBe(true);
    });

    it("displays game title", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame({ title: "My Game" }), counters: false },
      });
      expect(wrapper.text()).toContain("My Game");
    });

    it("has link class on container", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false },
      });
      expect(wrapper.find(".link").exists()).toBe(true);
    });

    it("renders default prefix", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false },
      });
      expect(wrapper.text()).toContain("-");
    });

    it("renders custom prefix", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false, prefix: "* " },
      });
      expect(wrapper.text()).toContain("*");
    });
  });

  // ============================================================================
  // COUNTERS
  // ============================================================================

  describe("Counters", () => {
    it("hides counters when counters prop is false", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false },
      });
      expect(wrapper.find(".counters").exists()).toBe(false);
    });

    it("shows counters on hover when counters prop is true", async () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true },
      });

      await wrapper.find(".link").trigger("mouseenter");
      await nextTick();

      expect(wrapper.find(".counters").exists()).toBe(true);
    });

    it("hides counters on mouse leave", async () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true },
      });

      await wrapper.find(".link").trigger("mouseenter");
      await nextTick();
      await wrapper.find(".link").trigger("mouseleave");
      await nextTick();

      expect(wrapper.find(".counters").exists()).toBe(false);
    });

    it("always shows counters when alwaysShowCounters is true", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true, alwaysShowCounters: true },
      });
      expect(wrapper.find(".counters").exists()).toBe(true);
    });
  });

  // ============================================================================
  // NEW GAME INDICATOR
  // ============================================================================

  describe("New Game Indicator", () => {
    it("renders game title in link", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame({ title: "Epic Adventure" }), counters: false },
      });

      // Component should render the game title
      expect(wrapper.text()).toContain("Epic Adventure");
    });
  });

  // ============================================================================
  // TOOLTIPS
  // ============================================================================

  describe("Tooltips", () => {
    it("wraps game title in Tooltip", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false },
      });
      // Stub renders as <span class="tooltip">
      const tooltips = wrapper.findAll(".tooltip");
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it("wraps counters in Tooltips when shown", async () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true, alwaysShowCounters: true },
      });

      // Stub renders as <span class="tooltip">
      // Should have tooltips for posts and comments counters
      const tooltips = wrapper.findAll(".tooltip");
      expect(tooltips.length).toBeGreaterThanOrEqual(3); // game + posts + comments
    });
  });

  // ============================================================================
  // ROUTER LINKS
  // ============================================================================

  describe("Router Links", () => {
    it("renders router-link for game", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame({ id: "game-123" }), counters: false },
      });

      const links = wrapper.findAll(".router-link");
      expect(links.length).toBeGreaterThan(0);
    });

    it("renders multiple links when counters shown", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame({ id: "game-123" }), counters: true, alwaysShowCounters: true },
      });

      // Should have links for game title, posts counter, comments counter
      const links = wrapper.findAll(".router-link");
      expect(links.length).toBeGreaterThanOrEqual(3);
    });

    it("displays unread counts in counters", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true, alwaysShowCounters: true },
      });

      // Mock returns 5 posts and 3 comments
      expect(wrapper.text()).toContain("5");
      expect(wrapper.text()).toContain("3");
    });
  });

  // ============================================================================
  // ACCESSIBILITY
  // ============================================================================

  describe("Accessibility", () => {
    it("has aria-hidden on decorative prefix", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: false },
      });
      const prefix = wrapper.find(".muted[aria-hidden]");
      expect(prefix.attributes("aria-hidden")).toBe("true");
    });

    it("renders counter links when counters shown", () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true, alwaysShowCounters: true },
      });

      const counters = wrapper.find(".counters");
      expect(counters.exists()).toBe(true);
    });
  });

  // ============================================================================
  // HOVER STATE
  // ============================================================================

  describe("Hover State", () => {
    it("tracks hover state", async () => {
      const wrapper = mount(GameLink, {
        props: { game: createMockGame(), counters: true },
      });

      // Initially not hovered
      expect(wrapper.find(".counters").exists()).toBe(false);

      // Hover
      await wrapper.find(".link").trigger("mouseenter");
      await nextTick();
      expect(wrapper.find(".counters").exists()).toBe(true);

      // Leave
      await wrapper.find(".link").trigger("mouseleave");
      await nextTick();
      expect(wrapper.find(".counters").exists()).toBe(false);
    });
  });
});
