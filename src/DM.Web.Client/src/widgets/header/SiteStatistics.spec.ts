/**
 * @vitest-environment jsdom
 */

/** The four lines of the statistics: their words, where they break, their colours. */
import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import SiteStatistics from "./SiteStatistics.vue";

const LARGE = {
  online: 1234,
  totals: {
    users: { value: 13487, todayDelta: 12 },
    characters: { value: 98765, todayDelta: 34 },
    games: { value: 12345, todayDelta: 5 },
    gamePosts: { value: 2831945, todayDelta: 1234 },
    blogs: { value: 1204, todayDelta: 1 },
    publications: { value: 21530, todayDelta: 12 },
  },
};

/** What the server answers, set by each test before it mounts; null refuses. */
const answer = vi.hoisted(() => ({ stats: null as unknown, pending: false }));

vi.mock("@/entities/statistics/api/statisticsApi", () => ({
  default: {
    getLiveStats: () =>
      answer.pending
        ? new Promise(() => {})
        : Promise.resolve(
            answer.stats
              ? { data: { resource: answer.stats }, error: null }
              : { data: null, error: { status: 500, title: "" } },
          ),
  },
}));

enableAutoUnmount(afterEach);

async function open() {
  const wrapper = mount(SiteStatistics);
  await flushPromises();
  return wrapper;
}

/** The words of an element as a selection copies them. */
const words = (el: Element) =>
  (el.textContent ?? "").replace(/\s+/g, " ").trim();

beforeEach(() => {
  setActivePinia(createPinia());
  answer.stats = LARGE;
  answer.pending = false;
});

describe("the lines of the statistics", () => {
  it("read as before", async () => {
    const rows = (await open()).findAll(".stat-row");

    expect(rows.map((row) => words(row.element))).toEqual([
      "Пользователей: 13 487 [+12], online: 1234",
      "Персонажей: 98 765 [+34]",
      "Игр: 12 345 [+5], постов: 2 831 945 [+1234]",
      "Блогов: 1 204 [+1], публикаций: 21 530 [+12]",
    ]);
  });

  it("break after the comma and nowhere else", async () => {
    const rows = (await open()).findAll(".stat-row");

    for (const row of rows) {
      const children = Array.from(row.element.childNodes).filter(
        (node) =>
          node.nodeType === Node.ELEMENT_NODE ||
          (node.nodeType === Node.TEXT_NODE && node.textContent?.trim()),
      );
      // A count, then for each next count a place to break and the count.
      const shape = children.map((node) =>
        node instanceof HTMLElement
          ? node.tagName === "WBR"
            ? "break"
            : node.className
          : "text",
      );
      const parts = row.findAll(".stat-part");
      expect(shape, words(row.element)).toEqual(
        parts.flatMap((_, at) => (at ? ["break", "stat-part"] : ["stat-part"])),
      );
      // The comma and the space are the end of the count before the break.
      for (const part of parts.slice(0, -1))
        expect(part.element.textContent).toMatch(/\], $/);
    }
  });

  // Split in two text nodes, a phone draws the first digit of the count a
  // fraction of a pixel apart.
  it("hold a label and its count in one text node", async () => {
    const parts = (await open()).findAll(".stat-part");

    const leads = parts.map((part) =>
      Array.from(part.element.childNodes)
        .find((node) => node.nodeType === Node.TEXT_NODE && node.textContent)
        ?.textContent?.replace(/\s+/g, " ")
        .trim(),
    );
    expect(leads).toEqual([
      "Пользователей: 13 487",
      "online:",
      "Персонажей: 98 765",
      "Игр: 12 345",
      "постов: 2 831 945",
      "Блогов: 1 204",
      "публикаций: 21 530",
    ]);
  });
});

/** Green for what is good: a count that grew, readers online; "+0" stays grey. */
describe("the colour of a count", () => {
  const GROWN = "rising";

  it("is green where the day added something, grey where it added nothing", async () => {
    answer.stats = {
      online: 0,
      totals: {
        ...LARGE.totals,
        users: { value: 22, todayDelta: 0 },
        games: { value: 70, todayDelta: 3 },
      },
    };
    const w = await open();
    const deltas = w.findAll(".delta");
    const byText = Object.fromEntries(
      deltas.map((d) => [
        words(d.element.closest(".stat-part")!).split(":")[0],
        d.classes(),
      ]),
    );

    expect(byText["Пользователей"]).not.toContain(GROWN);
    expect(byText["Игр"]).toContain(GROWN);
    expect(w.get(".online").text()).toBe("0");
    expect(w.get(".online").classes()).not.toContain(GROWN);
  });

  it("is green for readers online, as many as there are", async () => {
    const w = await open();
    expect(w.get(".online").classes()).toContain(GROWN);
    for (const delta of w.findAll(".delta"))
      expect(delta.classes()).toContain(GROWN);
  });

  it("is grey where the numbers did not come", async () => {
    answer.stats = null;
    const w = await open();
    expect(w.findAll(".delta, .online")).not.toHaveLength(0);
    for (const count of w.findAll(".delta, .online"))
      expect(count.classes()).not.toContain(GROWN);
  });
});
