/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import PeriodDigestBoards from "./PeriodDigestBoards.vue";
import {
  expandAll,
  collapseAll,
  clearRegistry,
} from "@/shared/lib/composables/useExpandableRegistry";
import communityApi from "@/shared/api/communityApi";

vi.mock("@/shared/api/communityApi", () => ({
  default: { getLeaderboards: vi.fn() },
}));

const entry = (name: string, rank: number) => ({
  rank,
  entityId: `id-${name}-${rank}`,
  publicId: null,
  name,
  score: 100 - rank,
});

/** Six entries per board so the top-5 teaser cap is observable. */
const board = (name: string) =>
  Array.from({ length: 6 }, (_, i) => entry(name, i + 1));

const leaderboards = {
  period: { year: 2026, month: 6 },
  topPlayersByRating: board("rating-player"),
  topGamesByRating: board("rating-game"),
  topBlogsByRating: board("rating-blog"),
  topPlayersByPosts: board("posts-player"),
  topGamesByPosts: board("posts-game"),
  topBlogsByPosts: board("posts-blog"),
  topPlayersByVolume: board("volume-player"),
  topBlogAuthorsByVolume: board("volume-author"),
};

const getLeaderboards = vi.mocked(communityApi.getLeaderboards);

describe("PeriodDigestBoards", () => {
  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
    getLeaderboards.mockReset();
    getLeaderboards.mockResolvedValue({
      data: { resource: leaderboards },
      error: undefined,
    } as never);
  });

  const mountComponent = (props: Record<string, unknown> = {}) =>
    mount(PeriodDigestBoards, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: { template: "<a><slot /></a>" } },
      },
      props: { year: 2026, month: 6, ...props },
    });

  it("renders the three-board top-5 teaser with the reveal button", async () => {
    const wrapper = mountComponent();
    await flushPromises();
    const boards = wrapper.findAll(".stat-board");
    expect(boards).toHaveLength(3);
    expect(boards[0].findAll("li")).toHaveLength(5);
    expect(wrapper.find(".digest-expand-button").exists()).toBe(true);
    expect(getLeaderboards).toHaveBeenCalledWith(2026, 6);
  });

  it("expands to all boards top-10 on click and drops the button", async () => {
    const wrapper = mountComponent();
    await flushPromises();
    await wrapper.find(".digest-expand-button").trigger("click");
    expect(wrapper.findAll(".stat-board")).toHaveLength(8);
    expect(wrapper.findAll(".stat-board")[0].findAll("li")).toHaveLength(6);
    expect(wrapper.find(".digest-expand-button").exists()).toBe(false);
  });

  it("starts fully expanded on the topic page", async () => {
    const wrapper = mountComponent({ expanded: true });
    await flushPromises();
    expect(wrapper.findAll(".stat-board")).toHaveLength(8);
    expect(wrapper.find(".digest-expand-button").exists()).toBe(false);
  });

  it("follows the page-wide expand/collapse-all toggle", async () => {
    const wrapper = mountComponent();
    await flushPromises();

    expandAll();
    await wrapper.vm.$nextTick();
    expect(wrapper.findAll(".stat-board")).toHaveLength(8);

    collapseAll();
    await wrapper.vm.$nextTick();
    expect(wrapper.findAll(".stat-board")).toHaveLength(3);
    expect(wrapper.find(".digest-expand-button").exists()).toBe(true);
  });

  it("auto-expands when mounted after a bulk expand (late registration)", async () => {
    expandAll();
    const wrapper = mountComponent();
    await flushPromises();
    expect(wrapper.findAll(".stat-board")).toHaveLength(8);
  });

  it("does not register started-expanded instances with the registry", async () => {
    const wrapper = mountComponent({ expanded: true });
    await flushPromises();
    collapseAll();
    await wrapper.vm.$nextTick();
    expect(wrapper.findAll(".stat-board")).toHaveLength(8);
  });
});
