/**
 * A published roll is a numbered line in the old site's words since 4 October
 * 2026: `1) 1D20+7: 25 - "Восприятие"`, under one heading "Результаты
 * бросков:", with no "Результат броска" in front and no period after. Every
 * form the server can publish is spelled out here, because the line is the
 * only place the reader checks a roll: one die shows its total, several dice
 * show every die and the sum, a negative bonus is a minus (the old site
 * printed "+-10"), and an exploded die carries a star per explosion and is
 * summed with its rerolls.
 *
 * The composer lists what it will send in the same words, without a result:
 * `1) 1D20+5 - "Восприятие"`. One formula serves both, so the list in the
 * form and the post under it never spell a roll two ways.
 *
 * The joins that keep a line readable where it wraps are asserted apart: the
 * number stays with the formula, "=" with the total and "-" with the comment.
 */
import { describe, it, expect } from "vitest";
import type { DiceRoll } from "@/entities/game";
import {
  DICE_RESULTS_HEADING,
  diceFormula,
  formatDiceRoll,
  formatPendingRoll,
} from "./diceLine";

/** The line as a reader sees it: the invisible joins taken out. */
const seen = (line: string) =>
  line.replace(/\u00A0/g, " ").replace(/\u2060/g, "");

const roll = (
  over: Partial<DiceRoll> & { values?: (number | [number, "exploded"])[] },
): DiceRoll => {
  const { values = [], ...rest } = over;
  return {
    rolls: 1,
    edges: 20,
    bonus: 0,
    explosion: 0,
    results: values.map((v) =>
      Array.isArray(v) ? { value: v[0], exploded: true } : { value: v },
    ),
    ...rest,
  };
};

describe("formatDiceRoll", () => {
  it("is headed by the old site's line", () => {
    expect(DICE_RESULTS_HEADING).toBe("Результаты бросков:");
  });

  it("numbers the roll and shows the total of one die, with the bonus in the formula", () => {
    expect(
      seen(
        formatDiceRoll(
          roll({ bonus: 7, values: [18], comment: "Восприятие" }),
          1,
        ),
      ),
    ).toBe('1) 1D20+7: 25 - "Восприятие"');
  });

  it("carries the number it is given", () => {
    expect(seen(formatDiceRoll(roll({ values: [18] }), 12))).toBe(
      "12) 1D20: 18",
    );
  });

  it("shows every die, the bonus and the sum of several", () => {
    expect(
      seen(
        formatDiceRoll(
          roll({
            rolls: 2,
            edges: 10,
            bonus: 25,
            values: [9, 8],
            comment: "Сила",
          }),
          2,
        ),
      ),
    ).toBe('2) 2D10+25: 9 + 8 + 25 = 42 - "Сила"');
  });

  it("writes a negative bonus with a minus, in the formula and in the sum", () => {
    expect(
      seen(formatDiceRoll(roll({ edges: 100, bonus: -25, values: [61] }), 1)),
    ).toBe("1) 1D100-25: 36");
    expect(
      seen(
        formatDiceRoll(
          roll({
            rolls: 2,
            edges: 10,
            bonus: -5,
            values: [10, 9],
            comment: "Тело",
          }),
          3,
        ),
      ),
    ).toBe('3) 2D10-5: 10 + 9 - 5 = 14 - "Тело"');
  });

  it("drops the bonus that is not there and the comment that was not given", () => {
    expect(seen(formatDiceRoll(roll({ values: [18] }), 1))).toBe("1) 1D20: 18");
    expect(
      seen(formatDiceRoll(roll({ rolls: 3, edges: 6, values: [4, 2, 6] }), 1)),
    ).toBe("1) 3D6: 4 + 2 + 6 = 12");
  });

  it("sums an exploded die with its rerolls and stars every explosion", () => {
    // Two sixes exploded, the three closed the die: one die worth 15.
    expect(
      seen(
        formatDiceRoll(
          roll({
            edges: 6,
            explosion: 100,
            values: [[6, "exploded"], [6, "exploded"], 3],
            comment: "Урон",
          }),
          1,
        ),
      ),
    ).toBe('1) 1D6E: **15 - "Урон"');
  });

  it("stars only the die that exploded among several, and names a cap", () => {
    expect(
      seen(
        formatDiceRoll(
          roll({
            rolls: 2,
            edges: 6,
            explosion: 3,
            bonus: 1,
            values: [[6, "exploded"], 2, 4],
          }),
          1,
        ),
      ),
    ).toBe("1) 2D6E3+1: *8 + 4 + 1 = 13");
  });

  it("keeps the number with the formula, the equals sign with the total and the dash with the comment", () => {
    const line = formatDiceRoll(
      roll({ rolls: 2, edges: 10, bonus: 25, values: [9, 8], comment: "Сила" }),
      4,
    );

    expect(line.startsWith("4)\u00A02D10")).toBe(true);
    expect(line).toContain("=\u00A042");
    expect(line).toContain('-\u2060\u00A0"Сила"');
  });
});

describe("formatPendingRoll", () => {
  it("is the published line without the result", () => {
    expect(
      seen(
        formatPendingRoll(
          { dice: 20, count: 1, bonus: 5, comment: "Восприятие" },
          1,
        ),
      ),
    ).toBe('1) 1D20+5 - "Восприятие"');
    expect(seen(formatPendingRoll({ dice: 6, count: 2, bonus: 0 }, 2))).toBe(
      "2) 2D6",
    );
  });

  it("takes a missing count for one die, and writes a cap and a minus as the post does", () => {
    expect(
      seen(formatPendingRoll({ dice: 100, bonus: -3, explosion: 2 }, 3)),
    ).toBe("3) 1D100E2-3");
    expect(seen(formatPendingRoll({ dice: 8, explosion: 100 }, 1))).toBe(
      "1) 1D8E",
    );
  });
});

describe("diceFormula", () => {
  it("leaves out what is not there", () => {
    expect(diceFormula({ count: 1, edges: 20 })).toBe("1D20");
    expect(diceFormula({ count: 3, edges: 4, bonus: 0, explosion: 0 })).toBe(
      "3D4",
    );
    expect(diceFormula({ count: 1, edges: 6, explosion: null })).toBe("1D6");
  });
});
