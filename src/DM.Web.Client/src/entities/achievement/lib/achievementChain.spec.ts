/**
 * @vitest-environment node
 */

/**
 * The chains of the "Достижения" tiles, read off the catalogue and the tiers
 * the owner holds: which tier faces a tile, which one it counts to, and the
 * numbers it says on the way, in the unit of its metric.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  AchievementMetric,
  type AchievementCategory,
  type AchievementType,
} from "@/shared/api/models/achievements";
import {
  achievementChains,
  chainCurrent,
  chainGoal,
  chainPercent,
  chainProgressLabel,
  chainTitle,
  chainTotal,
  tierNumeral,
} from "./achievementChain";

/**
 * The tiers of one category, as the catalogue lists them: only what a chain is
 * built from is filled in.
 */
function chain(
  id: string,
  metric: AchievementMetric,
  sortOrder: number,
  tiers: [string, number, number][],
): AchievementType[] {
  const category: AchievementCategory = {
    id,
    code: id,
    title: id,
    description: "",
    iconName: "",
    metric,
    sortOrder,
    isActive: true,
  };
  return tiers.map(([title, threshold, tier]) => ({
    id: `${id}-${tier}`,
    code: `${id}-${tier}`,
    title,
    threshold,
    tier,
    category,
  }));
}

// Tiers on the scale of the server: a chain climbs bronze 3, silver 2, gold 1.
const POSTS = chain("posts", AchievementMetric.GamePostsAuthored, 2, [
  ["Летописец", 500, 2],
  ["Писатель", 100, 3],
  ["Хронист", 1000, 1],
]);
const YEARS = chain("years", AchievementMetric.DaysSinceRegistration, 1, [
  ["Год с нами", 366, 3],
  ["Пять лет", 1827, 2],
  ["Десять лет", 3653, 1],
]);
const GAMES = chain("games", AchievementMetric.GamesHosted, 3, [
  ["Ведущий", 1, 3],
]);

const taken = (...types: AchievementType[]) =>
  types.map((type) => ({ id: `u-${type.id}`, type, earnedUtc: "2025-01-01" }));

const USER = {
  username: "anna",
  registeredUtc: "2020-01-01T00:00:00Z",
  rating: { totalPosts: 650 },
  gamesHosting: 2,
} as never;

const chains = () =>
  achievementChains(
    [...GAMES, ...POSTS, ...YEARS],
    taken(POSTS[1], YEARS[0], GAMES[0]),
    USER,
  );

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-16T12:00:00Z"));
});

afterEach(() => {
  vi.useRealTimers();
});

describe("achievementChains", () => {
  it("groups the catalogue by category, in the order of the categories", () => {
    expect(chains().map((one) => one.categoryId)).toEqual([
      "years",
      "posts",
      "games",
    ]);
  });

  it("faces a chain by its highest tier taken and counts to the first one not taken", () => {
    const posts = chains()[1];

    expect(posts.tiers.map((tier) => tier.type.title)).toEqual([
      "Писатель",
      "Летописец",
      "Хронист",
    ]);
    expect(chainTitle(posts)).toBe("Писатель");
    expect(posts.next?.type.title).toBe("Летописец");
    expect([posts.earnedCount, posts.earnedTier, posts.earnedPosition]).toEqual(
      [1, 3, 1],
    );
    expect([
      chainCurrent(posts),
      chainGoal(posts),
      chainPercent(posts),
    ]).toEqual([500, 500, 100]);
    expect(chainProgressLabel(posts)).toBe("500 из 500 постов");
  });

  it("counts the years since registration in years", () => {
    const years = chains()[0];

    expect([chainCurrent(years), chainGoal(years)]).toEqual([5, 5]);
    expect(chainProgressLabel(years)).toBe("5 из 5 лет");
  });

  it("fills a chain with every tier taken and says the count reached", () => {
    const games = chains()[2];

    expect([games.completed, games.locked, games.next]).toEqual([
      true,
      false,
      null,
    ]);
    expect([chainPercent(games), chainTotal(games)]).toEqual([100, 2]);
    expect(chainProgressLabel(games)).toBe("");
  });

  it("locks a chain with nothing taken under the name of its first tier", () => {
    const [locked] = achievementChains(POSTS, [], USER);

    expect([
      locked.locked,
      locked.earned,
      locked.earnedTier,
      locked.earnedPosition,
    ]).toEqual([true, null, null, 0]);
    expect(chainTitle(locked)).toBe("Писатель");
    expect(chainTotal(locked)).toBe(0);
    expect(tierNumeral(locked.earnedPosition)).toBe("");
    expect([1, 2, 3, 4].map(tierNumeral)).toEqual(["I", "II", "III", "IV"]);
  });

  // The tier picks the metal and the order of the thresholds the numeral.
  it("numbers a tier by its place in the chain, not by its tier", () => {
    const posts = chains()[1];

    expect(posts.tiers.map((tier) => tier.position)).toEqual([1, 2, 3]);
    expect(posts.tiers.map((tier) => tier.type.tier)).toEqual([3, 2, 1]);
    expect(posts.tiers.map((tier) => tierNumeral(tier.position))).toEqual([
      "I",
      "II",
      "III",
    ]);
  });
});
