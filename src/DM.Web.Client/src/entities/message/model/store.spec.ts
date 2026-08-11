/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { mockGetMessages, mockGetMessagesBefore, mockGetChat, mockSendMessage } =
  vi.hoisted(() => ({
    mockGetMessages: vi.fn(),
    mockGetMessagesBefore: vi.fn(),
    mockGetChat: vi.fn(),
    mockSendMessage: vi.fn(),
  }));

vi.mock("../api/messagingApi", () => ({
  default: {
    getMessages: mockGetMessages,
    getMessagesBefore: mockGetMessagesBefore,
    getChat: mockGetChat,
    sendMessage: mockSendMessage,
  },
}));

import { useMessagingStore } from "./store";

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

    // Два быстрых перехода между перепиской A и B кладут на провод по два
    // запроса. Ответ A, пришедший позже ответа B, перезаписывал состояние:
    // читатель видел переписку, из которой уже ушел, а отметка о прочтении для
    // нее была отправлена.
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
