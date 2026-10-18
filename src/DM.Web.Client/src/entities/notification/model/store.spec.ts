/**
 * @vitest-environment node
 */

/**
 * The bell debounces because pushes arrive in bursts and each one only means
 * "the count changed" - one request for a burst is the whole point of it. What
 * a plain trailing debounce also does is postpone that request for as long as
 * the burst lasts: every deferred call restarted the wait, so an evening in a
 * busy discussion held the number still exactly while there was most to report,
 * and released it once nothing was happening any more. So the wait has a
 * ceiling, and the ceiling is what this covers - the debounce itself is the
 * easy half and stands here to keep the fix from turning into no debounce.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { getUnreadCount } = vi.hoisted(() => ({
  getUnreadCount: vi.fn(),
}));

vi.mock("../api", () => ({
  notificationApi: { getUnreadCount },
}));

// Only the viewer is read out of the session, and the count is not fetched at
// all without one; a stub keeps the persisted copy and the ui store out.
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
