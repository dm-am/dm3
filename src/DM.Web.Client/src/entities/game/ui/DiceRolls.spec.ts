/**
 * @vitest-environment jsdom
 */

/**
 * The rolls of a post stand on a grey strip in the old site's words since
 * 4 October 2026: the heading "Результаты бросков:", then one numbered line
 * per roll; the words of a line are diceLine.spec.ts. Every roll there is
 * stands on the strip: the old site never folds the rolls of a post, however
 * many. Where the strip stands in a post is GamePost.spec.ts.
 */
import { describe, it, expect } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import type { DiceRoll } from "../model/diceTypes";
import DiceRolls from "./DiceRolls.vue";

describe("GamePost dice rolls", () => {
  /** The line as a reader sees it: the joins that hold it together taken out. */
  const seen = (text: string) =>
    text
      .split(String.fromCharCode(0x2060))
      .join("")
      .replace(/\s+/g, " ")
      .trim();

  const rollOf = (value: number, comment?: string) => ({
    rolls: 1,
    edges: 20,
    bonus: 0,
    results: [{ value }],
    comment,
  });

  const withRolls = (count: number) =>
    Array.from({ length: count }, (_, i) =>
      rollOf(i + 1, `Бросок ${i + 1}`),
    ) as DiceRoll[];

  const rows = (wrapper: VueWrapper) =>
    wrapper.findAll(".dice-rolls > div").map((row) => seen(row.text()));

  it("shows every roll of a long list, in order, with nothing to press", async () => {
    const wrapper = mount(DiceRolls, { props: { rolls: withRolls(10) } });

    expect(rows(wrapper)).toEqual([
      "Результаты бросков:",
      ...Array.from(
        { length: 10 },
        (_, i) => `${i + 1}) 1D20: ${i + 1} - "Бросок ${i + 1}"`,
      ),
    ]);
    expect(wrapper.get(".dice-rolls").find("button").exists()).toBe(false);
    expect(wrapper.get(".dice-rolls").text()).not.toMatch(/\.\.\.|Показать/);
    wrapper.unmount();
  });
});
