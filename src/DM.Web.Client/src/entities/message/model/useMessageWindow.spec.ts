/**
 * @vitest-environment jsdom
 */

/**
 * The window of a correspondence through useMessagingStore
 * (useMessageWindow): the pages older than it and their size, a day picked in
 * the calendar, the pages from there back toward the latest, and the first
 * day the calendar offers, as the global chat has them.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import { fromSiteInput } from "@/shared/lib/utils/datetime";
import { api, mockGetMessages, mockGetMessagesBefore } from "./store.doubles";
import { openChatWithHistory, page as numberedPage } from "./store.fixtures";

vi.mock("../api/messagingApi", async () =>
  (await import("./store.doubles")).messagingApi(),
);

import { useMessagingStore } from "./store";
import { useAuthStore } from "@/shared/stores";

/** A message written at noon of a day, so no zone moves it to another day. */
const message = (id: string, day = "2026-09-10") => ({
  id,
  text: id,
  createdUtc: `${day}T12:00:00Z`,
  likes: [],
});

type Msg = ReturnType<typeof message>;

const page = (
  resources: Msg[],
  paging: Partial<{
    prevCursor: string | null;
    nextCursor: string | null;
    hasPrev: boolean;
    hasNext: boolean;
  }> = {},
) => ({
  data: {
    resources,
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

const refused = () => ({
  data: null,
  error: { status: 500, title: "Server Error" },
});

const ids = (count: number, from = 0, day = "2026-09-10") =>
  Array.from({ length: count }, (_, i) => message(`m${from + i}`, day));

async function openChat() {
  api.getChat.mockResolvedValue({ data: { id: "c1" }, error: null });
  const store = useMessagingStore();
  await store.selectChat("c1" as never);
  return store;
}

beforeEach(() => {
  setActivePinia(createPinia());
  vi.clearAllMocks();
  api.getMessages.mockReset();
  api.getMessagesBefore.mockReset();
  api.getMessagesAfter.mockReset();
});

describe("useMessagingStore, loading older messages", () => {
  it("does not claim the history ended when the request failed", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Server Error" },
    });

    await store.fetchMoreBefore();

    // hasMoreBefore false unmounts the sentinel, and the rest of the history is lost.
    expect(store.hasMoreBefore).toBe(true);
    expect(store.errorBefore).toBe("Не удалось загрузить сообщения");
    expect(store.messagesList).toHaveLength(2);
  });

  it("still ends the history when the server answers with an empty page", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue(numberedPage([]));

    await store.fetchMoreBefore();

    expect(store.hasMoreBefore).toBe(false);
    expect(store.errorBefore).toBeNull();
  });

  it("prepends the page and follows its cursor", async () => {
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue(
      numberedPage([0, 1], { prevCursor: "cursor-0", hasPrev: true }),
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
    mockGetMessagesBefore.mockResolvedValueOnce(
      numberedPage([1], { hasPrev: false }),
    );

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

describe("useMessagingStore, the size of the window", () => {
  /** A reader who set "messages per page" to thirty. */
  function readerChose(pageSize: number) {
    useAuthStore().user = {
      settings: { pageSize },
    } as never;
  }

  it("asks for as many messages as the reader chose", async () => {
    readerChose(30);
    mockGetMessages.mockResolvedValue(numberedPage([1]));

    const store = useMessagingStore();
    await store.fetchMessages("c1" as never);

    expect(mockGetMessages).toHaveBeenCalledWith("c1", { limit: 30 });
  });

  it("keeps that size when it loads older messages", async () => {
    readerChose(30);
    const store = await openChatWithHistory();
    mockGetMessagesBefore.mockResolvedValue(numberedPage([0]));

    await store.fetchMoreBefore();

    expect(mockGetMessagesBefore).toHaveBeenCalledWith("c1", "cursor-1", 30);
  });

  // A size above every one the settings offer. Unclamped it comes back 400, and
  // fetchMessages reads only `data`: the conversation would be drawn empty, with
  // nothing on screen saying why. The cap is the largest size a reader may save,
  // because a preference the API refuses is a setting that does nothing.
  it("asks for no more than the API serves", async () => {
    readerChose(500);
    mockGetMessages.mockResolvedValue(numberedPage([1]));

    const store = useMessagingStore();
    await store.fetchMessages("c1" as never);

    expect(mockGetMessages).toHaveBeenCalledWith("c1", {
      limit: MAX_API_PAGE_SIZE,
    });
  });
});

describe("useMessagingStore, the window of a day", () => {
  it("asks for the messages near midnight of the day on the site's clock", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValue(page([message("a")]));

    await store.navigateToDate("c1" as never, "2026-09-10");

    expect(api.getMessages).toHaveBeenCalledWith(
      "c1",
      expect.objectContaining({
        nearTimestampUtc: fromSiteInput("2026-09-10T00:00"),
      }),
    );
  });

  it("lands on the day when the window holds a message of it", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValue(
      page([message("a", "2026-09-09"), message("b", "2026-09-10")], {
        hasNext: true,
        nextCursor: "after-b",
      }),
    );

    await store.navigateToDate("c1" as never, "2026-09-10");

    expect(store.landedOnRequestedDate).toBe(true);
    expect(store.messagesList.map((m) => m.id)).toEqual(["a", "b"]);
    expect(store.hasMoreAfter).toBe(true);
  });

  /**
   * A day with no message of its own still has the window the server lays
   * around the moment, as the global chat's has: the feed stands at its top,
   * on the day picked, and the reader pages on from there.
   */
  it("stands near the day when the day holds none, as the global chat does", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValueOnce(
      page([message("before", "2026-09-05"), message("later", "2026-09-20")], {
        hasNext: true,
      }),
    );

    await store.navigateToDate("c1" as never, "2026-09-10");

    expect(store.landedOnRequestedDate).toBe(true);
    expect(store.messagesList.map((m) => m.id)).toEqual(["before", "later"]);
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("stands on the latest messages when the correspondence has none near it", async () => {
    const store = await openChat();
    api.getMessages
      .mockResolvedValueOnce(page([]))
      .mockResolvedValueOnce(page([]));

    await store.navigateToDate("c1" as never, "2026-09-10");

    expect(store.landedOnRequestedDate).toBe(false);
    expect(api.getMessages).toHaveBeenLastCalledWith("c1", {
      limit: expect.any(Number),
    });
  });

  it("keeps the window there was and names the failure when the day did not load", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValueOnce(page([message("held")]));
    await store.fetchMessages("c1" as never);
    api.getMessages.mockResolvedValueOnce(refused());

    await store.navigateToDate("c1" as never, "2026-09-10");

    expect(store.landedOnRequestedDate).toBe(false);
    expect(store.messagesError).toBe("Не удалось загрузить сообщения");
    expect(store.messagesList.map((m) => m.id)).toEqual(["held"]);
    // A failure is not an empty day: nothing is asked in its place.
    expect(api.getMessages).toHaveBeenCalledTimes(2);
  });

  it("stands on the latest messages for a day that is not a day", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValue(page([message("latest")]));

    await store.navigateToDate("c1" as never, "не дата");

    expect(store.landedOnRequestedDate).toBe(false);
    expect(api.getMessages).toHaveBeenCalledTimes(1);
    expect(api.getMessages.mock.calls[0][1]).not.toHaveProperty(
      "nearTimestampUtc",
    );
  });
});

describe("useMessagingStore, the way back toward the latest messages", () => {
  async function onDay() {
    const store = await openChat();
    api.getMessages.mockResolvedValue(
      page([message("a"), message("b")], {
        hasNext: true,
        nextCursor: "after-b",
      }),
    );
    await store.navigateToDate("c1" as never, "2026-09-10");
    return store;
  }

  it("appends the next page and follows its cursor", async () => {
    const store = await onDay();
    api.getMessagesAfter.mockResolvedValue(
      page([message("c"), message("d")], {
        hasNext: true,
        nextCursor: "after-d",
      }),
    );

    await store.fetchMoreAfter();

    expect(api.getMessagesAfter).toHaveBeenCalledWith(
      "c1",
      "after-b",
      expect.any(Number),
    );
    expect(store.messagesList.map((m) => m.id)).toEqual(["a", "b", "c", "d"]);
    expect(store.hasMoreAfter).toBe(true);

    api.getMessagesAfter.mockResolvedValue(page([message("e")]));
    await store.fetchMoreAfter();
    expect(api.getMessagesAfter).toHaveBeenLastCalledWith(
      "c1",
      "after-d",
      expect.any(Number),
    );
    expect(store.hasMoreAfter).toBe(false);
  });

  it("keeps the way open and names the failure when a page did not come", async () => {
    const store = await onDay();
    api.getMessagesAfter.mockResolvedValue(refused());

    await store.fetchMoreAfter();

    expect(store.hasMoreAfter).toBe(true);
    expect(store.errorAfter).toBe("Не удалось загрузить сообщения");
    expect(store.messagesList.map((m) => m.id)).toEqual(["a", "b"]);
  });

  it("drops a page that lands after the window was replaced", async () => {
    const store = await onDay();
    let settle!: (answer: unknown) => void;
    api.getMessagesAfter.mockReturnValue(
      new Promise((resolve) => {
        settle = resolve;
      }),
    );
    const paging = store.fetchMoreAfter();
    api.getMessages.mockResolvedValue(page([message("latest", "2026-10-01")]));
    await store.fetchMessages("c1" as never);

    settle(page([message("c")]));
    await paging;

    expect(store.messagesList.map((m) => m.id)).toEqual(["latest"]);
  });

  /**
   * The window keeps 500 messages. Growing past that toward the latest drops
   * the oldest, and the cursor of the older side was cut against what was
   * dropped: following it would skip the stretch between it and what stays on
   * screen. The next page up is asked around the new first message instead.
   */
  it("asks the page above around the first message it kept once it trimmed", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValueOnce(
      page(ids(450, 0), {
        hasPrev: true,
        prevCursor: "before-m0",
        hasNext: true,
        nextCursor: "after-m449",
      }),
    );
    await store.navigateToMessage("c1" as never, "m200" as never);
    api.getMessagesAfter.mockResolvedValue(
      page(ids(100, 450), { hasNext: true, nextCursor: "after-m549" }),
    );

    await store.fetchMoreAfter();

    expect(store.messagesList).toHaveLength(500);
    expect(store.messagesList[0].id).toBe("m50");
    expect(store.hasMoreBefore).toBe(true);

    api.getMessages.mockResolvedValueOnce(page(ids(51, 25)));
    await store.fetchMoreBefore();

    expect(api.getMessagesBefore).not.toHaveBeenCalled();
    expect(api.getMessages).toHaveBeenLastCalledWith(
      "c1",
      expect.objectContaining({ aroundMessageId: "m50" }),
    );
    // The half of the page already on screen is not put there twice.
    expect(store.messagesList.slice(0, 26).map((m) => m.id)).toEqual(
      ids(26, 25).map((m) => m.id),
    );
    expect(new Set(store.messagesList.map((m) => m.id)).size).toBe(
      store.messagesList.length,
    );
  });

  it("asks the page below around the last message it kept once it trimmed", async () => {
    const store = await openChat();
    // A window in the middle of the history, with a cursor on either side.
    api.getMessages.mockResolvedValueOnce(
      page(ids(450, 100), {
        hasPrev: true,
        prevCursor: "before-m100",
        hasNext: true,
        nextCursor: "after-m549",
      }),
    );
    await store.navigateToMessage("c1" as never, "m300" as never);
    api.getMessagesBefore.mockResolvedValue(
      page(ids(100, 0), { hasPrev: false }),
    );

    await store.fetchMoreBefore();

    expect(store.messagesList).toHaveLength(500);
    expect(store.messagesList.at(-1)?.id).toBe("m499");
    expect(store.hasMoreAfter).toBe(true);

    api.getMessages.mockResolvedValueOnce(page(ids(51, 474)));
    await store.fetchMoreAfter();

    expect(api.getMessagesAfter).not.toHaveBeenCalled();
    expect(api.getMessages).toHaveBeenLastCalledWith(
      "c1",
      expect.objectContaining({ aroundMessageId: "m499" }),
    );
    expect(store.messagesList.at(-1)?.id).toBe("m524");
  });
});

describe("useMessagingStore, the first day of a correspondence", () => {
  it("takes it from the window when the window reaches the start", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValue(
      page([message("first", "2026-08-01"), message("b")]),
    );
    await store.fetchMessages("c1" as never);

    await store.fetchFirstMessage("c1" as never);

    expect(store.firstMessageUtc).toBe("2026-08-01T12:00:00Z");
    expect(api.getMessages).toHaveBeenCalledTimes(1);
  });

  it("asks the server once, near an instant before any message", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValueOnce(
      page([message("b")], { hasPrev: true, prevCursor: "before-b" }),
    );
    await store.fetchMessages("c1" as never);
    api.getMessages.mockResolvedValueOnce(
      page([message("first", "2025-01-15")]),
    );

    await store.fetchFirstMessage("c1" as never);
    await store.fetchFirstMessage("c1" as never);

    expect(api.getMessages).toHaveBeenCalledTimes(2);
    expect(api.getMessages).toHaveBeenLastCalledWith("c1", {
      nearTimestampUtc: new Date(0).toISOString(),
      limit: 1,
    });
    expect(store.firstMessageUtc).toBe("2025-01-15T12:00:00Z");
  });

  it("asks again after a failure, and forgets it with the correspondence", async () => {
    const store = await openChat();
    api.getMessages.mockResolvedValueOnce(
      page([message("b")], { hasPrev: true, prevCursor: "before-b" }),
    );
    await store.fetchMessages("c1" as never);
    api.getMessages.mockResolvedValueOnce(refused());
    await store.fetchFirstMessage("c1" as never);
    expect(store.firstMessageUtc).toBeNull();

    api.getMessages.mockResolvedValueOnce(
      page([message("first", "2025-01-15")]),
    );
    await store.fetchFirstMessage("c1" as never);
    expect(store.firstMessageUtc).toBe("2025-01-15T12:00:00Z");

    store.clearSelection();
    expect(store.firstMessageUtc).toBeNull();
  });
});
