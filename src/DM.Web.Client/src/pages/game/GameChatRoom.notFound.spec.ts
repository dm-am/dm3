/**
 * @vitest-environment jsdom
 */

/**
 * A chat room the viewer may not open has no page: its address opens "not
 * found", and the server is not asked for messages it would refuse. A list that
 * failed, or one of another game, says nothing of the room.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import GameChatRoom from "./GameChatRoom.vue";
import { mockGetChatMessages, mockGetRooms } from "./GameChatRoom.doubles";
import {
  FRAME_STUBS,
  mountRoom,
  notFoundAsked,
  notFoundAt,
  openRoomAs,
  roomAt,
  sentToNotFound,
  standInRoomOne,
  unmountRoom,
} from "./GameChatRoom.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./GameChatRoom.doubles")).roomApi(),
);

describe("GameChatRoom not found", () => {
  beforeEach(standInRoomOne);

  afterEach(unmountRoom);

  // Closed after the list was read: the server answers the messages 404.
  it.each([404, 403])(
    'opens "not found" at the address of the room when its messages answer %s',
    async (status) => {
      mockGetChatMessages.mockResolvedValue({ data: null, error: { status } });
      const wrapper = await openRoomAs({ access: "Private" });

      expect(notFoundAsked()).toEqual(notFoundAt("1"));
      expect(wrapper.findAllComponents(RefusalNote)).toHaveLength(0);
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
      expect(wrapper.find(".chat-input-wrapper").exists()).toBe(false);
      expect(wrapper.find(".login-prompt").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("закрыта");
      expect(wrapper.text()).not.toContain("Не удалось загрузить");
      expect(wrapper.text()).not.toContain("Повторить");
    },
  );

  it.each([
    ["a signed-in viewer", true],
    ["a guest", false],
  ] as const)(
    'opens "not found" for %s of a room the list closes to him',
    async (_who, signedIn) => {
      const wrapper = await openRoomAs({
        access: "Private",
        canView: false,
        signedIn,
      });

      expect(notFoundAsked()).toEqual(notFoundAt("1"));
      expect(mockGetChatMessages).not.toHaveBeenCalled();
      expect(wrapper.find(".login-prompt").exists()).toBe(false);
      expect(wrapper.findAllComponents(RefusalNote)).toHaveLength(0);
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("закрыта");
    },
  );

  it('opens "not found" for a number the list does not hold, with the query and the hash', async () => {
    const wrapper = await openRoomAs({
      at: roomAt("7", "?scrollTo=m-1#msg-m-1"),
    });

    expect(notFoundAsked()).toEqual(notFoundAt("7", "?scrollTo=m-1#msg-m-1"));
    expect(mockGetChatMessages).not.toHaveBeenCalled();
    expect(wrapper.text()).not.toContain("не найдена");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
  });

  // Until the game is in, the list may be another game's.
  it("decides nothing about a room it cannot find until the game is in", async () => {
    const wrapper = await openRoomAs({ gameIn: false, at: roomAt("7") });

    expect(sentToNotFound()).toBe(false);
    expect(
      wrapper.findComponent({ name: "ChatMessageSkeleton" }).exists(),
    ).toBe(true);

    useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
    await flushPromises();

    expect(notFoundAsked()).toEqual(notFoundAt("7"));
  });

  // On the way to another game the store holds the one before for a moment.
  it.each([
    ["closes", "1", false],
    ["does not hold", "7", undefined],
  ] as const)(
    "decides nothing about the room off a list of another game that %s it",
    async (_what, num, canView) => {
      await openRoomAs({ gameIn: false, canView, at: roomAt(num) });

      useGameDetailsStore().game = { id: "g2-guid", publicId: "g-2" } as never;
      await flushPromises();

      expect(sentToNotFound()).toBe(false);

      useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
      await flushPromises();

      expect(notFoundAsked()).toEqual(notFoundAt(num));
    },
  );

  it("does not send the reader away when the list itself failed", async () => {
    mockGetRooms.mockResolvedValue({ data: null, error: { status: 500 } });
    const pinia = createPinia();
    useGameDetailsStore(pinia).game = {
      id: "g-guid",
      publicId: "g-1",
    } as never;
    useAuthStore(pinia).user = { username: "player" } as never;
    const room = await mountRoom(GameChatRoom, { pinia, stubs: FRAME_STUBS });

    expect(sentToNotFound()).toBe(false);
    expect(room.get(".chat-error").text()).toContain(
      "Не удалось загрузить комнаты",
    );
    expect(mockGetChatMessages).not.toHaveBeenCalled();

    mockGetRooms.mockResolvedValue({
      data: {
        resources: [
          { id: "r-1", roomNumber: 1, title: "Таверна", access: "Open" },
        ],
        paging: null,
      },
      error: null,
    });
    await room.get(".chat-error .error-retry").trigger("click");
    await flushPromises();

    expect(sentToNotFound()).toBe(false);
    expect(room.findAll(".chat-message-row")).toHaveLength(1);
  });
});
