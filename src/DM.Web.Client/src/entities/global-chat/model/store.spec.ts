/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { watch } from "vue";

const {
  mockGetMessages,
  mockGetMessagesBefore,
  mockGetMessagesAround,
  mockUpdateMessage,
  mockDeleteMessage,
  mockGetMessagesNearDate,
} = vi.hoisted(() => ({
  mockGetMessages: vi.fn(),
  mockGetMessagesBefore: vi.fn(),
  mockGetMessagesAround: vi.fn(),
  mockUpdateMessage: vi.fn(),
  mockDeleteMessage: vi.fn(),
  mockGetMessagesNearDate: vi.fn(),
}));

vi.mock("../api/globalChatApi", () => ({
  default: {
    getMessages: mockGetMessages,
    getMessagesBefore: mockGetMessagesBefore,
    getMessagesAround: mockGetMessagesAround,
    updateMessage: mockUpdateMessage,
    deleteMessage: mockDeleteMessage,
    getMessagesNearDate: mockGetMessagesNearDate,
  },
}));

import { useGlobalChatStore } from "./store";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";
import { deferred } from "@/shared/lib/testing/tasks";

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

  // The stored cursor points before the trimmed messages, and a message id is no cursor.
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

// A full page of unknown messages is a drifted window, not a tail to append.
describe("useGlobalChatStore, catching up after a gap", () => {
  const PAGE_SIZE = 50;
  const run = (from: number) =>
    Array.from({ length: PAGE_SIZE }, (_, i) => from + i);

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("reloads the window when a full latest page shares nothing with it", async () => {
    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();

    // Applied where it lands: a second read would blank the feed into skeletons.
    const loadingFlips: boolean[] = [];
    const stopWatching = watch(
      () => store.loading,
      (value) => loadingFlips.push(value),
      { flush: "sync" },
    );

    // Over a page arrived meanwhile, so nothing held is in the answer.
    mockGetMessages.mockResolvedValue(
      page(run(100), { prevCursor: "cursor-latest", hasPrev: true }),
    );
    await store.pollForNewer();
    stopWatching();

    expect(store.messages).toHaveLength(PAGE_SIZE);
    expect(store.messages[0].id).toBe("m100");
    // Reloaded, not appended: the paging metadata describes the new window.
    expect(store.prevCursor).toBe("cursor-latest");
    expect(loadingFlips).toEqual([]);
    expect(mockGetMessages).toHaveBeenCalledTimes(2);
  });

  // The caller measures the position: the store holds no feed.
  it("leaves the window in place under a reader who is not at the tail", async () => {
    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();

    mockGetMessages.mockResolvedValue(
      page(run(100), { prevCursor: "cursor-latest", hasPrev: true }),
    );
    await store.pollForNewer(false);

    expect(store.messages[0].id).toBe("m0");
    expect(store.messages).toHaveLength(PAGE_SIZE);
    expect(store.prevCursor).toBe("cursor-held");
  });

  it("replaces it on the read that finds him back at the tail", async () => {
    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();

    mockGetMessages.mockResolvedValue(
      page(run(100), { prevCursor: "cursor-latest", hasPrev: true }),
    );
    await store.pollForNewer(false);
    await store.pollForNewer(true);

    expect(store.messages[0].id).toBe("m100");
    expect(store.prevCursor).toBe("cursor-latest");
  });

  it("answers the queued catch-up with the position measured last", async () => {
    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();

    const inFlight = deferred<ReturnType<typeof page>>();
    mockGetMessages.mockReturnValueOnce(inFlight.promise);
    const atTheTail = store.pollForNewer(true);
    const scrolledUpMeanwhile = store.pollForNewer(false);

    // The read in flight brings nothing new; the queued one finds the window
    // drifted off, and the reader up in the history by then.
    mockGetMessages.mockResolvedValue(
      page(run(100), { prevCursor: "cursor-latest", hasPrev: true }),
    );
    inFlight.resolve(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );
    await Promise.all([atTheTail, scrolledUpMeanwhile]);

    expect(store.messages[0].id).toBe("m0");
    expect(store.prevCursor).toBe("cursor-held");
  });

  // A first load that failed leaves no cursors, so nothing appended would hang on anything.
  it("fills an empty feed whatever the reader's position says", async () => {
    mockGetMessages.mockResolvedValue({
      data: null,
      error: { type: "", title: "Ошибка", status: 500, traceId: "t" },
    });

    const store = useGlobalChatStore();
    await store.fetchMessages();
    expect(store.messages).toHaveLength(0);

    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );
    await store.pollForNewer(false);

    expect(store.messages).toHaveLength(PAGE_SIZE);
    expect(store.prevCursor).toBe("cursor-held");
  });

  // A page is full whenever the chat holds fifty messages, which is nearly always.
  it("appends a full page that still overlaps the window", async () => {
    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );

    const store = useGlobalChatStore();
    await store.fetchMessages();

    // One message went by: a full page again, and all of it known but the last.
    mockGetMessages.mockResolvedValue(
      page(run(1), { prevCursor: "cursor-later", hasPrev: true }),
    );
    await store.pollForNewer();

    expect(mockGetMessages).toHaveBeenCalledTimes(2);
    expect(store.messages).toHaveLength(PAGE_SIZE + 1);
    expect(store.messages[PAGE_SIZE].id).toBe("m50");
    // Appending says nothing about where the window starts, so the cursor the
    // held window was read with has to survive it.
    expect(store.prevCursor).toBe("cursor-held");
  });

  it("takes the paging of the page it applies when the feed came up empty", async () => {
    mockGetMessages.mockResolvedValue({
      data: null,
      error: { type: "", title: "Ошибка", status: 500, traceId: "t" },
    });

    const store = useGlobalChatStore();
    await store.fetchMessages();
    expect(store.messages).toHaveLength(0);

    mockGetMessages.mockResolvedValue(
      page(run(0), { prevCursor: "cursor-held", hasPrev: true }),
    );
    await store.pollForNewer();

    expect(store.messages).toHaveLength(PAGE_SIZE);
    expect(store.prevCursor).toBe("cursor-held");
    expect(store.hasMoreBefore).toBe(true);
  });

  it("makes one request out of two catch-ups that overlap", async () => {
    mockGetMessages.mockResolvedValue(page([1, 2]));

    const store = useGlobalChatStore();
    await store.fetchMessages();

    const inFlight = deferred<ReturnType<typeof page>>();
    mockGetMessages.mockReturnValueOnce(inFlight.promise);
    const first = store.pollForNewer();
    const second = store.pollForNewer();

    expect(mockGetMessages).toHaveBeenCalledTimes(2);

    mockGetMessages.mockResolvedValue(page([1, 2, 3]));
    inFlight.resolve(page([1, 2]));
    await Promise.all([first, second]);

    // The caller that arrived mid-flight is served after it, not alongside it.
    expect(mockGetMessages).toHaveBeenCalledTimes(3);
    expect(store.messages.map((m) => m.id)).toEqual(["m1", "m2", "m3"]);
  });

  it("drops an answer that arrives after the window was replaced", async () => {
    mockGetMessages.mockResolvedValue(page(run(0)));

    const store = useGlobalChatStore();
    await store.fetchMessages();

    const inFlight = deferred<ReturnType<typeof page>>();
    mockGetMessages.mockReturnValueOnce(inFlight.promise);
    const polling = store.pollForNewer();

    mockGetMessages.mockResolvedValue(page(run(200)));
    await store.fetchMessages();

    inFlight.resolve(page([60, 61]));
    await polling;

    expect(store.messages).toHaveLength(PAGE_SIZE);
    expect(store.messages[PAGE_SIZE - 1].id).toBe("m249");
  });

  // A short page is everything the server had.
  it("appends a short page instead of reloading on it", async () => {
    mockGetMessages.mockResolvedValue(page([1, 2]));

    const store = useGlobalChatStore();
    await store.fetchMessages();

    mockGetMessages.mockResolvedValue(page([80, 81]));
    await store.pollForNewer();

    expect(mockGetMessages).toHaveBeenCalledTimes(2);
    expect(store.messages.map((m) => m.id)).toEqual(["m1", "m2", "m80", "m81"]);
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

// Kiritimati is UTC+14, a zone no machine running this suite keeps.
describe("useGlobalChatStore, jumping to a date", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    mockGetMessagesNearDate.mockResolvedValue(page([1]));
  });

  afterEach(() => {
    setAccountTimeZone(null);
  });

  it("anchors at midnight of the account's zone for a signed-in reader", async () => {
    setAccountTimeZone("Pacific/Kiritimati");

    await useGlobalChatStore().navigateToDate("2026-09-27");

    expect(mockGetMessagesNearDate).toHaveBeenCalledWith(
      "2026-09-26T10:00:00.000Z",
      50,
    );
  });

  it("anchors at the device's midnight for a guest", async () => {
    setAccountTimeZone(null);

    await useGlobalChatStore().navigateToDate("2026-09-27");

    expect(mockGetMessagesNearDate).toHaveBeenCalledWith(
      new Date(2026, 8, 27).toISOString(),
      50,
    );
  });
});
