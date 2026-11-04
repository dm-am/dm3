import type { DiceResult, DiceRoll, DiceRollInput } from "./diceTypes";

// Several dice show every die and the bonus, so the reader can check the sum.

/** The line over the rolls of a post. */
export const DICE_RESULTS_HEADING = "Результаты бросков:";

// CreatePostValidator.MaxExplosionCount, compared by ClientFieldLimitsShould: a roll
// stored with it is written as a bare "E".
export const UNLIMITED_EXPLOSIONS = 100;

// "=" stays with the total and "-" with the comment; a hyphen breaks even before a
// no-break space, so the comment needs a word joiner too.
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

// A die is a run of exploded throws closed by the first one that did not explode.
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
  const bonus = roll.bonus;
  const dice = countedDice(roll.results);
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
