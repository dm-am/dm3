/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import StatBoard from "../ui/StatBoard.vue";
import type { LeaderboardEntry } from "@/shared/api/models/community";

const entry = (over: Partial<LeaderboardEntry> = {}): LeaderboardEntry => ({
  rank: 1,
  entityId: "00000000-0000-0000-0000-000000000001",
  publicId: null,
  name: "Player",
  score: 5,
  ...over,
});

describe("StatBoard", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  const mountComponent = (props: Record<string, unknown> = {}) =>
    mount(StatBoard, {
      global: {
        // Fresh pinia per mount; the auth store seeds itself from
        // localStorage, so tests control the signed-in user via the "user"
        // key before mounting.
        plugins: [createPinia()],
        stubs: {
          RouterLink: { template: "<a><slot /></a>" },
        },
      },
      props: {
        title: "Лучший игрок по сумме оценок",
        kind: "player",
        entries: [],
        ...props,
      },
    });

  it("renders skeleton rows while loading", () => {
    const wrapper = mountComponent({ loading: true, skeletonRows: 10 });
    expect(wrapper.findAll(".stat-board-skeleton-row")).toHaveLength(10);
  });

  it("renders the empty text when there are no entries", () => {
    const wrapper = mountComponent({ emptyText: "Данных за этот период нет" });
    expect(wrapper.text()).toContain("Данных за этот период нет");
  });

  it("renders the server's ordinal ranks", () => {
    const wrapper = mountComponent({
      entries: [
        entry({ rank: 1, name: "a", score: 9, entityId: "1" }),
        entry({ rank: 2, name: "b", score: 5, entityId: "2" }),
        entry({ rank: 3, name: "c", score: 5, entityId: "3" }),
        entry({ rank: 4, name: "d", score: 3, entityId: "4" }),
      ],
    });
    const rows = wrapper.findAll(".stat-board-list li");
    expect(rows.map((r) => r.text().trim()[0])).toEqual(["1", "2", "3", "4"]);
  });

  it("groups digits of large scores", () => {
    const wrapper = mountComponent({
      entries: [entry({ score: 1234567 })],
    });
    // ru-RU grouping uses non-breaking spaces (U+00A0) between digit groups;
    // build the expectation the same way to stay engine-agnostic.
    expect(wrapper.find(".score-pos").text()).toBe(
      `+${(1234567).toLocaleString("ru-RU")}`,
    );
  });

  it("emphasizes the signed-in viewer's own row on player boards", () => {
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    const wrapper = mountComponent({
      entries: [
        entry({ name: "SolohinLex", entityId: "1" }),
        entry({ name: "Other", entityId: "2", rank: 2 }),
      ],
    });
    const rows = wrapper.findAll(".stat-board-list li");
    expect(rows[0].classes()).toContain("own");
    expect(rows[1].classes()).not.toContain("own");
  });

  it("does not emphasize rows on game boards even for a name match", () => {
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    const wrapper = mountComponent({
      kind: "game",
      entries: [entry({ name: "SolohinLex", publicId: "aaaaa" })],
    });
    expect(wrapper.find(".stat-board-list li").classes()).not.toContain("own");
  });
});
