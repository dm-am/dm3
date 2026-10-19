// A room of posts with a turn awaited and a player's character let in.
import { createPinia, setActivePinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";

export const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "trace",
});

export const room = {
  id: "r-1",
  roomNumber: 1,
  title: "Таверна",
  unreadPostsCount: 0,
  settings: { diceEnabled: false },
  pendencies: [
    {
      id: "p-1",
      roomId: "r-1",
      characterId: "c-1",
      characterName: "Гоблин",
      createdUtc: "2026-07-01T10:00:00Z",
      waitingFor: { username: "player" },
    },
  ],
  accesses: [{ id: "a-1", policy: "Full", character: { id: "c-1" } }],
};

export const character = {
  id: "c-1",
  name: "Гоблин",
  status: "Active",
  isNpc: false,
  author: { username: "player" },
};

export const messages = () => useToast().toasts.value.map((t) => t.message);

export function viewerOfTheGame(
  username: string,
  game: object,
  characters: unknown[] = [],
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().updateUser({ username } as never);
  const store = useGameDetailsStore();
  store.game = game as never;
  store.characters = characters as never;
  return pinia;
}
