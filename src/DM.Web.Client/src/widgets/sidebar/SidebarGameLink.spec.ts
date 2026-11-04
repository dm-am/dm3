/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import SidebarGameLink from "./SidebarGameLink.vue";
import type { GameRef } from "@/entities/game";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: {
    template: '<span class="tooltip" :data-text="text"><slot /></span>',
    props: ["text"],
  },
};

const createMockGame = (overrides: Record<string, unknown> = {}): GameRef =>
  ({
    id: "game-1",
    publicId: "abcde",
    title: "Test Game",
    status: "Active",
    master: { id: "user-1", username: "master" },
    assistants: [],
    participation: [],
    subscribersCount: 0,
    recruitment: { isOpen: true, pcCount: 0, isSubsequent: false },
    unreadPostsCount: 0,
    unreadCommentsCount: 0,
    gameReviewsCount: 0,
    postReviewsCount: 0,
    ...overrides,
  }) as unknown as GameRef;

function render(props: Record<string, unknown> = {}) {
  return mount(SidebarGameLink, {
    props: { game: createMockGame(), ...props },
    global: { stubs },
  });
}

function starTooltipText(wrapper: ReturnType<typeof render>) {
  return wrapper
    .findAll(".tooltip")
    .find((tooltip) => tooltip.find(".star").exists())
    ?.attributes("data-text");
}

describe("SidebarGameLink", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("displays game title", () => {
    const wrapper = render({ game: createMockGame({ title: "My Game" }) });
    expect(wrapper.text()).toContain("My Game");
  });

  describe("Counters", () => {
    it("shows counters on hover", async () => {
      const wrapper = render();

      await wrapper.find(".link").trigger("mouseenter");

      expect(wrapper.find(".counters").exists()).toBe(true);
    });

    it("hides counters on mouse leave", async () => {
      const wrapper = render();

      await wrapper.find(".link").trigger("mouseenter");
      await wrapper.find(".link").trigger("mouseleave");

      expect(wrapper.find(".counters").exists()).toBe(false);
    });

    it("always shows counters when alwaysShowCounters is true", () => {
      const wrapper = render({ alwaysShowCounters: true });
      expect(wrapper.find(".counters").exists()).toBe(true);
    });
  });

  describe("Tooltips", () => {
    it("wraps game title in Tooltip", () => {
      const wrapper = render();
      expect(wrapper.findAll(".tooltip").length).toBeGreaterThanOrEqual(1);
    });

    it("renders counters as plain links without per-counter tooltips", () => {
      const wrapper = render({ alwaysShowCounters: true });

      // The only Tooltip is the game title's.
      const counters = wrapper.find(".counters");
      expect(counters.findAll(".tooltip").length).toBe(0);
      expect(counters.findAll(".router-link").length).toBe(2);
    });
  });

  // The star repeats what the server said; the lists that are not the reader's own games draw no obligations.
  describe("Wait Marker", () => {
    it("draws the star when the game awaits the viewer", () => {
      const wrapper = render({
        game: createMockGame({
          awaitsViewerTurn: true,
          awaitedCharacterNames: ["Гром", "Искра"],
        }),
        waitMarker: true,
      });

      expect(wrapper.find(".star").exists()).toBe(true);
      expect(wrapper.find(".star").attributes("aria-label")).toBe(
        "Ожидается ваш ход",
      );
      expect(starTooltipText(wrapper)).toBe("Вашего хода ждут: Гром, Искра");
    });

    it("draws no star when the game awaits somebody else", () => {
      const wrapper = render({
        game: createMockGame({
          awaitsViewerTurn: false,
          awaitedCharacterNames: [],
        }),
        waitMarker: true,
      });

      expect(wrapper.find(".star").exists()).toBe(false);
    });

    it("draws no star when the server says nothing about turns", () => {
      const wrapper = render({ waitMarker: true });

      expect(wrapper.find(".star").exists()).toBe(false);
    });

    it("draws no star without the wait-marker prop", () => {
      const wrapper = render({
        game: createMockGame({
          awaitsViewerTurn: true,
          awaitedCharacterNames: ["Гром"],
        }),
      });

      expect(wrapper.find(".star").exists()).toBe(false);
    });
  });

  describe("Router Links", () => {
    it("renders router-link for game", () => {
      const wrapper = render();
      expect(wrapper.findAll(".router-link").length).toBeGreaterThan(0);
    });

    it("renders multiple links when counters shown", () => {
      const wrapper = render({ alwaysShowCounters: true });

      // The title, the posts counter, the comments counter.
      expect(wrapper.findAll(".router-link").length).toBeGreaterThanOrEqual(3);
    });

    it("displays unread counts in counters", () => {
      const wrapper = render({
        game: createMockGame({ unreadPostsCount: 5, unreadCommentsCount: 3 }),
        alwaysShowCounters: true,
      });

      expect(wrapper.find(".counters").text()).toContain("5");
      expect(wrapper.find(".counters").text()).toContain("3");
    });
  });

  it("has aria-hidden on decorative prefix", () => {
    const wrapper = render();
    expect(wrapper.find(".muted[aria-hidden]").attributes("aria-hidden")).toBe(
      "true",
    );
  });
});
