/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { mockGetMessages, mockGetMessagesBefore, mockGetChat } = vi.hoisted(
  () => ({
    mockGetMessages: vi.fn(),
    mockGetMessagesBefore: vi.fn(),
    mockGetChat: vi.fn(),
  }),
);

vi.mock("../api/messagingApi", () => ({
  default: {
    getMessages: mockGetMessages,
    getMessagesBefore: mockGetMessagesBefore,
    getChat: mockGetChat,
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
