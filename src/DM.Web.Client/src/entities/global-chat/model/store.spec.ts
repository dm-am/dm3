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
  mockGetEvents,
  mockGetMessagesNearDate,
} = vi.hoisted(() => ({
  mockGetMessages: vi.fn(),
  mockGetMessagesBefore: vi.fn(),
  mockGetMessagesAround: vi.fn(),
  mockUpdateMessage: vi.fn(),
  mockDeleteMessage: vi.fn(),
  mockGetEvents: vi.fn(),
  mockGetMessagesNearDate: vi.fn(),
}));

vi.mock("../api/globalChatApi", () => ({
  default: {
    getMessages: mockGetMessages,
    getMessagesBefore: mockGetMessagesBefore,
    getMessagesAround: mockGetMessagesAround,
    updateMessage: mockUpdateMessage,
    deleteMessage: mockDeleteMessage,
    getEvents: mockGetEvents,
    getMessagesNearDate: mockGetMessagesNearDate,
  },
}));

import { useGlobalChatStore } from "./store";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";
import { NotificationType } from "@/shared/api/models/notifications";

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

/**
 * Catching up asks for the latest page, and that page holds 50 messages. A
 * socket that was down long enough, or a tab that slept through a busy evening,
 * comes back to a full page with nothing in it that is on screen — and
 * appending that used to splice two windows together over a hole no scrolling
 * could reach, with nothing saying a stretch of the conversation was missing.
 */
describe("useGlobalChatStore, catching up after a gap", () => {
  const PAGE_SIZE = 50;
  const run = (from: number) =>
    Array.from({ length: PAGE_SIZE }, (_, i) => from + i);

  /** A request the test resolves itself, to hold an answer in flight. */
  function deferred<T>() {
    let resolve!: (value: T) => void;
    const promise = new Promise<T>((settle) => {
      resolve = settle;
    });
    return { promise, resolve };
  }

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

    // The page that comes back IS the window to show, so it is applied where it
    // lands: asking for it a second time costs a request, and that second read
    // goes through the initial-load path, which blanks the feed into skeletons
    // and turns a failure into an error screen over messages that were readable.
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

  /**
   * The window is the page the reader is reading. Replacing it under a reader
   * who has scrolled up takes that page away and leaves him at the end of a
   * conversation he never asked to be taken to, so the replacement waits for
   * his return to the tail. Where he stands is the caller's to measure: the
   * store holds no feed and cannot see one.
   */
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

  /**
   * The call that arrives while a read is in flight is served by one more run
   * after it, and the reader may well have moved in between — which is exactly
   * what a push landing on a reader who has just scrolled up looks like. The
   * run is answered with the position measured last, not with the one the read
   * in flight was started on.
   */
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

  /**
   * A feed that holds nothing has no reader in it to take a page away from,
   * and it is left empty by a first load that failed — with no cursors either,
   * so anything appended to it hangs on nothing.
   */
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

  /**
   * The rule is about the messages nobody has seen, not about the size of the
   * answer. A page is full whenever the chat holds fifty live messages, which
   * is nearly always, so treating a full page as a window that has drifted off
   * throws away the held window on the quietest of chats: one new message, and
   * the reader loses the scrollback he had.
   */
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

  /**
   * A feed left empty by a first load that failed has no cursors either, so a
   * page appended to it hangs on nothing: the sentinel at the top is mounted
   * and has nowhere to go, and the scrollback stops at whatever the poll
   * happened to bring.
   */
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

  /**
   * A push and the timer tick fall on the same moment easily enough. Two reads
   * of the same page then race, and the answers apply in whichever order they
   * come back.
   */
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

  /**
   * The window can be replaced while a catch-up is in flight: a search hit, a
   * date jump, a reload of the latest page. What comes back then is the tail of
   * a window nobody is looking at, and appending it puts older messages after
   * newer ones with no order left between them.
   */
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

  /**
   * A page shorter than the limit is everything the server had, so there is
   * nothing between it and what is held however little the two share — and a
   * feed still empty shares nothing with any page at all.
   */
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

/**
 * The events list was read once, when the chat opened, and nothing ever asked
 * for it again: an event could start and finish in front of an open tab while
 * the line above the feed went on describing page load. Both ends of the fix
 * are held here, because the push is only half of it — the list also has to end
 * up holding what came back.
 */
describe("useGlobalChatStore, the events list on a realtime push", () => {
  const chatEvent = {
    id: "e1",
    title: "Вечер быстрых зарисовок",
    startsUtc: "2026-08-04T20:00:00Z",
    status: "Live",
    isOpen: true,
    participantCount: 3,
  };

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    mockGetEvents.mockResolvedValue({
      data: { resources: [chatEvent] },
      error: null,
    });
  });

  it("re-reads the events when one starts", async () => {
    const store = useGlobalChatStore();

    await store.refreshEventsOnNotification(
      NotificationType.GlobalChatEventStarted,
    );

    expect(mockGetEvents).toHaveBeenCalledTimes(1);
    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });

  it("re-reads the events when one ends", async () => {
    const store = useGlobalChatStore();

    await store.refreshEventsOnNotification(
      NotificationType.GlobalChatEventEnded,
    );

    expect(mockGetEvents).toHaveBeenCalledTimes(1);
  });

  /**
   * One socket carries every message of the chat as well. Re-reading the events
   * on each of them would put a request behind every line anybody types.
   */
  it("leaves them alone on a push about anything else", async () => {
    const store = useGlobalChatStore();

    await store.refreshEventsOnNotification(
      NotificationType.NewGlobalChatMessage,
    );

    expect(mockGetEvents).not.toHaveBeenCalled();
  });

  /**
   * The list is read from several places at once: the page on open, the socket
   * coming up, a push about an event starting. Two of those overlap easily, and
   * without an order between the answers the slower one wins, which is to say
   * the list on screen is the older of the two that came back.
   */
  it("keeps the newer answer when two reads overlap", async () => {
    const ended = { ...chatEvent, id: "e0", status: "Finished" };
    let settleFirst!: (value: unknown) => void;
    mockGetEvents.mockReturnValueOnce(
      new Promise((resolve) => {
        settleFirst = resolve;
      }),
    );

    const store = useGlobalChatStore();
    const first = store.fetchEvents();
    const second = store.fetchEvents();
    await second;

    settleFirst({ data: { resources: [ended] }, error: null });
    await first;

    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });

  /**
   * "Has it just been read" is one question, and both halves of it live here:
   * the moment and the arithmetic over it. Split across two files, with the
   * page subtracting a moment the store wrote down, it was a rule nothing could
   * read whole and no test could reach.
   */
  it("leaves the list alone when it has only just been read", async () => {
    const store = useGlobalChatStore();
    await store.fetchEvents();

    await store.fetchEventsIfFresh();

    expect(mockGetEvents).toHaveBeenCalledTimes(1);
  });

  it("reads the list when nothing has read it yet", async () => {
    const store = useGlobalChatStore();

    await store.fetchEventsIfFresh();

    expect(mockGetEvents).toHaveBeenCalledTimes(1);
  });

  /**
   * A read that failed is not a read. Counting it as one would leave the strip
   * describing page load for the whole window, and the caller with no reason to
   * ask again.
   */
  it("reads the list again after a read that failed", async () => {
    mockGetEvents.mockResolvedValueOnce({
      data: null,
      error: { status: 500 },
    });

    const store = useGlobalChatStore();
    await store.fetchEvents();
    await store.fetchEventsIfFresh();

    expect(mockGetEvents).toHaveBeenCalledTimes(2);
    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });
});

/**
 * An empty list is a claim: "no events". The strip above the feed made it
 * before the list had answered and after the server had refused it, the
 * second time for as long as the page stayed open. How the newest read ended
 * is what lets the strip say "pending" and "did not load" instead.
 */
describe("useGlobalChatStore, how the read of the events ended", () => {
  const chatEvent = {
    id: "e1",
    title: "Вечер быстрых зарисовок",
    startsUtc: "2026-08-04T20:00:00Z",
    status: "Live",
    isOpen: true,
    participantCount: 3,
  };
  const refusal = { data: null, error: { status: 500 } };

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    // Answers queued by a test that failed early stay out of the next one.
    mockGetEvents.mockReset();
  });

  it("is pending until a read has answered", () => {
    expect(useGlobalChatStore().eventsLoad).toBe("pending");
  });

  it("is loaded by an answer, an empty one included", async () => {
    mockGetEvents.mockResolvedValue({ data: { resources: [] }, error: null });
    const store = useGlobalChatStore();

    await store.fetchEvents();

    expect(store.eventsLoad).toBe("loaded");
  });

  it("is failed by a refusal, and keeps the list it had", async () => {
    mockGetEvents
      .mockResolvedValueOnce({ data: { resources: [chatEvent] }, error: null })
      .mockResolvedValueOnce(refusal);
    const store = useGlobalChatStore();

    await store.fetchEvents();
    await store.fetchEvents();

    expect(store.eventsLoad).toBe("failed");
    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });

  it("is loaded again by the read after a refusal", async () => {
    mockGetEvents
      .mockResolvedValueOnce(refusal)
      .mockResolvedValueOnce({ data: { resources: [] }, error: null });
    const store = useGlobalChatStore();

    await store.fetchEvents();
    expect(store.eventsLoad).toBe("failed");
    await store.fetchEvents();

    expect(store.eventsLoad).toBe("loaded");
  });

  it("is not moved by an answer older than the newest read", async () => {
    let settleFirst!: (value: unknown) => void;
    mockGetEvents
      .mockReturnValueOnce(
        new Promise((resolve) => {
          settleFirst = resolve;
        }),
      )
      .mockResolvedValueOnce({ data: { resources: [chatEvent] }, error: null });
    const store = useGlobalChatStore();

    const first = store.fetchEvents();
    await store.fetchEvents();
    settleFirst(refusal);
    await first;

    expect(store.eventsLoad).toBe("loaded");
  });
});

/**
 * "Перейти к дате" asks the server for the messages around midnight of the
 * picked day, and whose midnight it is decides which day comes back. The chat
 * prints its dates on the site's clock (the account's zone for a signed-in
 * reader), and the jump anchored at the device's midnight: a reader whose
 * account keeps another zone than the device picked a day and landed on the
 * one next to it.
 *
 * Kiritimati is UTC+14, a zone no machine running this suite keeps, so the
 * device's midnight can never pass for the account's here.
 */
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
