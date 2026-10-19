/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { GameParticipation } from "@/entities/game";
import GameRoom from "./GameRoom.vue";
import {
  mockGetCharacters,
  mockGetFirstUnreadRoomPost,
  mockGetPosts,
  mockGetRooms,
  mockMarkRoomAsRead,
  mockReplace,
} from "./room/room.doubles";
import { refusal, room, viewerOfTheGame } from "./room/room.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./room/room.doubles")).roomApi(),
);
vi.mock("@/entities/game/api/postApi", async () =>
  (await import("./room/room.doubles")).postApi(),
);
vi.mock("@/entities/game/api/characterApi", async () =>
  (await import("./room/room.doubles")).characterApi(),
);
vi.mock("vue-router", async () =>
  (await import("./room/room.doubles")).vueRouter(),
);

async function mountRoom() {
  const pinia = viewerOfTheGame("master", {
    id: "g-1",
    participation: [GameParticipation.Owner],
  });

  const wrapper = mount(GameRoom, {
    shallow: true,
    global: {
      plugins: [pinia],
      stubs: { RouterLink: true, ActionResult: false },
    },
  });
  await flushPromises();
  return wrapper;
}

// A room the reader may not open has no page: its address opens "not found",
// and its posts are not asked for.
describe("GameRoom closed to the reader", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  const notFound = {
    name: "not-found",
    params: { pathMatch: ["game", "g-1", "rooms", "1"] },
    query: {},
    hash: "",
  };

  it("opens the site's not found for a closed room it may not open", async () => {
    mockGetRooms.mockResolvedValue({
      data: {
        resources: [{ ...room, canView: false, accesses: [], pendencies: [] }],
        paging: null,
      },
      error: null,
    });
    await mountRoom();

    expect(mockReplace).toHaveBeenCalledWith(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
  });

  it("opens it for a room the list of the game does not have", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [{ ...room, roomNumber: 2 }], paging: null },
      error: null,
    });
    await mountRoom();

    expect(mockReplace).toHaveBeenCalledWith(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
  });

  // A list that answered with nothing is an answer too.
  it("opens it when the list of the game answered with no room at all", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    const wrapper = await mountRoom();

    expect(mockReplace).toHaveBeenCalledWith(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
    expect(wrapper.findComponent({ name: "ErrorState" }).exists()).toBe(false);
  });

  it("opens the room it may open", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    await mountRoom();

    expect(mockReplace).not.toHaveBeenCalledWith(
      expect.objectContaining({ name: "not-found" }),
    );
    expect(mockGetPosts).toHaveBeenCalled();
    expect(mockGetCharacters).toHaveBeenCalled();
  });

  it("does not send the reader away when the list itself failed", async () => {
    mockGetRooms.mockResolvedValue({ data: null, error: refusal(500) });
    await mountRoom();

    expect(mockReplace).not.toHaveBeenCalledWith(
      expect.objectContaining({ name: "not-found" }),
    );
  });
});
