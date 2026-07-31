/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const {
  mockGetMessages,
  mockGetMessagesBefore,
  mockGetMessagesAround,
  mockUpdateMessage,
  mockDeleteMessage,
} = vi.hoisted(() => ({
  mockGetMessages: vi.fn(),
  mockGetMessagesBefore: vi.fn(),
  mockGetMessagesAround: vi.fn(),
  mockUpdateMessage: vi.fn(),
  mockDeleteMessage: vi.fn(),
}));

vi.mock("../api/globalChatApi", () => ({
  default: {
    getMessages: mockGetMessages,
    getMessagesBefore: mockGetMessagesBefore,
    getMessagesAround: mockGetMessagesAround,
    updateMessage: mockUpdateMessage,
    deleteMessage: mockDeleteMessage,
  },
}));

import { useGlobalChatStore } from "./store";

const MAX_MESSAGES = 500;

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

describe("useGlobalChatStore, loading older messages", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("follows the cursor the server handed out", async () => {
    mockGetMessages.mockResolvedValue(
      page([2, 3], { prevCursor: "cursor-1", hasPrev: true }),
    );
    mockGetMessagesBefore.mockResolvedValue(
      page([0, 1], { prevCursor: "cursor-0", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();
    await store.fetchMoreBefore();

    expect(mockGetMessagesBefore).toHaveBeenCalledWith("cursor-1", 50);
    expect(store.messages.map((m) => m.id)).toEqual(["m0", "m1", "m2", "m3"]);
  });

  /**
   * Trimming drops the very messages the stored cursor points before, so keeping
   * it would fetch a page older than what stays on screen and leave a gap. The
   * message id that used to be put here instead is not a cursor at all: the
   * server fails to decode it and answers with the latest page, which prepended
   * the 50 newest messages to the top of the scrollback.
   */
  it("re-anchors on the new first message after a trim instead of inventing a cursor", async () => {
    const held = Array.from({ length: MAX_MESSAGES + 10 }, (_, i) => i);
    mockGetMessages.mockResolvedValue(
      page(held, { prevCursor: "cursor-oldest", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();
    // A newer message arriving is what pushes the buffer over the cap and trims it.
    mockGetMessages.mockResolvedValue(page([held.length]));
    await store.pollForNewer();

    expect(store.messages).toHaveLength(MAX_MESSAGES);
    const firstHeld = store.messages[0].id;

    mockGetMessagesAround.mockResolvedValue(
      page([-2, -1, Number(firstHeld.slice(1))], {
        prevCursor: "cursor-older",
        hasPrev: true,
      }),
    );

    await store.fetchMoreBefore();

    expect(mockGetMessagesBefore).not.toHaveBeenCalled();
    expect(mockGetMessagesAround).toHaveBeenCalledWith(firstHeld, 50);
    // Only the half older than the anchor is prepended; the anchor itself is held.
    expect(store.messages.slice(0, 3).map((m) => m.id)).toEqual([
      "m-2",
      "m-1",
      firstHeld,
    ]);
    expect(store.messages.filter((m) => m.id === firstHeld)).toHaveLength(1);
  });

  it("asks for the latest page when polling rather than passing a message id as a cursor", async () => {
    mockGetMessages.mockResolvedValue(
      page([1, 2], { prevCursor: "cursor-1", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();

    mockGetMessages.mockResolvedValue(page([2, 3]));
    await store.pollForNewer();

    expect(mockGetMessages).toHaveBeenLastCalledWith({ limit: 50 });
    expect(store.messages.map((m) => m.id)).toEqual(["m1", "m2", "m3"]);
  });
});

describe("useGlobalChatStore, editing and deleting a message", () => {
  const refusal = {
    type: "",
    title: "Недостаточно прав",
    status: 403,
    traceId: "t",
  };

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  /**
   * A refused delete used to strike the message through anyway, so the
   * moderator read the deleted-message placeholder next to the toast saying
   * the delete had failed.
   */
  it("leaves the message alone when the delete is refused", async () => {
    mockGetMessages.mockResolvedValue(page([1]));
    mockDeleteMessage.mockResolvedValue({ data: null, error: refusal });

    const store = useGlobalChatStore();
    await store.fetchMessages();

    const { error } = await store.deleteMessage("m1");

    expect(error).toBe(refusal);
    expect(store.messages[0].isRemoved).toBeFalsy();
  });

  it("keeps the message when the edit is refused", async () => {
    mockGetMessages.mockResolvedValue(page([1]));
    mockUpdateMessage.mockResolvedValue({ data: null, error: refusal });

    const store = useGlobalChatStore();
    await store.fetchMessages();

    const { error } = await store.updateMessage("m1", "Новый текст");

    expect(error).toBe(refusal);
    expect(store.messages[0].text).toBe("text 1");
  });
});
