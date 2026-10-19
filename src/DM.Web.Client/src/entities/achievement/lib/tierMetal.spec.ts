// The one scale of tiers in words, and the metal a new top of a chain takes.
import { describe, it, expect } from "vitest";
import { nextChainTier, tierWord } from "./tierMetal";

describe("tierWord", () => {
  it("names every tier of the scale by its metal", () => {
    expect([1, 2, 3, 4, 5].map(tierWord)).toEqual([
      "Золото",
      "Серебро",
      "Бронза",
      "Сталь",
      "Платина",
    ]);
  });

  it("names nothing for no tier or one off the scale", () => {
    expect(tierWord(null)).toBeNull();
    expect(tierWord(0)).toBeNull();
    expect(tierWord(6)).toBeNull();
  });
});

describe("nextChainTier", () => {
  it("climbs bronze, silver, gold, platinum", () => {
    expect(nextChainTier([])).toBe(3);
    expect(nextChainTier([3])).toBe(2);
    expect(nextChainTier([3, 2])).toBe(1);
    expect(nextChainTier([2, 3, 1])).toBe(5);
  });

  it("offers nothing past platinum, and steel moves no step", () => {
    expect(nextChainTier([3, 2, 1, 5])).toBeNull();
    expect(nextChainTier([4, null])).toBe(3);
  });
});
