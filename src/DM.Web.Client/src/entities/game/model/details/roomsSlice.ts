import { ref, computed, type Ref } from "vue";
import type { Game } from "../gameTypes";
import type { Room, CreateRoomInput } from "../roomTypes";
import type { GeneralError } from "@/shared/api/models/common";
import roomApi from "../../api/roomApi";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { requestNotSent } from "@/shared/lib/errors";

function roomsState() {
  return {
    rooms: ref<Room[]>([]),
    roomsLoading: ref(false),
    roomsError: ref<string | null>(null),
    roomsGuard: createRequestGuard(),
  };
}

type Rooms = ReturnType<typeof roomsState>;

export function createRoomsSlice() {
  const state = roomsState();
  const { rooms, roomsLoading, roomsError } = state;

  const activeRooms = computed(() => rooms.value.filter((r) => !r.isArchived));
  const archivedRooms = computed(() => rooms.value.filter((r) => r.isArchived));

  // The shell and the room page load the list on one mount: the second joins the first.
  let roomsInFlight: { gameId: string; promise: Promise<void> } | null = null;

  async function loadRooms(gameId: string): Promise<void> {
    if (roomsInFlight?.gameId === gameId) return roomsInFlight.promise;

    const promise = fetchRooms(state, gameId);
    roomsInFlight = { gameId, promise };
    try {
      await promise;
    } finally {
      if (roomsInFlight?.promise === promise) roomsInFlight = null;
    }
  }

  // The request in flight belongs to the game being left, so nobody may join it.
  function resetRooms(): void {
    roomsInFlight = null;
    rooms.value = [];
    roomsLoading.value = false;
    roomsError.value = null;
  }

  return {
    ...state,
    activeRooms,
    archivedRooms,
    loadRooms,
    resetRooms,
  };
}

export function createRoomChanges(
  game: Ref<Game | null>,
  loadRooms: (gameId: string) => Promise<void>,
) {
  async function createRoom(
    room: CreateRoomInput,
  ): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const id = game.value.id;
    const { error } = await roomApi.createRoom(id, room);
    if (error) return error;
    await loadRooms(id);
    return null;
  }

  async function archiveRoom(roomId: string): Promise<GeneralError | null> {
    if (!game.value) return requestNotSent;
    const { error } = await roomApi.archiveRoom(roomId);
    if (error) return error;
    await loadRooms(game.value.id);
    return null;
  }

  return { createRoom, archiveRoom };
}

async function fetchRooms(
  { rooms, roomsLoading, roomsError, roomsGuard }: Rooms,
  gameId: string,
): Promise<void> {
  const requestId = roomsGuard.next();
  roomsLoading.value = true;
  roomsError.value = null;

  const { data, error } = await roomApi.getRooms(gameId);

  if (!roomsGuard.isCurrent(requestId)) return;

  if (error) {
    roomsError.value = "Не удалось загрузить комнаты";
    rooms.value = [];
  } else if (data) {
    rooms.value = data.resources;
  }

  roomsLoading.value = false;
}
