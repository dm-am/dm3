/**
 * @vitest-environment jsdom
 */

/**
 * The live tail API_DESIGN.md ("Realtime") states for every live list, and the
 * moments it must not run: a tab off screen, a page that has left, a refused
 * room, a read in flight, an answer for a room the reader has left.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import {
  mockGetChatMessages,
  mockMarkChatRoomRead,
  mockSendChatMessage,
} from "./GameChatRoom.doubles";
import {
  WINDOW_HEIGHT,
  heldRead,
  landing,
  message,
  moveTo,
  notFoundAsked,
  notFoundAt,
  openRoom,
  page,
  parkAboveTheTail,
  parkAtTheTail,
  raiseTheHiddenFlag,
  reads,
  returnToTheTail,
  scrollTopOf,
  setHidden,
  shownIds,
  tick,
  timesSentToNotFound,
  unmountRoom,
} from "./GameChatRoom.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./GameChatRoom.doubles")).roomApi(),
);

describe("GameChatRoom live tail", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    setHidden(false);
    mockGetChatMessages.mockResolvedValue(page());
    mockSendChatMessage.mockResolvedValue({ data: null, error: null });
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
  });

  afterEach(() => {
    unmountRoom();
    vi.useRealTimers();
    setHidden(false);
  });

  it("reads the newest page on every tick and shows a message once", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();
    expect(mockGetChatMessages).toHaveBeenCalledTimes(1);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(mockGetChatMessages).toHaveBeenCalledTimes(2);
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);

    // De-duplicated by id.
    await tick();
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("shows the window it already has when the tail shares nothing with it", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();

    // Appending would leave a hole between m-1 and what arrived.
    mockGetChatMessages.mockResolvedValue(
      page(message("m-9"), message("m-10")),
    );
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-9", "msg-m-10"]);
    // The page in hand is the window, not asked for a second time.
    expect(mockGetChatMessages).toHaveBeenCalledTimes(2);
  });

  it("leaves the window in place under a reader who has scrolled up", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();
    parkAboveTheTail(wrapper);

    mockGetChatMessages.mockResolvedValue(
      page(message("m-9"), message("m-10")),
    );
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);
    expect(reads()).toBe(2);
  });

  it("replaces it on the tick that finds him back at the tail", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();
    parkAboveTheTail(wrapper);

    mockGetChatMessages.mockResolvedValue(
      page(message("m-9"), message("m-10")),
    );
    await tick();
    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);

    returnToTheTail(wrapper);
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-9", "msg-m-10"]);
  });

  it("reads nothing while the tab is off screen", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await openRoom();
    const readsBefore = reads();

    setHidden(true);
    // No timer at all: the guarded read alone would hide one left running.
    expect(vi.getTimerCount()).toBe(0);
    await tick();
    await tick();

    expect(reads()).toBe(readsBefore);

    setHidden(false);
    await flushPromises();

    // The return reads once on its own, and one timer comes back.
    expect(reads()).toBe(readsBefore + 1);
    expect(vi.getTimerCount()).toBe(1);
  });

  // Between the hidden flag and its event, which the browser sends apart.
  it("reads nothing on a tick that fires after the tab is already hidden", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await openRoom();
    const readsBefore = reads();

    raiseTheHiddenFlag(true);
    await tick();
    await tick();

    expect(reads()).toBe(readsBefore);
  });

  it("catches up the moment the tab comes back", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();
    setHidden(true);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    setHidden(false);
    await flushPromises();

    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("keeps a single timer when the tab is reported visible twice", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await openRoom();

    // One return to the screen can arrive as more than one event.
    setHidden(false);
    setHidden(false);
    await flushPromises();
    const readsBefore = reads();

    await tick();

    expect(reads()).toBe(readsBefore + 1);
  });

  it("reads once when the tab comes back while a tick is in flight", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();
    const readsBefore = reads();

    const held = heldRead();
    mockGetChatMessages.mockReturnValueOnce(held.read);
    await tick();
    expect(reads()).toBe(readsBefore + 1);

    // Back while the tick still waits for its answer.
    setHidden(true);
    setHidden(false);
    await flushPromises();
    expect(reads()).toBe(readsBefore + 1);

    held.answer(page(message("m-1"), message("m-2")));
    await flushPromises();
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("drops the tail of a room the reader has already left", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();

    const held = heldRead();
    mockGetChatMessages.mockReturnValueOnce(held.read);
    await tick();
    expect(mockGetChatMessages.mock.calls.at(-1)?.[0]).toBe("r-1");

    // Room 2, empty, opened while that read is in flight.
    await moveTo("2");
    await flushPromises();
    expect(shownIds(wrapper)).toEqual([]);

    held.answer(page(message("m-1"), message("m-2")));
    await flushPromises();

    expect(shownIds(wrapper)).toEqual([]);
  });

  it('opens "not found" when the tail comes back refused, and reads no more', async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();

    // Out of reach while open: 404 (RoomAvailable), not 403.
    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 404 },
    });
    await tick();

    expect(notFoundAsked()).toEqual(notFoundAt("1"));
    expect(wrapper.find(".chat-messages").exists()).toBe(false);

    const readsBefore = reads();
    await tick();
    await tick();
    expect(reads()).toBe(readsBefore);
  });

  // A refusal belongs to the room it was given for.
  it("opens the next room of the game after one the server refused", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();

    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 404 },
    });
    await tick();
    expect(timesSentToNotFound()).toBe(1);

    mockGetChatMessages.mockResolvedValue(page(message("m-7")));
    await moveTo("2");
    await flushPromises();

    expect(timesSentToNotFound()).toBe(1);
    expect(shownIds(wrapper)).toEqual(["msg-m-7"]);
  });

  it("drops the answer of a load a newer one has replaced", async () => {
    const held = heldRead();
    mockGetChatMessages.mockReturnValueOnce(held.read);
    const wrapper = await openRoom();
    expect(mockGetChatMessages.mock.calls.at(-1)?.[0]).toBe("r-1");

    mockGetChatMessages.mockResolvedValueOnce(page(message("m-7")));
    await moveTo("2");
    await flushPromises();
    expect(mockGetChatMessages.mock.calls.at(-1)?.[0]).toBe("r-2");
    expect(shownIds(wrapper)).toEqual(["msg-m-7"]);

    held.answer({ data: null, error: { status: 404 } } as never);
    await flushPromises();

    expect(timesSentToNotFound()).toBe(0);
    expect(landing()).toEqual({
      name: "game-chat-room",
      fullPath: "/game/g-1/chat-rooms/2",
    });
    expect(shownIds(wrapper)).toEqual(["msg-m-7"]);
  });

  // A guest's 401 would read as a session that ran out.
  it("reads nothing for a guest of a closed room, neither on the way in nor on a tick", async () => {
    await openRoom({ signedIn: false, access: "Private" });
    await tick();
    setHidden(true);
    setHidden(false);
    await flushPromises();
    await tick();

    expect(reads()).toBe(0);
  });

  it("reads an open room for a guest, on the way in and on every tick", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom({ signedIn: false });
    expect(reads()).toBe(1);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(reads()).toBe(2);
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
    expect(mockMarkChatRoomRead).not.toHaveBeenCalled();
  });

  it("changes nothing when a tail read fails, and asks again on the next tick", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom();

    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 500 },
    });
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);
    expect(wrapper.find("div.chat-empty-inline").exists()).toBe(false);

    // Nor a failure with no status at all.
    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { title: "Не удалось выполнить запрос" },
    });
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);
    expect(wrapper.find("div.chat-empty-inline").exists()).toBe(false);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("marks the room read when new messages land in front of the reader", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom({ signedIn: true });
    parkAtTheTail(wrapper);
    const marksBefore = mockMarkChatRoomRead.mock.calls.length;

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
    expect(mockMarkChatRoomRead.mock.calls.length).toBe(marksBefore + 1);
    expect(scrollTopOf(wrapper)).toBe(WINDOW_HEIGHT);
  });

  it("leaves the room unread for a reader who is away from the tail", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await openRoom({ signedIn: true });
    parkAboveTheTail(wrapper);
    const marksBefore = mockMarkChatRoomRead.mock.calls.length;

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
    expect(mockMarkChatRoomRead.mock.calls.length).toBe(marksBefore);
    expect(scrollTopOf(wrapper)).toBe(0);
  });

  it("stops reading once the page is gone", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await openRoom();
    const readsBefore = reads();

    unmountRoom();
    await tick();
    await tick();

    expect(reads()).toBe(readsBefore);

    // The listener goes with the page.
    setHidden(true);
    setHidden(false);
    await flushPromises();
    await tick();

    expect(reads()).toBe(readsBefore);
  });
});
