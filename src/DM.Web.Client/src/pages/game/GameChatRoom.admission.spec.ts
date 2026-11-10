/**
 * @vitest-environment jsdom
 */

/**
 * Who reads a chat room of a game and who writes in it. Openness is about
 * reading; writing is what RoomIntentionResolver.SendMessage takes, and a field
 * it would refuse gives way to the plaque of who writes.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { mockGetChatMessages } from "./GameChatRoom.doubles";
import {
  FIELD,
  LINE,
  NOTHING,
  characterRow,
  message,
  openRoomAs,
  page,
  readerRow,
  sentToNotFound,
  standInRoomOne,
  under,
  unmountRoom,
} from "./GameChatRoom.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./GameChatRoom.doubles")).roomApi(),
);

describe("GameChatRoom admission", () => {
  beforeEach(standInRoomOne);

  afterEach(unmountRoom);

  it.each([
    ["a reader of an open room nobody let in", {}, LINE],
    ["a reader the room lets write", { accesses: [readerRow("Full")] }, FIELD],
    [
      "a reader the room lets only read",
      { access: "Private", accesses: [readerRow("ReadOnly")] },
      LINE,
    ],
    [
      "a player whose character in play the room lets write",
      { accesses: [characterRow("Full")] },
      FIELD,
    ],
    [
      "a player whose character has left the game",
      { accesses: [characterRow("Full", { status: "Retired" })] },
      LINE,
    ],
    [
      "a reader with the grants of somebody else",
      {
        accesses: [
          characterRow("Full", { author: "other" }),
          readerRow("Full", "other"),
        ],
      },
      LINE,
    ],
    [
      "a player whose character the room lets only read",
      { access: "Private", accesses: [characterRow("ReadOnly")] },
      LINE,
    ],
    [
      "the master without a grant",
      { participation: ["Owner", "Authority"] },
      FIELD,
    ],
    ["an assistant without a grant", { participation: ["Authority"] }, FIELD],
    [
      "a viewer on the game's blacklist the room lets write",
      { accesses: [readerRow("Full")], blacklisted: true },
      FIELD,
    ],
  ])(
    "gives %s what stands under the messages",
    async (_, options, expected) => {
      const wrapper = await openRoomAs(options);

      expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
      expect(under(wrapper)).toEqual(expected);
    },
  );

  // Drawn before the role arrives, the line would blink under a master.
  it("draws no line until the game, and the viewer's role in it, are in", async () => {
    const wrapper = await openRoomAs({ gameIn: false });

    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(under(wrapper)).toEqual(NOTHING);

    useGameDetailsStore().game = {
      id: "g-guid",
      publicId: "g-1",
      participation: ["Owner"],
    } as never;
    await flushPromises();

    expect(under(wrapper)).toEqual(FIELD);
  });

  // The answer may yet be the refusal of a room the viewer may not read.
  it("draws no line while the messages are on their way", async () => {
    let answer: (value: unknown) => void = () => {};
    mockGetChatMessages.mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }),
    );
    const wrapper = await openRoomAs();

    expect(under(wrapper)).toEqual(NOTHING);

    answer(page(message("m-1")));
    await flushPromises();

    expect(under(wrapper)).toEqual(LINE);
  });

  // Signing in lets nobody write by itself, so the guest is not invited in.
  it("reads an open room to a guest and tells him who writes in it", async () => {
    const wrapper = await openRoomAs({ signedIn: false });

    expect(mockGetChatMessages).toHaveBeenCalledTimes(1);
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(under(wrapper)).toEqual(LINE);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(sentToNotFound()).toBe(false);
  });

  // The list of a session that just ran out may still open a closed room.
  it("invites the guest of a closed room his list opens to sign in to read, and asks the server nothing", async () => {
    const wrapper = await openRoomAs({ signedIn: false, access: "Private" });

    expect(mockGetChatMessages).not.toHaveBeenCalled();
    expect(wrapper.findAll(".login-prompt")).toHaveLength(1);
    expect(wrapper.get(".login-prompt").text()).toContain("читать чат игры");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
    expect(wrapper.find(".chat-input-wrapper").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("участникам игры");
    expect(sentToNotFound()).toBe(false);
  });

  it("invites the guest in when the server reads the room to the signed-in only", async () => {
    mockGetChatMessages.mockResolvedValue({
      data: null,
      error: { status: 401 },
    });
    const wrapper = await openRoomAs({ signedIn: false });

    expect(wrapper.findAll(".login-prompt")).toHaveLength(1);
    expect(wrapper.get(".login-prompt").text()).toContain("читать чат игры");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
    expect(sentToNotFound()).toBe(false);
  });

  // Signing in does not reload the page.
  it("reads the room the moment the guest signs in", async () => {
    mockGetChatMessages.mockImplementation(async () =>
      useAuthStore().user
        ? page(message("m-1"))
        : { data: null, error: { status: 401 } },
    );
    const wrapper = await openRoomAs({ signedIn: false, access: "Private" });

    useAuthStore().user = { username: "player" } as never;
    await flushPromises();

    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
  });

  // The interceptor takes the viewer off the store on a 401; the room is then
  // read as a guest's.
  it("turns a closed room into the guest's invitation when the session runs out", async () => {
    mockGetChatMessages.mockImplementation(async () => {
      useAuthStore().user = null;
      return { data: null, error: { status: 401 } };
    });
    const wrapper = await openRoomAs({ access: "Private" });

    expect(mockGetChatMessages).toHaveBeenCalledTimes(1);
    expect(wrapper.findAll(".login-prompt")).toHaveLength(1);
    expect(wrapper.get(".login-prompt").text()).toContain("читать чат игры");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("участникам игры");
    expect(sentToNotFound()).toBe(false);
  });

  it("keeps an open room on screen for the guest when the session runs out", async () => {
    mockGetChatMessages.mockImplementation(async () => {
      if (!useAuthStore().user) return page(message("m-1"));
      useAuthStore().user = null;
      return { data: null, error: { status: 401 } };
    });
    const wrapper = await openRoomAs();

    expect(mockGetChatMessages).toHaveBeenCalledTimes(2);
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(sentToNotFound()).toBe(false);
    expect(under(wrapper)).toEqual(LINE);
  });
});
