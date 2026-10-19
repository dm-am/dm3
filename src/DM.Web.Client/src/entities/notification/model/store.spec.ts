/**
 * @vitest-environment node
 */

// The ceiling of the debounce is what this covers; the debounce itself keeps it from being none.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { getUnreadCount } = vi.hoisted(() => ({
  getUnreadCount: vi.fn(),
}));

vi.mock("../api", () => ({
  notificationApi: { getUnreadCount },
}));

// A stub keeps the persisted session and the ui store out.
vi.mock("@/shared/stores", async () => {
  const { ref } = await import("vue");
  return { useAuthStore: () => ({ user: ref({ id: "u1" }) }) };
});

import { useNotificationStore } from "./store";

describe("the unread notifications count", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    getUnreadCount.mockResolvedValue({ data: { count: 3 }, error: null });
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("re-reads the count even while the events keep arriving", async () => {
    const store = useNotificationStore();

    // Pushes spaced closer together than the debounce, for longer than the
    // ceiling: this is what a lively evening looks like from the bell.
    for (let elapsed = 0; elapsed < 10_000; elapsed += 1500) {
      await store.fetchUnreadCount();
      await vi.advanceTimersByTimeAsync(1500);
      if (elapsed + 1500 < 10_000) {
        expect(getUnreadCount).not.toHaveBeenCalled();
      }
    }

    expect(getUnreadCount).toHaveBeenCalledTimes(1);
    expect(store.unreadCount).toBe(3);
  });

  it("still collapses a burst into one request", async () => {
    const store = useNotificationStore();

    await store.fetchUnreadCount();
    await vi.advanceTimersByTimeAsync(100);
    await store.fetchUnreadCount();
    await vi.advanceTimersByTimeAsync(100);
    await store.fetchUnreadCount();

    expect(getUnreadCount).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(2000);
    expect(getUnreadCount).toHaveBeenCalledTimes(1);
  });
});
