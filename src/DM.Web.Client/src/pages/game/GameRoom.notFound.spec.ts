/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { GameParticipation } from "@/entities/game";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GameRoom from "./GameRoom.vue";
import {
  mockGetCharacters,
  mockGetFirstUnreadRoomPost,
  mockGetPosts,
  mockGetRooms,
  mockMarkRoomAsRead,
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

type Landed = Awaited<ReturnType<typeof mountRoom>>["landed"];

async function mountRoom() {
  const pinia = viewerOfTheGame("master", {
    id: "g-1",
    participation: [GameParticipation.Owner],
  });

  // Whether the room drew a load failure the moment it left for "not found".
  let errorDrawnOnLeave: boolean | undefined;
  const { wrapper, router } = await mountPage(GameRoom, "/game/g-1/rooms/1", {
    routes: [
      ["game-room", "/game/:id/rooms/:num"],
      ["game", "/game/:id"],
      ["not-found", "/:pathMatch(.*)*"],
    ],
    pinia,
    // Left, the room unmounts as in the app, and reads no address but its own.
    routed: true,
    beforeMount: (r) =>
      r.beforeEach((to) => {
        if (to.name === "not-found")
          errorDrawnOnLeave = wrapper
            .findComponent({ name: "ErrorState" })
            .exists();
      }),
    // The children drawn as stubs, so the room alone is the subject.
    stubs: {
      BackLink: true,
      RoomPostFilter: true,
      ErrorState: true,
      GamePostSkeleton: true,
      PagingWithSeparators: true,
      GamePost: true,
      PostComposer: true,
      SecondaryText: true,
    },
  });
  await flushPromises();
  return {
    errorDrawnOnLeave: () => errorDrawnOnLeave,
    landed: () => router.currentRoute.value,
  };
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
  /** The parts of the address the room was sent to that notFound names. */
  const where = ({ name, params, query, hash }: ReturnType<Landed>) => ({
    name,
    params,
    query,
    hash,
  });

  it("opens the site's not found for a closed room it may not open", async () => {
    mockGetRooms.mockResolvedValue({
      data: {
        resources: [{ ...room, canView: false, accesses: [], pendencies: [] }],
        paging: null,
      },
      error: null,
    });
    const { landed } = await mountRoom();

    expect(where(landed())).toEqual(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
  });

  it("opens it for a room the list of the game does not have", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [{ ...room, roomNumber: 2 }], paging: null },
      error: null,
    });
    const { landed } = await mountRoom();

    expect(where(landed())).toEqual(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
  });

  // A list that answered with nothing is an answer too.
  it("opens it when the list of the game answered with no room at all", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    const { errorDrawnOnLeave, landed } = await mountRoom();

    expect(where(landed())).toEqual(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
    expect(errorDrawnOnLeave()).toBe(false);
  });

  it("opens the room it may open", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    const { landed } = await mountRoom();

    expect(landed().name).not.toBe("not-found");
    expect(mockGetPosts).toHaveBeenCalled();
    expect(mockGetCharacters).toHaveBeenCalled();
  });

  it("does not send the reader away when the list itself failed", async () => {
    mockGetRooms.mockResolvedValue({ data: null, error: refusal(500) });
    const { landed } = await mountRoom();

    expect(landed().name).not.toBe("not-found");
  });
});
