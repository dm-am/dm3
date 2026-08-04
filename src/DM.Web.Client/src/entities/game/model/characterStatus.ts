import type { Character } from "./types";

/**
 * What happened to a character, as one caption under its name.
 *
 * An active character gets none: being in the game is the default state of
 * everyone in the roster, and a caption for it would repeat under every name.
 * `Retired` is not a state on its own either — three flags refine it, and each
 * of them is a different sentence about the same status (the flags are not
 * mutually exclusive in the schema, so the order below is the answer for a
 * character that carries more than one).
 *
 * The wording is fixed: the spellings that lost are held out by the gate in
 * shared/lib/utils/statusCopy.spec.ts.
 */
export function characterStatusLabel(character: Character): string | null {
  switch (character.status) {
    case "UnderReview":
      return "На рассмотрении";
    case "Retired":
      if (character.isDead) return "Персонаж мертв";
      if (character.isPlayerLeft) return "Покинул игру";
      if (character.isPlayerExiled) return "Выведен из игры";
      return "Вне игры";
    default:
      return null;
  }
}
