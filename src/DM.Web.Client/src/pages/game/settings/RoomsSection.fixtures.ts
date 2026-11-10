// The rooms in the settings of a game, mounted in front of its master.
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { vi } from "vitest";
import {
  useGameDetailsStore,
  RoomAccessType,
  RoomType,
  type Character,
  type Room,
} from "@/entities/game";
import {
  createMockCharacter,
  createMockGame,
} from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import RoomsSection from "./RoomsSection.vue";

const toast = useToast();
export const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

export const dismissPlates = () =>
  [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));

export const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "t",
});

export function aRoom(
  id: string,
  title: string,
  extra: Partial<Room> = {},
): Room {
  return {
    id,
    roomNumber: Number(id.replace(/\D/g, "")) || 1,
    title,
    type: RoomType.Default,
    access: RoomAccessType.Open,
    isArchived: false,
    unreadPostsCount: 0,
    accesses: [],
    settings: {
      viewPrivateText: false,
      viewDiceResults: false,
      diceEnabled: false,
      hiddenWithoutAccess: false,
    },
    ...extra,
  } as unknown as Room;
}

const aCharacter = (id: string, name: string): Character =>
  createMockCharacter(id, name);

export const ELF = aCharacter("c-1", "Эльвира");
export const RANGER = aCharacter("c-2", "Арагорн");

/** The section over `rooms`; a list it asks for again answers with `reread`. */
export function mountSection(
  rooms: Room[],
  characters: Character[] = [],
  reread: Promise<void> = Promise.resolve(),
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
  };
  store.rooms = rooms;
  store.characters = characters;
  vi.spyOn(store, "loadRooms").mockReturnValue(reread);
  vi.spyOn(store, "loadCharacters").mockResolvedValue(undefined);

  return mount(RoomsSection, {
    global: { plugins: [pinia], stubs: { Teleport: true } },
  });
}

export const roomOf = (wrapper: VueWrapper, number: number) =>
  wrapper.get(`tbody[data-room='${number}']`);

const saveButton = (wrapper: VueWrapper) => wrapper.get(".save-all");

export const answer = (wrapper: VueWrapper) => ({
  label: saveButton(wrapper).text(),
  inert: saveButton(wrapper).attributes("disabled") !== undefined,
});

export async function save(wrapper: VueWrapper) {
  await saveButton(wrapper).trigger("click");
  await flushPromises();
}

export const writerBox = (wrapper: VueWrapper, number: number, name: string) =>
  roomOf(wrapper, number)
    .findAll(".col-writers label")
    .find((label) => label.text() === name)!
    .get("input");
