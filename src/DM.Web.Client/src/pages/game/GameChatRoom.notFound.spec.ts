/**
 * @vitest-environment jsdom
 */

/**
 * A chat room the viewer may not open has no page: its address opens "not
 * found", and the server is not asked for messages it would refuse. A list that
 * failed, or one of another game, says nothing of the room.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import GameChatRoom from "./GameChatRoom.vue";
import {
  mockGetChatMessages,
  mockGetRooms,
  mockReplace,
  route,
} from "./GameChatRoom.doubles";
import {
  notFoundAt,
  openRoomAs,
  keep,
  sentToNotFound,
  standInRoomOne,
  unmountRoom,
} from "./GameChatRoom.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./GameChatRoom.doubles")).roomApi(),
);
vi.mock("vue-router", async () =>
  (await import("./GameChatRoom.doubles")).vueRouter(),
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

      expect(mockReplace).toHaveBeenCalledWith(notFoundAt("1"));
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

      expect(mockReplace).toHaveBeenCalledWith(notFoundAt("1"));
      expect(mockGetChatMessages).not.toHaveBeenCalled();
      expect(wrapper.find(".login-prompt").exists()).toBe(false);
      expect(wrapper.findAllComponents(RefusalNote)).toHaveLength(0);
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("закрыта");
    },
  );

  it('opens "not found" for a number the list does not hold, with the query and the hash', async () => {
    route.params.num = "7";
    route.query = { scrollTo: "m-1" };
    route.hash = "#msg-m-1";
    try {
      const wrapper = await openRoomAs();

      expect(mockReplace).toHaveBeenCalledWith({
        ...notFoundAt("7"),
        query: { scrollTo: "m-1" },
        hash: "#msg-m-1",
      });
      expect(mockGetChatMessages).not.toHaveBeenCalled();
      expect(wrapper.text()).not.toContain("не найдена");
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
    } finally {
      route.query = {};
      route.hash = "";
    }
  });

  // Until the game is in, the list may be another game's.
  it("decides nothing about a room it cannot find until the game is in", async () => {
    route.params.num = "7";
    const wrapper = await openRoomAs({ gameIn: false });

    expect(sentToNotFound()).toBe(false);
    expect(
      wrapper.findComponent({ name: "ChatMessageSkeleton" }).exists(),
    ).toBe(true);

    useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
    await flushPromises();

    expect(mockReplace).toHaveBeenCalledWith(notFoundAt("7"));
  });

  // On the way to another game the store holds the one before for a moment.
  it.each([
    ["closes", "1", false],
    ["does not hold", "7", undefined],
  ] as const)(
    "decides nothing about the room off a list of another game that %s it",
    async (_what, num, canView) => {
      route.params.num = num;
      await openRoomAs({ gameIn: false, canView });

      useGameDetailsStore().game = { id: "g2-guid", publicId: "g-2" } as never;
      await flushPromises();

      expect(sentToNotFound()).toBe(false);

      useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
      await flushPromises();

      expect(mockReplace).toHaveBeenCalledWith(notFoundAt(num));
    },
  );

  it("does not send the reader away when the list itself failed", async () => {
    mockGetRooms.mockResolvedValue({ data: null, error: { status: 500 } });
    const pinia = createPinia();
    setActivePinia(pinia);
    useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
    useAuthStore().user = { username: "player" } as never;
    const room = keep(
      mount(GameChatRoom, {
        global: {
          plugins: [pinia],
          stubs: { RouterLink: true, BBCodeEditor: true, ChatMessage: true },
        },
      }),
    );
    await flushPromises();

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
