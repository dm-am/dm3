/**
 * @vitest-environment node
 */

/**
 * On a narrow screen the statistics are drawn by two blocks in turn, the one
 * of the header and the one of the right column of a phone, and while the
 * shell switches between them both are on screen for a moment. The store
 * polled for whoever called last: the block that went stopped the polling of
 * the one that stayed, and the numbers froze until the tab was hidden and
 * shown again.
 *
 * The store polls while any block that asked for it is still there. A block
 * asks once however many times it calls: one mounted in a hidden tab starts on
 * mount and again when the tab is shown, and hiding the tab must still stop it.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";

const { mockGetLiveStats } = vi.hoisted(() => ({
  mockGetLiveStats: vi.fn(),
}));

vi.mock("../api", () => ({
  statisticsApi: { getLiveStats: mockGetLiveStats },
}));

import { useStatisticsStore } from "./store";

const MINUTE = 60_000;

/** How many times the store asked the server over the next few minutes. */
async function asksOver(minutes: number) {
  mockGetLiveStats.mockClear();
  await vi.advanceTimersByTimeAsync(minutes * MINUTE);
  return mockGetLiveStats.mock.calls.length;
}

describe("the polling of the statistics", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.useFakeTimers();
    mockGetLiveStats.mockReset();
    mockGetLiveStats.mockResolvedValue({ data: null, error: null });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("goes on for the block that stays when the other one goes", async () => {
    const store = useStatisticsStore();
    const header = {};
    const column = {};

    store.startPolling(header);
    store.startPolling(column);
    store.stopPolling(column);

    expect(await asksOver(3)).toBe(3);
  });

  it("asks the server once for two blocks, not once each", async () => {
    const store = useStatisticsStore();

    store.startPolling({});
    store.startPolling({});

    expect(mockGetLiveStats).toHaveBeenCalledTimes(1);
    expect(await asksOver(3)).toBe(3);
  });

  it("stops when the last block goes", async () => {
    const store = useStatisticsStore();
    const header = {};
    const column = {};

    store.startPolling(header);
    store.startPolling(column);
    store.stopPolling(header);
    store.stopPolling(column);

    expect(await asksOver(3)).toBe(0);
  });

  it("stops for a block that started twice, on mount and on the tab shown", async () => {
    const store = useStatisticsStore();
    const block = {};

    store.startPolling(block);
    store.startPolling(block);
    store.stopPolling(block);

    expect(await asksOver(3)).toBe(0);
  });

  it("starts again for a block that comes back", async () => {
    const store = useStatisticsStore();
    const block = {};

    store.startPolling(block);
    store.stopPolling(block);
    store.startPolling(block);

    expect(await asksOver(3)).toBe(3);
  });
});
