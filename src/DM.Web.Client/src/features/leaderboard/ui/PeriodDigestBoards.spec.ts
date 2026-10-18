/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from "vitest";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import PeriodDigestBoards from "./PeriodDigestBoards.vue";
import {
  expandAll,
  collapseAll,
  clearRegistry,
} from "@/shared/lib/composables/useExpandableRegistry";
import { statisticsApi } from "@/entities/statistics";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const SFC = join(
  dirname(fileURLToPath(import.meta.url)),
  "PeriodDigestBoards.vue",
);

vi.mock("@/entities/statistics", () => ({
  statisticsApi: { getLeaderboards: vi.fn() },
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

const getLeaderboards = vi.mocked(statisticsApi.getLeaderboards);

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

/**
 * How many boards stand side by side is decided by the width of the card, not
 * of the window: at 1024 the middle column is 285 between two sidebars, and
 * three boards sized by their own names stood 155 into the right column.
 * jsdom lays nothing out, so what is read is the stylesheet that decides.
 */
describe("PeriodDigestBoards columns", () => {
  const css = componentCss(SFC);
  const queries = [
    ...css.matchAll(
      /@container stat-boards \(min-width: ([\d.]+)em\) \{([\s\S]*?\})\s*\}/g,
    ),
  ].map(([, em, body]) => ({
    from: Number(em) * 16,
    columns: ruleMap(body).get(".boards-grid")?.["grid-template-columns"],
  }));
  const outside = ruleMap(
    queries.length ? css.replace(/@container[\s\S]*?\}\s*\}/g, "") : css,
  );

  // On the stand, in the site's face at 16: a login of twenty letters,
  // "LongestLoginPossible", is 147.2 wide; a board adds its 16 on each side.
  const NARROWEST_BOARD = 147.2 + 2 * 16;
  const GAP = 16;

  it("asks the card it stands in, and lets no float narrow it", () => {
    expect(outside.get(".digest-boards")).toMatchObject({
      container: "stat-boards/inline-size",
      clear: "both",
    });
    expect(css).not.toMatch(/@media/);
  });

  it("stands one board under another in a narrow card", () => {
    expect(outside.get(".boards-grid")?.["grid-template-columns"]).toBe(
      "minmax(0, 1fr)",
    );
  });

  it("puts two and then three side by side as the card widens", () => {
    expect(queries.map((q) => q.columns)).toEqual([
      "repeat(2, minmax(0, 1fr))",
      "repeat(3, minmax(0, 1fr))",
    ]);
    expect(queries[0].from).toBeLessThan(queries[1].from);
  });

  it("gives every board room for a login on a line of its own", () => {
    const [two, three] = queries;
    expect((two.from - GAP) / 2).toBeGreaterThanOrEqual(NARROWEST_BOARD);
    expect((three.from - 2 * GAP) / 3).toBeGreaterThanOrEqual(NARROWEST_BOARD);
  });

  it("keeps three boards side by side on a wide window, as before", () => {
    // The card of a news item at 1366: the column of 627 less the card's own
    // 16 on either side and its border.
    expect(queries[1].from).toBeLessThanOrEqual(627 - 2 * 17);
  });
});
