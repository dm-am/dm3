/**
 * @vitest-environment jsdom
 */

/**
 * Turn tracking is the only mutation the room page owns, and both ends of it
 * used to swallow the refusal: the button re-enabled, the list stayed as it
 * was, and nothing said why — so the master pressed it again.
 *
 * These pin the outcome every neighbouring master screen already gives
 * (RoomsSection, RolesSection, BlacklistSection): one toast through
 * notifyFailure, and a refresh of the room only when the server agreed.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { GameParticipation, useGameDetailsStore } from "@/entities/game";
import GameRoom from "./GameRoom.vue";

const {
  mockGetRooms,
  mockGetPosts,
  mockGetCharacters,
  mockMarkRoomAsRead,
  mockCreatePendency,
  mockDeletePendency,
} = vi.hoisted(() => ({
  mockGetRooms: vi.fn(),
  mockGetPosts: vi.fn(),
  mockGetCharacters: vi.fn(),
  mockMarkRoomAsRead: vi.fn(),
  mockCreatePendency: vi.fn(),
  mockDeletePendency: vi.fn(),
}));

vi.mock("@/entities/game/api/gameApi", () => ({
  default: {
    getRooms: mockGetRooms,
    getPosts: mockGetPosts,
    getCharacters: mockGetCharacters,
    markRoomAsRead: mockMarkRoomAsRead,
    createPendency: mockCreatePendency,
    deletePendency: mockDeletePendency,
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "g-1", num: "1" }, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/** A status the response interceptor stays quiet about, so the page must speak. */
const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "trace",
});

const room = {
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
};

const character = {
  id: "c-1",
  name: "Гоблин",
  status: "Active",
  isNpc: false,
  author: { username: "player" },
};

const messages = () => useToast().toasts.value.map((t) => t.message);

async function mountRoom() {
  const pinia = createPinia();
  setActivePinia(pinia);
  // The turn controls belong to the master; participation is what decides it.
  useGameDetailsStore().game = {
    id: "g-1",
    participation: [GameParticipation.Owner],
  } as never;

  const wrapper = mount(GameRoom, {
    shallow: true,
    global: { plugins: [pinia], stubs: { RouterLink: true } },
  });
  await flushPromises();
  return wrapper;
}

describe("GameRoom turn tracking", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    vi.clearAllMocks();
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [character], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockCreatePendency.mockResolvedValue({ data: null, error: null });
    mockDeletePendency.mockResolvedValue({ data: null, error: null });
  });

  it("says why the character could not be added to the queue", async () => {
    mockCreatePendency.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountRoom();

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(messages()).toEqual(["Не удалось добавить ожидание хода"]);
  });

  it("says why the awaited turn could not be dropped", async () => {
    mockDeletePendency.mockResolvedValue({ data: null, error: refusal(404) });
    const wrapper = await mountRoom();

    await wrapper.find(".pending-dismiss").trigger("click");
    await flushPromises();

    expect(messages()).toEqual(["Не удалось снять ожидание хода"]);
  });

  it("refreshes the room and stays quiet when the server agreed", async () => {
    const wrapper = await mountRoom();
    const before = mockGetRooms.mock.calls.length;

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(mockGetRooms.mock.calls.length).toBe(before + 1);
    expect(messages()).toEqual([]);
  });

  it("does not refresh the room over a refused pendency", async () => {
    mockCreatePendency.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountRoom();
    const before = mockGetRooms.mock.calls.length;

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(mockGetRooms.mock.calls.length).toBe(before);
  });
});
