import type { DiceResult, DiceRoll, DiceRollInput } from "@/entities/game";

/**
 * The rolls of a post in the words the old site has used since 4 October
 * 2026: one line "Результаты бросков:", then a line per roll with its number,
 * `1) 1D20+7: 25 - "Восприятие"`. No "Результат броска" in front of every
 * line and no period after it: the heading says what the lines are, and a
 * numbered line of figures is an item of a list, not a sentence.
 *
 * One die shows its total. Several dice show every die, the bonus and the
 * total, "2D10+25: 9 + 8 + 25 = 42", so the reader can check the sum. A die
 * that exploded is summed with the rerolls it threw and carries a star for
 * every explosion; the formula says so with "E", or "E3" for a cap of three.
 * A negative bonus is written with a minus, where the old site printed "+-10".
 *
 * The composer lists the rolls it will send in the same words, without the
 * result the server has not thrown yet: `1) 1D20+5 - "Восприятие"`. The
 * formula and the comment are written here once for both.
 */

/** The line over the rolls of a post. */
export const DICE_RESULTS_HEADING = "Результаты бросков:";

// The cap the server clamps a requested explosion count to
// (DiceRoller.MaxExplosionsPerDie). A roll stored with it is written as the
// bare "E" of a die that explodes for as long as it keeps rolling its maximum.
export const UNLIMITED_EXPLOSIONS = 100;

// Where a roll wraps, "=" stays with the total and "-" with the comment. A
// no-break space holds the first pair, and the number with the formula after
// it. A hyphen allows a break after itself even before a no-break space, so
// the comment needs a word joiner too.
const NBSP = "\u00A0";
const WORD_JOINER = "\u2060";

/** What a roll is before it falls: how many dice of how many edges, and so on. */
export interface DiceFormula {
  count: number;
  edges: number;
  bonus?: number;
  explosion?: number | null;
}

/** "2D6E3+1": the count, the edges, the cap of explosions, the bonus. */
export function diceFormula({
  count,
  edges,
  bonus,
  explosion,
}: DiceFormula): string {
  const exploding = explosion
    ? `E${explosion === UNLIMITED_EXPLOSIONS ? "" : explosion}`
    : "";
  let modifier = "";
  if (bonus) modifier = bonus > 0 ? `+${bonus}` : `-${-bonus}`;
  return `${count}D${edges}${exploding}${modifier}`;
}

/** ` - "Восприятие"`, held to the line before it; nothing without a comment. */
function commentPart(comment: string | undefined): string {
  return comment ? ` -${WORD_JOINER}${NBSP}"${comment}"` : "";
}

/** "1) ", held to what follows it. */
function numbered(number: number, line: string): string {
  return `${number})${NBSP}${line}`;
}

/** One die as it counts: its value with every reroll its explosions threw. */
interface CountedDie {
  sum: number;
  explosions: number;
}

/**
 * The dice of a roll, rerolls folded into the die that threw them. The server
 * lists every throw in order and marks the ones that exploded, so a die is a
 * run of exploded throws closed by the first one that did not.
 */
function countedDice(results: DiceResult[]): CountedDie[] {
  const dice: CountedDie[] = [];
  let current: CountedDie | null = null;
  for (const throwResult of results) {
    current ??= { sum: 0, explosions: 0 };
    current.sum += throwResult.value;
    if (throwResult.exploded) {
      current.explosions++;
    } else {
      dice.push(current);
      current = null;
    }
  }
  if (current) dice.push(current);
  return dice;
}

/** A published roll, numbered: `1) 2D6: 3 + 4 = 7 - "Урон"`. */
export function formatDiceRoll(roll: DiceRoll, number: number): string {
  const bonus = roll.bonus || 0;
  const dice = countedDice(roll.results ?? []);
  const total = dice.reduce((sum, die) => sum + die.sum, 0) + bonus;
  const shown = (die: CountedDie) => `${"*".repeat(die.explosions)}${die.sum}`;

  let result: string;
  if (roll.rolls > 1) {
    result = dice.map(shown).join(" + ");
    if (bonus) result += ` ${bonus > 0 ? "+" : "-"} ${Math.abs(bonus)}`;
    result += ` =${NBSP}${total}`;
  } else {
    result = `${"*".repeat(dice[0]?.explosions ?? 0)}${total}`;
  }

  const formula = diceFormula({
    count: roll.rolls,
    edges: roll.edges,
    bonus,
    explosion: roll.explosion,
  });
  return numbered(number, `${formula}: ${result}${commentPart(roll.comment)}`);
}

/** A roll the composer will send with the post: `1) 1D20+5 - "Восприятие"`. */
export function formatPendingRoll(roll: DiceRollInput, number: number): string {
  const formula = diceFormula({
    count: roll.count ?? 1,
    edges: roll.dice,
    bonus: roll.bonus,
    explosion: roll.explosion,
  });
  return numbered(number, `${formula}${commentPart(roll.comment)}`);
}
