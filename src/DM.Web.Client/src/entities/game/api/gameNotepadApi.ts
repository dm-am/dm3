import { notepadEndpoints } from "@/shared/api";

// Two addresses for opposite audiences: the player's notepad is closed to the master, and back.
export default {
  ofGame: (gameId: string) => notepadEndpoints(`games/${gameId}/notepad`),
  ofCharacter: (characterId: string) =>
    notepadEndpoints(`characters/${characterId}/notepad`),
  ofCharacterMaster: (characterId: string) =>
    notepadEndpoints(`characters/${characterId}/master-notepad`),
};
