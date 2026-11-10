/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import type { Message } from "@/shared/api/models/common";
import {
  mockGetChatMessages,
  mockMarkChatRoomRead,
} from "./GameChatRoom.doubles";
import {
  heldRead,
  message,
  openRoom,
  page,
  setHidden,
  shownIds,
  unmountRoom,
} from "./GameChatRoom.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./GameChatRoom.doubles")).roomApi(),
);

// jsdom has no IntersectionObserver: the test fires a stand-in by hand.
describe("GameChatRoom older messages", () => {
  const observers: {
    fire: (entries: { isIntersecting: boolean }[]) => void;
    watched: Element | null;
    live: boolean;
  }[] = [];

  const sentinelInView = () => {
    const current = observers.filter((o) => o.live && o.watched).at(-1);
    current?.fire([{ isIntersecting: true }]);
  };

  const olderReads = () =>
    mockGetChatMessages.mock.calls.filter(([, cursor]) => cursor !== undefined);

  const withCursor = (hasPrev: boolean, ...messages: Message[]) => {
    const answer = page(...messages);
    answer.data.paging.hasPrev = hasPrev;
    answer.data.paging.prevCursor = hasPrev ? `before-${messages[0].id}` : "";
    return answer;
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    setHidden(false);
    observers.length = 0;
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        record: (typeof observers)[number];
        constructor(
          callback: (entries: { isIntersecting: boolean }[]) => void,
        ) {
          this.record = { fire: callback, watched: null, live: true };
          observers.push(this.record);
        }
        observe(element: Element) {
          this.record.watched = element;
        }
        disconnect() {
          this.record.live = false;
        }
      },
    );
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
    mockGetChatMessages.mockResolvedValue(
      withCursor(true, message("m-5"), message("m-6")),
    );
  });

  afterEach(() => {
    unmountRoom();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("reads the page before the oldest message when the top comes into view", async () => {
    const wrapper = await openRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce(
      withCursor(true, message("m-3"), message("m-4")),
    );

    sentinelInView();
    await flushPromises();

    expect(olderReads()).toEqual([["r-1", "before-m-5", 50]]);
    expect(shownIds(wrapper)).toEqual([
      "msg-m-3",
      "msg-m-4",
      "msg-m-5",
      "msg-m-6",
    ]);
  });

  it("keeps the reader on the message he was reading as the page lands above it", async () => {
    const wrapper = await openRoom();
    await flushPromises();
    const el = wrapper.find(".chat-messages").element;
    let height = 1000;
    Object.defineProperty(el, "scrollHeight", {
      get: () => height,
      configurable: true,
    });
    el.scrollTop = 0;
    const { read, answer } = heldRead();
    mockGetChatMessages.mockReturnValueOnce(read);

    sentinelInView();
    height = 1600;
    answer(withCursor(true, message("m-3"), message("m-4")));
    await flushPromises();

    expect(el.scrollTop).toBe(600);
  });

  it("asks once while a page is on its way, and again only on the next sighting", async () => {
    await openRoom();
    await flushPromises();
    const { read, answer } = heldRead();
    mockGetChatMessages.mockReturnValueOnce(read);

    sentinelInView();
    sentinelInView();
    expect(olderReads()).toHaveLength(1);

    answer(withCursor(true, message("m-3"), message("m-4")));
    await flushPromises();
    expect(olderReads()).toHaveLength(1);

    mockGetChatMessages.mockResolvedValueOnce(
      withCursor(false, message("m-1"), message("m-2")),
    );
    sentinelInView();
    await flushPromises();
    expect(olderReads().map(([, cursor]) => cursor)).toEqual([
      "before-m-5",
      "before-m-3",
    ]);
  });

  it("stops at the start of the room: no sentinel, no further read", async () => {
    const wrapper = await openRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce(
      withCursor(false, message("m-1"), message("m-2")),
    );

    sentinelInView();
    await flushPromises();

    expect(wrapper.find(".top-sentinel").exists()).toBe(false);
    sentinelInView();
    await flushPromises();
    expect(olderReads()).toHaveLength(1);
  });

  it("takes an empty page for the start of the room", async () => {
    const wrapper = await openRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce(withCursor(false));

    sentinelInView();
    await flushPromises();

    expect(wrapper.find(".top-sentinel").exists()).toBe(false);
    expect(shownIds(wrapper)).toEqual(["msg-m-5", "msg-m-6"]);
  });

  it("keeps the window and the sentinel when the read fails", async () => {
    const wrapper = await openRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 500, title: "" },
    } as never);

    sentinelInView();
    await flushPromises();

    expect(shownIds(wrapper)).toEqual(["msg-m-5", "msg-m-6"]);
    expect(wrapper.find(".top-sentinel").exists()).toBe(true);
  });
});
