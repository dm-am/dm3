/**
 * @vitest-environment jsdom
 */

/**
 * The four lines of the statistics, wherever they stand: the corner of the
 * header, or the top of the right column of a phone.
 *
 * The column of a phone is narrower than the header's corner, and with numbers
 * of the old site's size (13 487 players, 2 831 945 posts) a line runs past
 * it. A line then breaks after its comma and nowhere else: the second count
 * goes under the first whole, never a word or a number of it. What a reader
 * copies is the line as before.
 */
import { beforeEach, describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { computed, ref } from "vue";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";

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

/** What the server answered, set by each test before it mounts. */
const answer = vi.hoisted(() => ({ stats: null as unknown, loaded: true }));

vi.mock("@/entities/statistics", () => ({
  useStatisticsStore: () => {
    const stats = ref(answer.stats as typeof LARGE | null);
    const total = (key: keyof typeof LARGE.totals) =>
      computed(() => stats.value?.totals[key] ?? { value: 0, todayDelta: 0 });
    return {
      stats,
      loaded: ref(answer.loaded),
      online: computed(() => stats.value?.online ?? 0),
      users: total("users"),
      characters: total("characters"),
      games: total("games"),
      posts: total("gamePosts"),
      blogs: total("blogs"),
      publications: total("publications"),
      startPolling: vi.fn(),
      stopPolling: vi.fn(),
    };
  },
}));

// storeToRefs over the plain object above: its refs are taken as they are.
vi.mock("pinia", async (original) => ({
  ...(await original<typeof import("pinia")>()),
  storeToRefs: (store: object) => store,
}));

const { default: SiteStatistics } = await import("./SiteStatistics.vue");

/** The words of an element as a selection copies them. */
const words = (el: Element) =>
  (el.textContent ?? "").replace(/\s+/g, " ").trim();

const rules = styleRules(
  componentCss(
    resolve(dirname(fileURLToPath(import.meta.url)), "SiteStatistics.vue"),
  ),
);
/** What the rules say of a selector, alone or in a list of them. */
const of = (selector: string) =>
  rules.find((rule) =>
    rule.selector.split(",").some((one) => one.trim() === selector),
  )?.declarations ?? {};

beforeEach(() => {
  answer.stats = LARGE;
  answer.loaded = true;
});

describe("the lines of the statistics", () => {
  it("read as before", () => {
    const rows = mount(SiteStatistics).findAll(".stat-row");

    expect(rows.map((row) => words(row.element))).toEqual([
      "Пользователей: 13 487 [+12], online: 1234",
      "Персонажей: 98 765 [+34]",
      "Игр: 12 345 [+5], постов: 2 831 945 [+1234]",
      "Блогов: 1 204 [+1], публикаций: 21 530 [+12]",
    ]);
  });

  it("break after the comma and nowhere else", () => {
    const rows = mount(SiteStatistics).findAll(".stat-row");

    for (const row of rows) {
      const children = Array.from(row.element.childNodes).filter(
        (node) => node.nodeType !== Node.TEXT_NODE || node.textContent?.trim(),
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

    expect(of(".stat-part")["white-space"]).toBe("nowrap");
    expect(of(".site-stats")["white-space"]).toBeUndefined();
  });
});

/**
 * Green is for what is good (the owner's rule of colour): a count of the day
 * that grew, and readers online. Nothing is neither good nor bad, so a "+0"
 * and "online: 0" are grey, as the brackets around them are.
 */
describe("the colour of a count", () => {
  const GROWN = "rising";

  it("is green where the day added something, grey where it added nothing", () => {
    answer.stats = {
      online: 0,
      totals: {
        ...LARGE.totals,
        users: { value: 22, todayDelta: 0 },
        games: { value: 70, todayDelta: 3 },
      },
    };
    const w = mount(SiteStatistics);
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

  it("is green for readers online, as many as there are", () => {
    const w = mount(SiteStatistics);
    expect(w.get(".online").classes()).toContain(GROWN);
    for (const delta of w.findAll(".delta"))
      expect(delta.classes()).toContain(GROWN);
  });

  it("is grey where the numbers did not come", () => {
    answer.stats = null;
    const w = mount(SiteStatistics);
    for (const count of w.findAll(".delta, .online"))
      expect(count.classes()).not.toContain(GROWN);
  });

  it("takes the ink of the theme: grey by default, green when grown", () => {
    expect(of(".delta").color).toBe("var(--text-muted)");
    expect(of(".online").color).toBe("var(--text-muted)");
    expect(of(".delta.rising").color).toBe("var(--accent-green)");
    expect(of(".online.rising").color).toBe("var(--accent-green)");
  });
});

describe("the class its place gives the block", () => {
  // The sliding column of a narrow window spaces the statistics off the
  // column under them by a class (.drawer-lead in App.vue), and that class
  // has to stand on the placeholder of the numbers too, or the column jumps
  // when they arrive.
  it("stands on the placeholder while the numbers are on their way, as on the lines", () => {
    answer.loaded = false;
    const waiting = mount(SiteStatistics, { attrs: { class: "drawer-lead" } });
    expect(waiting.get(".site-stats-skeleton").classes()).toContain(
      "drawer-lead",
    );

    answer.loaded = true;
    const loaded = mount(SiteStatistics, { attrs: { class: "drawer-lead" } });
    expect(loaded.get(".site-stats").classes()).toContain("drawer-lead");
  });
});
