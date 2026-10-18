/**
 * @vitest-environment jsdom
 */

/**
 * The "Достижения" tiles as a reader of a profile sees them, each chain in one
 * of its states: some tiers taken, none taken, all taken, and the years since
 * registration counted in years. What a tile says, how far its bar is filled,
 * and what its hint lists.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount, type DOMWrapper } from "@vue/test-utils";
import { achievementApi } from "@/entities/achievement";
import {
  AchievementMetric,
  type AchievementType,
} from "@/shared/api/models/achievements";
import ProfileAchievementsSection from "./ProfileAchievementsSection.vue";

const category = (
  id: string,
  metric: AchievementMetric,
  sortOrder: number,
) => ({
  id,
  code: id,
  title: `Цепочка ${id}`,
  description: `Что считает ${id}`,
  iconName: `icon-${id}`,
  metric,
  sortOrder,
  isActive: true,
});

function chain(
  id: string,
  metric: AchievementMetric,
  sortOrder: number,
  tiers: [string, number][],
): AchievementType[] {
  const of = category(id, metric, sortOrder);
  return tiers.map(([title, threshold], at) => ({
    id: `${id}-${at + 1}`,
    code: `${id}-${at + 1}`,
    title,
    threshold,
    tier: at + 1,
    category: of,
  }));
}

const POSTS = chain("posts", AchievementMetric.GamePostsAuthored, 2, [
  ["Писатель", 100],
  ["Летописец", 500],
  ["Хронист", 1000],
  ["Сказитель", 5000],
]);
const YEARS = chain("years", AchievementMetric.DaysSinceRegistration, 1, [
  ["Год с нами", 366],
  ["Пять лет", 1827],
  ["Десять лет", 3653],
]);
const GAMES = chain("games", AchievementMetric.GamesHosted, 3, [
  ["Ведущий", 1],
  ["Мастер", 5],
]);
const BLOGS = chain("blogs", AchievementMetric.BlogsHosted, 4, [
  ["Блогер", 1],
  ["Летописец блогов", 3],
]);

const taken = (...types: AchievementType[]) =>
  types.map((type) => ({ id: `u-${type.id}`, type, earnedUtc: "2025-01-01" }));

const USER = {
  username: "anna",
  registeredUtc: "2020-01-01T00:00:00Z",
  rating: { totalPosts: 650 },
  gamesHosting: 8,
  blogsHosting: 0,
};

const stubs = {
  GameIcon: { template: "<i class='icon' />" },
  Tooltip: {
    template:
      "<div class='tile-wrap'><slot /><div class='popover'><slot name='content' /></div></div>",
  },
};

async function section(answers: { earned?: unknown; catalog?: unknown }) {
  vi.spyOn(achievementApi, "getUserAchievements").mockResolvedValue(
    (answers.earned ?? { data: { resources: [] }, error: null }) as never,
  );
  vi.spyOn(achievementApi, "getAchievementTypes").mockResolvedValue(
    (answers.catalog ?? { data: { resources: [] }, error: null }) as never,
  );
  const wrapper = mount(ProfileAchievementsSection, {
    props: { username: "anna", user: USER as never },
    global: { stubs },
  });
  await flushPromises();
  return wrapper;
}

async function tiles() {
  const wrapper = await section({
    earned: {
      data: {
        resources: taken(POSTS[0], POSTS[1], YEARS[0], YEARS[1], ...GAMES),
      },
      error: null,
    },
    catalog: {
      data: { resources: [...BLOGS, ...GAMES, ...POSTS, ...YEARS] },
      error: null,
    },
  });
  return { wrapper, tiles: wrapper.findAll(".tile-wrap") };
}

const faceOf = (tile: DOMWrapper<Element>) => {
  const bar = tile.get(".chain-bar");
  return {
    title: tile.get(".chain-title").text(),
    degree: tile.find(".chain-degree").exists()
      ? tile.get(".chain-degree").text()
      : null,
    locked: tile.get(".chain").classes().includes("chain--locked"),
    numbers: tile.get(".chain-progress").text(),
    filled: bar.attributes("aria-valuenow"),
    said: bar.attributes("aria-label"),
  };
};

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-16T12:00:00Z"));
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("ProfileAchievementsSection: the tiles", () => {
  it("draws every chain of the catalogue in the catalogue's order", async () => {
    const { tiles: all } = await tiles();

    expect(all.map((tile) => tile.get(".chain-title").text())).toEqual([
      "Пять лет",
      "Летописец",
      "Мастер",
      "Блогер",
    ]);
  });

  it("faces a chain with tiers taken by its highest one, and counts to the next", async () => {
    const { tiles: all } = await tiles();

    expect(faceOf(all[1])).toEqual({
      title: "Летописец",
      degree: "II",
      locked: false,
      numbers: "650 / 1000",
      filled: "65",
      said: "Цепочка posts: 650/1000",
    });
  });

  it("counts the years since registration in years", async () => {
    const { tiles: all } = await tiles();

    expect(faceOf(all[0])).toMatchObject({
      title: "Пять лет",
      degree: "II",
      numbers: "7 / 10",
      filled: "68",
    });
  });

  it("fills the bar of a chain with every tier taken and shows the count reached", async () => {
    const { tiles: all } = await tiles();

    expect(faceOf(all[2])).toEqual({
      title: "Мастер",
      degree: "II",
      locked: false,
      numbers: "8",
      filled: "100",
      said: "Цепочка games: завершено",
    });
  });

  it("ghosts a chain with nothing taken under the name of its first tier", async () => {
    const { tiles: all } = await tiles();

    expect(faceOf(all[3])).toEqual({
      title: "Блогер",
      degree: null,
      locked: true,
      numbers: "0 / 1",
      filled: "0",
      said: "Цепочка blogs: 0/1",
    });
  });
});

describe("ProfileAchievementsSection: the hint of a tile", () => {
  it("names the chain, counts the tiers taken, says the way to the next and lists them all", async () => {
    const { tiles: all } = await tiles();
    const hint = all[1].get(".popover");

    expect(hint.get(".chain-popover__title").text()).toBe("Цепочка posts");
    expect(hint.get(".chain-popover__badge").text()).toBe("2 / 4");
    expect(hint.get(".chain-popover__desc").text()).toBe("Что считает posts");
    expect(hint.get(".chain-popover__progress").text()).toBe(
      "650 из 1000 постов",
    );
    expect(
      hint
        .findAll(".chain-popover__tier")
        .map((tier) => [
          tier.get(".chain-popover__tier-degree").text(),
          tier.get(".chain-popover__tier-title").text(),
          tier.get(".chain-popover__tier-threshold").text(),
          tier.classes().includes("chain-popover__tier--earned"),
        ]),
    ).toEqual([
      ["I", "Писатель", "100 постов", true],
      ["II", "Летописец", "500 постов", true],
      ["III", "Хронист", "1000 постов", false],
      ["IV", "Сказитель", "5000 постов", false],
    ]);
  });

  it("says the way to the next tier in years for the registration", async () => {
    const { tiles: all } = await tiles();

    expect(all[0].get(".chain-popover__progress").text()).toBe("7 из 10 лет");
  });

  it("says no way onward for a chain with every tier taken", async () => {
    const { tiles: all } = await tiles();

    expect(all[2].find(".chain-popover__progress").exists()).toBe(false);
  });
});

describe("ProfileAchievementsSection: what it tells the tab", () => {
  it("reports content once a catalogue is in", async () => {
    const { wrapper } = await tiles();

    expect(wrapper.emitted("state")).toEqual([["loading"], ["content"]]);
  });

  it("reports empty and draws nothing for an empty catalogue", async () => {
    const wrapper = await section({});

    expect(wrapper.emitted("state")).toEqual([["loading"], ["empty"]]);
    expect(wrapper.find(".achievements-section").exists()).toBe(false);
  });

  it("says its failure in its own box and reports it", async () => {
    const wrapper = await section({
      catalog: { data: null, error: { status: 500, title: "" } },
    });

    expect(wrapper.text()).toContain("Не удалось загрузить достижения");
    expect(wrapper.emitted("state")).toEqual([["loading"], ["error"]]);
    expect(wrapper.find(".chain").exists()).toBe(false);
  });
});
