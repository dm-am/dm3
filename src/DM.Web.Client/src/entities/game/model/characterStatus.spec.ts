import { describe, it, expect } from "vitest";
import { characterStatusLabel } from "./characterStatus";
import type { Character } from "./types";

/** The status of a character and the flags that refine it. */
type Standing = {
  status: Character["status"];
  isDead?: boolean;
  isPlayerLeft?: boolean;
  isPlayerExiled?: boolean;
};

const character = (over: Standing): Character =>
  ({
    id: "c1",
    name: "Гоблин",
    isDead: false,
    isPlayerLeft: false,
    isPlayerExiled: false,
    ...over,
  }) as unknown as Character;

/**
 * The caption under a character's name for every status and every flag that
 * refines "Retired", the order of the flags included where a character
 * carries more than one.
 */
describe("characterStatusLabel", () => {
  it.each<[string, Standing, string | null]>([
    ["in the game", { status: "Active" }, null],
    ["under review", { status: "UnderReview" }, "На рассмотрении"],
    ["declined", { status: "Declined" }, null],
    ["retired with no flag", { status: "Retired" }, "Вне игры"],
    ["dead", { status: "Retired", isDead: true }, "Персонаж мертв"],
    ["left", { status: "Retired", isPlayerLeft: true }, "Покинул игру"],
    ["exiled", { status: "Retired", isPlayerExiled: true }, "Выведен из игры"],
    [
      "dead and left",
      { status: "Retired", isDead: true, isPlayerLeft: true },
      "Персонаж мертв",
    ],
    [
      "left and exiled",
      { status: "Retired", isPlayerLeft: true, isPlayerExiled: true },
      "Покинул игру",
    ],
    ["active with a flag left over", { status: "Active", isDead: true }, null],
  ])("captions a character %s: %s", (_what, over, caption) => {
    expect(characterStatusLabel(character(over))).toBe(caption);
  });
});
