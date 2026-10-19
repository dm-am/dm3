/**
 * @vitest-environment jsdom
 */

/**
 * The unread of private messages through useMessagingStore (useChatUnread):
 * the count of the header's badge and its re-read under a stream of pushes.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import {
  mockGetChats,
  mockGetUnreadCount,
  mockMarkAsRead,
} from "./store.doubles";

vi.mock("../api/messagingApi", async () =>
  (await import("./store.doubles")).messagingApi(),
);

import { useMessagingStore } from "./store";
import { useAuthStore } from "@/shared/stores";

describe("useMessagingStore, the unread count", () => {
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

    // A page of the list misses the unread conversations on the pages after it.
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

  // A trailing debounce alone holds the count still for as long as a burst lasts.
  describe("under a stream of pushes", () => {
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
