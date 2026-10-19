import { computed, ref, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useNotFoundWhen } from "@/shared/lib/composables/useNotFoundWhen";

/**
 * A room this reader may not open has no page: its address opens the site's
 * "not found". `refusedByServer` is a refusal of the room's own reads.
 */
export function useRoomNotFound(options: {
  gameId: Ref<string>;
  roomNum: Ref<number>;
  refusedByServer?: Ref<boolean>;
}) {
  const { gameId, roomNum, refusedByServer } = options;
  const gameStore = useGameDetailsStore();
  const { game, rooms, roomsLoading, roomsError } = storeToRefs(gameStore);

  // Tells an empty list that answered from one not asked for yet.
  const listedGame = ref<string | null>(null);

  async function ensureRooms(): Promise<void> {
    const id = gameId.value;
    if (!rooms.value.length) await gameStore.loadRooms(id);
    if (id === gameId.value && !roomsLoading.value && !roomsError.value)
      listedGame.value = id;
  }

  // Of this game's list only: the previous game's stays a moment in the store.
  const roomRefused = computed(() => {
    if (refusedByServer?.value) return true;
    const current = game.value;
    if (!current || roomsError.value) return false;
    if (!rooms.value.length && listedGame.value !== gameId.value) return false;
    if (gameId.value !== current.publicId && gameId.value !== current.id)
      return false;
    const listed = rooms.value.find((r) => r.roomNumber === roomNum.value);
    return !listed || listed.canView === false;
  });

  useNotFoundWhen(roomRefused);

  return { ensureRooms, roomRefused };
}
