import type { Character } from "./characterTypes";

// An active character gets no caption; the flags of Retired are checked in this order.
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
