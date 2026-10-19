/**
 * @vitest-environment jsdom
 */

/**
 * The correspondences of useMessagingStore: the one that does not come, and
 * the two things done to one from its menu. The window of messages, the
 * unread and the reader's changes are specced beside their parts.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import {
  mockGetChat,
  mockGetMessages,
  mockClearHistory,
  mockLeaveChat,
} from "./store.doubles";
import { message, page, openChatWithHistory } from "./store.fixtures";

vi.mock("../api/messagingApi", async () =>
  (await import("./store.doubles")).messagingApi(),
);

import { useMessagingStore } from "./store";

/**
 * "Переписка не найдена" is the server's answer and nothing else: a page that
 * said it while the correspondence was on its way, or when the network failed
 * to bring it, sent the reader away from a conversation that was there.
 */
describe("useMessagingStore, a correspondence that does not come", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("calls a 404 missing and nothing else", async () => {
    mockGetChat.mockResolvedValue({
      data: null,
      error: { status: 404, title: "Переписка не найдена" },
    });
    const store = useMessagingStore();

    await store.selectChat("c1" as never);

    expect(store.chatMissing).toBe(true);
    expect(store.chatError).toBeNull();
  });

  it("calls a failed request a failure, not a missing correspondence", async () => {
    mockGetChat.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Server Error" },
    });
    const store = useMessagingStore();

    await store.selectChat("c1" as never);

    expect(store.chatMissing).toBe(false);
    expect(store.chatError).toBe("Не удалось загрузить переписку");
    // The list of correspondences keeps its own error to itself.
    expect(store.error).toBeNull();
  });

  it("forgets the last answer when the next correspondence is asked for", async () => {
    mockGetChat.mockResolvedValueOnce({
      data: null,
      error: { status: 404, title: "Переписка не найдена" },
    });
    const store = useMessagingStore();
    await store.selectChat("c1" as never);

    let settle!: (answer: unknown) => void;
    mockGetChat.mockReturnValueOnce(
      new Promise((resolve) => {
        settle = resolve;
      }),
    );
    const loading = store.selectChat("c2" as never);

    expect(store.chatMissing).toBe(false);
    settle({ data: { id: "c2" }, error: null });
    await loading;
    expect(store.chatMissing).toBe(false);
  });

  it("keeps the window on screen and names the failure when it does not load", async () => {
    const store = await openChatWithHistory();
    mockGetMessages.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Server Error" },
    });

    await store.fetchMessages("c1" as never);

    expect(store.messagesError).toBe("Не удалось загрузить сообщения");
    expect(store.messagesList.map((m) => m.id)).toEqual(["m2", "m3"]);
  });

  it("drops the failure once the window loads", async () => {
    const store = await openChatWithHistory();
    mockGetMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 500, title: "Server Error" },
    });
    await store.fetchMessages("c1" as never);

    mockGetMessages.mockResolvedValueOnce(page([4]));
    await store.fetchMessages("c1" as never);

    expect(store.messagesError).toBeNull();
    expect(store.messagesList.map((m) => m.id)).toEqual(["m4"]);
  });
});

// The store takes off the screen what the server will no longer give.
describe("useMessagingStore, the history cleared and the group left", () => {
  const listed = (
    rows: Array<{ id: string; unread?: number; last?: boolean }>,
  ) => ({
    resources: rows.map((row) => ({
      id: row.id,
      type: "Group",
      participants: [],
      unreadMessagesCount: row.unread ?? 0,
      lastMessage: row.last ? message(9) : undefined,
    })),
    paging: { pages: 1, current: 1, skip: 0, take: 20, total: rows.length },
  });

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("empties the open window, the row's last message and its unread once cleared", async () => {
    const store = await openChatWithHistory();
    store.chats = listed([
      { id: "c1", unread: 3, last: true },
    ]) as unknown as typeof store.chats;
    store.totalUnreadCount = 5;
    mockClearHistory.mockResolvedValue({ data: null, error: null });

    const result = await store.clearHistory("c1" as never, {
      ownsRefusal: true,
    });

    expect(result).toEqual({ error: null });
    expect(mockClearHistory).toHaveBeenCalledWith("c1", { ownsRefusal: true });
    expect(store.messagesList).toEqual([]);
    expect(store.hasMoreBefore).toBe(false);
    expect(store.chats?.resources[0].lastMessage).toBeUndefined();
    expect(store.chats?.resources[0].unreadMessagesCount).toBe(0);
    expect(store.totalUnreadCount).toBe(2);
  });

  it("keeps everything and hands the refusal back when the clearing is refused", async () => {
    const store = await openChatWithHistory();
    const refusal = { status: 403, title: "Нельзя" };
    mockClearHistory.mockResolvedValue({ data: null, error: refusal });

    const result = await store.clearHistory("c1" as never);

    expect(result).toEqual({ error: refusal });
    expect(store.messagesList.map((m) => m.id)).toEqual(["m2", "m3"]);
  });

  it("takes a left group out of the list and its unread out of the count", async () => {
    const store = useMessagingStore();
    store.chats = listed([
      { id: "g1", unread: 2 },
      { id: "c2", unread: 1 },
    ]) as unknown as typeof store.chats;
    store.totalUnreadCount = 3;
    mockLeaveChat.mockResolvedValue({ data: null, error: null });

    const result = await store.leaveChat("g1" as never);

    expect(result).toEqual({ error: null });
    expect(store.chats?.resources.map((c) => c.id)).toEqual(["c2"]);
    expect(store.chats?.paging?.total).toBe(1);
    expect(store.totalUnreadCount).toBe(1);
  });

  it("keeps the group in the list when leaving is refused", async () => {
    const store = useMessagingStore();
    store.chats = listed([{ id: "g1" }]) as unknown as typeof store.chats;
    mockLeaveChat.mockResolvedValue({
      data: null,
      error: { status: 403, title: "Нельзя" },
    });

    await store.leaveChat("g1" as never);

    expect(store.chats?.resources.map((c) => c.id)).toEqual(["g1"]);
  });
});
