/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import { setActivePinia, createPinia } from "pinia";

const {
  mockGetMessages,
  mockGetMessagesBefore,
  mockGetChat,
  mockGetChats,
  mockGetUnreadCount,
  mockMarkAsRead,
  mockSendMessage,
  mockClearHistory,
  mockLeaveChat,
} = vi.hoisted(() => ({
  mockGetMessages: vi.fn(),
  mockGetMessagesBefore: vi.fn(),
  mockGetChat: vi.fn(),
  mockGetChats: vi.fn(),
  mockGetUnreadCount: vi.fn(),
  mockMarkAsRead: vi.fn(),
  mockSendMessage: vi.fn(),
  mockClearHistory: vi.fn(),
  mockLeaveChat: vi.fn(),
}));

vi.mock("../api/messagingApi", () => ({
  default: {
    getMessages: mockGetMessages,
    getMessagesBefore: mockGetMessagesBefore,
    getChat: mockGetChat,
    getChats: mockGetChats,
    getUnreadCount: mockGetUnreadCount,
    markAsRead: mockMarkAsRead,
    sendMessage: mockSendMessage,
    clearHistory: mockClearHistory,
    leaveChat: mockLeaveChat,
  },
}));

import { useMessagingStore } from "./store";
import { useAuthStore } from "@/shared/stores";

const message = (id: number) => ({
  id: `m${id}`,
  text: `text ${id}`,
  createdUtc: new Date(2026, 2, 14, 12, 0, id % 60).toISOString(),
});

const page = (ids: number[], paging: Record<string, unknown> = {}) => ({
  data: {
    resources: ids.map(message),
    paging: {
      prevCursor: null,
      nextCursor: null,
      hasPrev: false,
      hasNext: false,
      ...paging,
    },
  },
  error: null,
});

/** Opens a chat sitting on a page that has older history behind it. */
async function openChatWithHistory() {
  mockGetChat.mockResolvedValue({ data: { id: "c1" }, error: null });
  mockGetMessages.mockResolvedValue(
    page([2, 3], { prevCursor: "cursor-1", hasPrev: true }),
  );

  const store = useMessagingStore();
  await store.selectChat("c1" as never);
  await store.fetchMessages("c1" as never);
  return store;
}

describe("useMessagingStore, loading older messages", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("does not claim the history ended when the request failed", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Server Error" },
    });

    await store.fetchMoreBefore();

    // A failed page used to set hasMoreBefore false, which unmounts the
    // sentinel: the rest of the correspondence became unreachable for as long
    // as the chat stayed open, silently.
    expect(store.hasMoreBefore).toBe(true);
    expect(store.errorBefore).toBe("Не удалось загрузить сообщения");
    expect(store.messagesList).toHaveLength(2);
  });

  it("still ends the history when the server answers with an empty page", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue(page([]));

    await store.fetchMoreBefore();

    expect(store.hasMoreBefore).toBe(false);
    expect(store.errorBefore).toBeNull();
  });

  it("prepends the page and follows its cursor", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue(
      page([0, 1], { prevCursor: "cursor-0", hasPrev: true }),
    );

    await store.fetchMoreBefore();

    expect(store.messagesList.map((m) => m.id)).toEqual([
      "m0",
      "m1",
      "m2",
      "m3",
    ]);
    expect(store.hasMoreBefore).toBe(true);
  });

  it("drops a failure when the next attempt succeeds", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValueOnce({
      data: null,
      error: { status: 500, title: "Server Error" },
    });
    mockGetMessagesBefore.mockResolvedValueOnce(page([1], { hasPrev: false }));

    await store.fetchMoreBefore();
    await store.fetchMoreBefore();

    // The retry button is the only way back in, so a stale error would keep
    // the sentinel parked after it worked.
    expect(store.errorBefore).toBeNull();
    expect(store.messagesList.map((m) => m.id)).toEqual(["m1", "m2", "m3"]);
  });

  it("does not carry a failure into the next window it loads", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Server Error" },
    });
    await store.fetchMoreBefore();

    await store.fetchMessages("c1" as never);

    expect(store.errorBefore).toBeNull();
  });
});

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

describe("useMessagingStore, sending a message", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("hands the refusal back and adds nothing to the list", async () => {
    const store = await openChatWithHistory();
    mockSendMessage.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 400, traceId: "t" },
    });

    const { error } = await store.sendMessage("c1" as never, "письмо");

    // The page restores what was typed from this. While the answer was the
    // created message or null, a refusal and a success were the same value,
    // and the page emptied the composer either way.
    expect(error?.status).toBe(400);
    expect(store.messagesList.map((m) => m.id)).toEqual(["m2", "m3"]);
  });

  it("appends the sent message and points the chat at it", async () => {
    const store = await openChatWithHistory();
    mockSendMessage.mockResolvedValue({ data: message(4), error: null });

    const { error } = await store.sendMessage("c1" as never, "письмо");

    expect(error).toBeNull();
    expect(store.messagesList.map((m) => m.id)).toEqual(["m2", "m3", "m4"]);
    expect(store.selectedChat?.lastMessage?.id).toBe("m4");
  });

  describe("ответы вне порядка", () => {
    function deferred<T>() {
      let resolve!: (value: T) => void;
      const promise = new Promise<T>((settle) => {
        resolve = settle;
      });
      return { promise, resolve };
    }

    // Two quick switches between chat A and chat B put two requests on the
    // wire. The answer for A, arriving after the answer for B, used to
    // overwrite the state: the reader saw a chat they had already left, and the
    // read receipt for that one had been sent.
    it("оставляет ту переписку, которую выбрали последней", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetChat
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useMessagingStore();
      const first = store.selectChat("a" as never);
      const second = store.selectChat("b" as never);

      newer.resolve({ data: { id: "b" }, error: null });
      await second;
      older.resolve({ data: { id: "a" }, error: null });
      await first;

      expect(store.selectedChat?.id).toBe("b");
    });

    it("оставляет то окно сообщений, которое запросили последним", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetMessages
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useMessagingStore();
      const first = store.fetchMessages("a" as never);
      const second = store.fetchMessages("b" as never);

      newer.resolve({
        data: { resources: [{ id: "mb" }], paging: null },
        error: null,
      });
      await second;
      older.resolve({
        data: { resources: [{ id: "ma" }], paging: null },
        error: null,
      });
      await first;

      expect(store.messagesList.map((m) => m.id)).toEqual(["mb"]);
      expect(store.loadingMessages).toBe(false);
    });
  });
});

describe("useMessagingStore, the size of the window", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  /** A reader who set "messages per page" to thirty. */
  function readerChose(messagesPerPage: number) {
    useAuthStore().user = {
      settings: { paging: { messagesPerPage } },
    } as never;
  }

  // The preference was saved and never read: the window held a constant of its
  // own, so the choice moved nothing.
  it("asks for as many messages as the reader chose", async () => {
    readerChose(30);
    mockGetMessages.mockResolvedValue(page([1]));

    const store = useMessagingStore();
    await store.fetchMessages("c1" as never);

    expect(mockGetMessages).toHaveBeenCalledWith("c1", { limit: 30 });
  });

  it("keeps that size when it loads older messages", async () => {
    readerChose(30);
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue(page([0]));

    await store.fetchMoreBefore();

    expect(mockGetMessagesBefore).toHaveBeenCalledWith("c1", "cursor-1", 30);
  });

  // A size above every one the settings offer. Unclamped it comes back 400, and
  // fetchMessages reads only `data`: the conversation would be drawn empty, with
  // nothing on screen saying why. The cap is the largest size a reader may save,
  // because a preference the API refuses is a setting that does nothing.
  it("asks for no more than the API serves", async () => {
    readerChose(500);
    mockGetMessages.mockResolvedValue(page([1]));

    const store = useMessagingStore();
    await store.fetchMessages("c1" as never);

    expect(mockGetMessages).toHaveBeenCalledWith("c1", {
      limit: MAX_API_PAGE_SIZE,
    });
  });
});

describe("useMessagingStore, счетчик непрочитанных", () => {
  const counted = (count: number) => ({
    data: { resource: { count } },
    error: null,
  });

  const chatPage = (unreadMessagesCount: number) => ({
    data: {
      resources: [{ id: "c1", participants: [], unreadMessagesCount }],
      paging: null,
    },
    error: null,
  });

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    // The badge is only fetched for somebody who is signed in.
    useAuthStore().user = { username: "reader" } as never;
    // Answered even where the count is not supposed to ask for it, so that a
    // badge going back to the chat list fails on the assertion that says so
    // and not on an unstubbed call.
    mockGetChats.mockResolvedValue(chatPage(3));
    mockGetUnreadCount.mockResolvedValue(counted(5));
    mockMarkAsRead.mockResolvedValue({ data: null, error: null });
  });

  it("asks the counter, not a page of the chat list", async () => {
    mockGetUnreadCount.mockResolvedValue(counted(7));

    const store = useMessagingStore();
    await store.fetchUnreadCount(true);

    // The badge used to be the sum over `getChats({number: 1, take: 20})`, so
    // an unread conversation on page two was missing from the number, and the
    // recount dropped whatever page of the list the messenger was showing.
    expect(mockGetUnreadCount).toHaveBeenCalledTimes(1);
    expect(mockGetChats).not.toHaveBeenCalled();
    expect(store.totalUnreadCount).toBe(7);
    expect(store.chats).toBeNull();
  });

  it("takes a conversation out of the badge when it is read", async () => {
    const store = useMessagingStore();
    await store.fetchChats();
    await store.fetchUnreadCount(true);

    await store.markAsRead("c1" as never);

    // While the badge was summed from the list, zeroing the entry took it down
    // by itself; the server's number has to be told, or the header goes on
    // counting messages the reader has just opened.
    expect(store.totalUnreadCount).toBe(2);
  });

  /**
   * Pushes arrive in bursts and each one only means "the count changed", so one
   * request for a burst is the whole point of the debounce. What a plain
   * trailing debounce also does is postpone that request for as long as the
   * burst lasts: every deferred call restarted the wait, so a lively
   * correspondence held the number still exactly while there was most to
   * report, and released it once nothing was happening any more. The ceiling on
   * that wait is what the first test covers; the second is here to keep the fix
   * from turning into no debounce at all.
   */
  describe("под потоком пушей", () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("re-reads the count even while the pushes keep arriving", async () => {
      const store = useMessagingStore();

      // Pushes spaced closer together than the debounce, for longer than the
      // ceiling: this is what a busy conversation looks like from the header.
      for (let elapsed = 0; elapsed < 10_000; elapsed += 1500) {
        await store.fetchUnreadCount();
        await vi.advanceTimersByTimeAsync(1500);
        if (elapsed + 1500 < 10_000) {
          expect(mockGetUnreadCount).not.toHaveBeenCalled();
        }
      }

      expect(mockGetUnreadCount).toHaveBeenCalledTimes(1);
      expect(store.totalUnreadCount).toBe(5);
    });

    it("still collapses a burst into one request", async () => {
      const store = useMessagingStore();

      await store.fetchUnreadCount();
      await vi.advanceTimersByTimeAsync(100);
      await store.fetchUnreadCount();
      await vi.advanceTimersByTimeAsync(100);
      await store.fetchUnreadCount();

      expect(mockGetUnreadCount).not.toHaveBeenCalled();

      await vi.advanceTimersByTimeAsync(2000);
      expect(mockGetUnreadCount).toHaveBeenCalledTimes(1);
    });
  });
});

/**
 * The two things done to a correspondence from its menu. The server keeps the
 * truth; the store takes off the screen what the server will no longer give.
 */
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
