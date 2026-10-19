/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";

const { mockGetEvents } = vi.hoisted(() => ({
  mockGetEvents: vi.fn(),
}));

vi.mock("../api/globalChatApi", () => ({
  default: {
    getEvents: mockGetEvents,
  },
}));

import { useGlobalChatEventsStore } from "./eventsStore";
import { NotificationType } from "@/shared/api/models/notifications";

const chatEvent = {
  id: "e1",
  title: "Вечер быстрых зарисовок",
  startsUtc: "2026-08-04T20:00:00Z",
  status: "Live",
  isOpen: true,
  participantCount: 3,
};

// A push about an event start or end rereads the list, and the list holds what came back.
describe("useGlobalChatEventsStore, the events list on a realtime push", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    mockGetEvents.mockResolvedValue({
      data: { resources: [chatEvent] },
      error: null,
    });
  });

  it("re-reads the events when one starts", async () => {
    const store = useGlobalChatEventsStore();

    await store.refreshEventsOnNotification(
      NotificationType.GlobalChatEventStarted,
    );

    expect(mockGetEvents).toHaveBeenCalledTimes(1);
    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });

  it("re-reads the events when one ends", async () => {
    const store = useGlobalChatEventsStore();

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
    const store = useGlobalChatEventsStore();

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

    const store = useGlobalChatEventsStore();
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
    const store = useGlobalChatEventsStore();
    await store.fetchEvents();

    await store.fetchEventsIfFresh();

    expect(mockGetEvents).toHaveBeenCalledTimes(1);
  });

  it("reads the list when nothing has read it yet", async () => {
    const store = useGlobalChatEventsStore();

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

    const store = useGlobalChatEventsStore();
    await store.fetchEvents();
    await store.fetchEventsIfFresh();

    expect(mockGetEvents).toHaveBeenCalledTimes(2);
    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });
});

// An empty list says "no events" only once a read has answered with one.
describe("useGlobalChatEventsStore, how the read of the events ended", () => {
  const refusal = { data: null, error: { status: 500 } };

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    // Answers queued by a test that failed early stay out of the next one.
    mockGetEvents.mockReset();
  });

  it("is pending until a read has answered", () => {
    expect(useGlobalChatEventsStore().eventsLoad).toBe("pending");
  });

  it("is loaded by an answer, an empty one included", async () => {
    mockGetEvents.mockResolvedValue({ data: { resources: [] }, error: null });
    const store = useGlobalChatEventsStore();

    await store.fetchEvents();

    expect(store.eventsLoad).toBe("loaded");
  });

  it("is failed by a refusal, and keeps the list it had", async () => {
    mockGetEvents
      .mockResolvedValueOnce({ data: { resources: [chatEvent] }, error: null })
      .mockResolvedValueOnce(refusal);
    const store = useGlobalChatEventsStore();

    await store.fetchEvents();
    await store.fetchEvents();

    expect(store.eventsLoad).toBe("failed");
    expect(store.events.map((e) => e.id)).toEqual(["e1"]);
  });

  it("is loaded again by the read after a refusal", async () => {
    mockGetEvents
      .mockResolvedValueOnce(refusal)
      .mockResolvedValueOnce({ data: { resources: [] }, error: null });
    const store = useGlobalChatEventsStore();

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
    const store = useGlobalChatEventsStore();

    const first = store.fetchEvents();
    await store.fetchEvents();
    settleFirst(refusal);
    await first;

    expect(store.eventsLoad).toBe("loaded");
  });
});
