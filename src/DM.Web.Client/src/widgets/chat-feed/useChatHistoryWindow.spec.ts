/**
 * @vitest-environment jsdom
 */

/**
 * Which part of a chat's history the feed shows: a day picked in the
 * calendar, a message a link or a search lands on, the way back to the latest
 * messages and the catch-up that makes an archive window live again, on the
 * real router: a change of ?date reaches the window as a navigation.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, nextTick, reactive, ref } from "vue";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import type { RouteLocationNormalized, Router } from "vue-router";
import type { Message } from "@/shared/api/models/common";
import { LANDING_SCROLL_MS } from "@/shared/lib/composables/useAnchoredInfiniteScroll";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";

import {
  MESSAGE_GONE_NOTICE,
  emptyDayNotice,
  useChatHistoryWindow,
} from "./useChatHistoryWindow";

enableAutoUnmount(afterEach);

const routes: Address[] = [["global-chat", "/chat"]];

/** The router of the window last opened, and every navigation after it opened. */
let router: Router;
let moves: RouteLocationNormalized[];
const address = () => router.currentRoute.value.fullPath;

const message = (id: string, createdUtc: string) =>
  ({ id, createdUtc }) as Message;

/** A chat at the address, whose store answers as the test sets it, behind a feed that records. */
async function chatWindow(
  over: {
    at?: string;
    held?: Message[];
    landed?: boolean;
    failed?: boolean;
    hasMoreAfter?: boolean;
    atBottom?: boolean;
    found?: (id: string) => Message[];
  } = {},
) {
  const state = reactive({
    held: over.held ?? [message("m1", "2026-10-01T12:00:00Z")],
    failed: over.failed ?? false,
    hasMoreAfter: over.hasMoreAfter ?? false,
    loading: false,
    atBottom: over.atBottom ?? true,
  });
  const feed = {
    scrollToBottom: vi.fn(),
    scrollToMessage: vi.fn(),
    atBottom: () => state.atBottom,
    setupInfiniteScroll: vi.fn(),
    suspendInfiniteScroll: vi.fn(),
    resumeInfiniteScroll: vi.fn(),
  };
  const load = {
    latest: vi.fn(async () => {
      state.held = [message("tail", "2026-10-09T12:00:00Z")];
    }),
    date: vi.fn(async () => {}),
    message: vi.fn(async (id: string) => {
      if (over.found) state.held = over.found(id);
    }),
  };
  let history!: ReturnType<typeof useChatHistoryWindow>;
  const Shell = defineComponent({
    setup() {
      history = useChatHistoryWindow({
        feed: ref(feed),
        location: () => ({ name: "global-chat", query: { page: "2" } }),
        messages: () => state.held,
        loading: () => state.loading,
        failed: () => state.failed,
        hasMoreAfter: () => state.hasMoreAfter,
        landedOnRequestedDate: () => over.landed ?? true,
        load,
      });
      return () => null;
    },
  });
  moves = [];
  ({ router } = await mountPage(Shell, over.at ?? "/chat", {
    routes,
    beforeMount: (r) => r.afterEach((to) => void moves.push(to)),
  }));
  return { history, feed, load, state };
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  vi.useRealTimers();
});

/** The day picked in "Перейти к дате" stays picked unless the store fell back to the latest. */
describe("a day picked in the calendar", () => {
  /**
   * The window near 2 October, as the server builds it: around the first
   * message from the day's midnight on (m6, nothing is said from the 1st to
   * the 5th), with the messages before that one ahead of it.
   */
  const NEAR_THE_SECOND = [
    message("m1", "2026-09-30T12:00:00Z"),
    message("m6", "2026-10-06T12:00:00Z"),
    message("m7", "2026-10-07T12:00:00Z"),
    message("m8", "2026-10-08T12:00:00Z"),
  ];

  const ON_THE_SECOND = "/chat?date=2026-10-02";

  it("goes into the address with the rest of its query, and the window follows it", async () => {
    const { history, load } = await chatWindow({ held: NEAR_THE_SECOND });

    history.pickDate("2026-10-02");
    await flushPromises();

    expect(address()).toBe("/chat?page=2&date=2026-10-02");
    expect(load.date).toHaveBeenCalledWith("2026-10-02");
    expect(history.selectedDate.value).toBe("2026-10-02");
    // A new entry of the history: Back returns to the address before the pick.
    router.back();
    await flushPromises();
    expect(address()).toBe("/chat");
  });

  it("is no move at all when nothing is picked", async () => {
    const { history } = await chatWindow({ at: ON_THE_SECOND });

    history.pickDate("");
    await flushPromises();

    expect(moves).toHaveLength(0);
    expect(address()).toBe(ON_THE_SECOND);
  });

  it("stays picked over the window near it, though no message is on it", async () => {
    const { history, feed, load } = await chatWindow({
      at: ON_THE_SECOND,
      held: NEAR_THE_SECOND,
      hasMoreAfter: true,
    });

    await history.loadDate("2026-10-02");
    await flushPromises();

    expect(history.selectedDate.value).toBe("2026-10-02");
    expect(history.notice.value).toBeNull();
    expect(moves).toHaveLength(0);
    expect(address()).toBe(ON_THE_SECOND);
    expect(load.latest).not.toHaveBeenCalled();
    // The top of the window, m1 before the day, and not m6 after it.
    expect(feed.scrollToMessage).toHaveBeenCalledWith("m1", "start");
  });

  it("opens at the first message of the day where the day has one", async () => {
    const { history, feed } = await chatWindow({
      at: "/chat?date=2026-10-07",
      held: NEAR_THE_SECOND,
      hasMoreAfter: true,
    });

    await history.loadDate("2026-10-07");
    await nextTick();

    expect(history.selectedDate.value).toBe("2026-10-07");
    expect(feed.scrollToMessage).toHaveBeenCalledWith("m7", "start");
  });

  it("is dropped, with a line naming it, where the store found nothing near it", async () => {
    // The store's own fallback: the latest messages are in already.
    const { history, load } = await chatWindow({
      at: ON_THE_SECOND,
      held: NEAR_THE_SECOND,
      landed: false,
      hasMoreAfter: true,
    });

    await history.loadDate("2026-10-02");
    await flushPromises();

    expect(history.selectedDate.value).toBe("");
    expect(address()).toBe("/chat?page=2");
    expect(history.notice.value).toBe(
      "За 02.10.2026 сообщений нет, показаны последние сообщения",
    );
    expect(load.latest).not.toHaveBeenCalled();
    // The dropped day takes its own place: Back does not bring it back to load again.
    const loads = load.date.mock.calls.length;
    router.back();
    await flushPromises();
    expect(address()).toBe("/chat?page=2");
    expect(load.date).toHaveBeenCalledTimes(loads);
  });

  it("is dropped without a line where the request failed", async () => {
    const { history } = await chatWindow({
      at: ON_THE_SECOND,
      landed: false,
      failed: true,
    });

    await history.loadDate("2026-10-02");

    expect(history.selectedDate.value).toBe("");
    expect(history.notice.value).toBeNull();
  });

  it("is dropped when the reader goes back to the latest messages", async () => {
    const { history } = await chatWindow({
      at: ON_THE_SECOND,
      held: NEAR_THE_SECOND,
      hasMoreAfter: true,
    });
    await history.loadDate("2026-10-02");

    await history.jumpToLatest();

    expect(history.selectedDate.value).toBe("");
  });

  it("names no day that does not parse", () => {
    expect(emptyDayNotice("вчера")).toBeNull();
  });
});

describe("a message a link or a search lands on", () => {
  it("lands on it with the sentinels asleep until the landing has settled", async () => {
    // setImmediate stays real: flushPromises waits on it for the navigation.
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    const found = [
      message("m1", "2026-10-01T00:00:00Z"),
      message("hit", "2026-10-01T01:00:00Z"),
    ];
    const { history, feed, load } = await chatWindow({
      at: "/chat?date=2026-10-02",
      found: () => found,
    });

    await history.landOnMessage("hit");
    await flushPromises();

    expect(address()).toBe("/chat?page=2");
    expect(history.selectedDate.value).toBe("");
    expect(feed.suspendInfiniteScroll).toHaveBeenCalled();
    expect(load.message).toHaveBeenCalledWith("hit");
    expect(feed.setupInfiniteScroll).toHaveBeenCalled();
    expect(feed.resumeInfiniteScroll).not.toHaveBeenCalled();
    vi.advanceTimersByTime(LANDING_SCROLL_MS);
    expect(feed.resumeInfiniteScroll).toHaveBeenCalledOnce();
    expect(load.latest).not.toHaveBeenCalled();
    expect(history.notice.value).toBeNull();
    // The day left takes its own place: Back does not bring it back.
    router.back();
    await flushPromises();
    expect(address()).toBe("/chat?page=2");
  });

  it("shows the latest messages under a line when the message is gone", async () => {
    const { history, feed, load } = await chatWindow({ found: () => [] });

    await history.landOnMessage("gone");
    await nextTick();

    expect(load.latest).toHaveBeenCalledOnce();
    expect(feed.scrollToBottom).toHaveBeenCalled();
    expect(history.notice.value).toBe(MESSAGE_GONE_NOTICE);
  });

  it("leaves the window on screen and says nothing when the request failed", async () => {
    const { history, feed, load } = await chatWindow({ failed: true });

    await history.landOnMessage("lost");
    await nextTick();

    expect(load.latest).not.toHaveBeenCalled();
    expect(feed.resumeInfiniteScroll).toHaveBeenCalled();
    expect(history.notice.value).toBeNull();
  });

  it("fetches the latest messages, without a line, when it failed over an empty feed", async () => {
    const { history, load } = await chatWindow({ failed: true, held: [] });

    await history.landOnMessage("lost");

    expect(load.latest).toHaveBeenCalledOnce();
    expect(history.notice.value).toBeNull();
  });
});

describe("the way back to the latest messages", () => {
  it("takes the day off the address, and the window loads the tail", async () => {
    const { history, load } = await chatWindow({
      at: "/chat?date=2026-10-02&page=2",
      hasMoreAfter: true,
    });

    await history.jumpToLatest();
    await flushPromises();

    expect(address()).toBe("/chat?page=2");
    expect(load.latest).toHaveBeenCalledOnce();
    // The day takes its own place: Back does not bring it back.
    router.back();
    await flushPromises();
    expect(address()).toBe("/chat?page=2");
  });

  it("only scrolls down where the feed holds the tail already", async () => {
    const { history, feed, load } = await chatWindow({ hasMoreAfter: false });

    await history.jumpToLatest();
    await nextTick();

    expect(load.latest).not.toHaveBeenCalled();
    expect(feed.scrollToBottom).toHaveBeenCalled();
  });

  it("loads the tail itself where the window ends short of it, and clears the line", async () => {
    const { history, load } = await chatWindow({
      hasMoreAfter: true,
      found: () => [],
    });
    await history.landOnMessage("gone");
    expect(history.notice.value).toBe(MESSAGE_GONE_NOTICE);
    load.latest.mockClear();

    await history.jumpToLatest();

    expect(load.latest).toHaveBeenCalledOnce();
    expect(history.notice.value).toBeNull();
  });
});

describe("an archive window that has caught up with the present", () => {
  async function onLastPageOf(date: string, atBottom: boolean) {
    const opened = await chatWindow({
      at: `/chat?date=${date}`,
      hasMoreAfter: true,
      atBottom,
    });
    await opened.history.loadDate(date);
    return opened;
  }

  it("goes live without a reload once its last page is in and the reader is at the bottom", async () => {
    const { history, load, state } = await onLastPageOf("2026-10-02", true);

    state.hasMoreAfter = false;
    await nextTick();
    await nextTick();
    // The address it moved reaches its watcher once the navigation lands.
    await flushPromises();

    expect(history.selectedDate.value).toBe("");
    expect(router.currentRoute.value.query).toEqual({ page: "2" });
    expect(load.latest).not.toHaveBeenCalled();
  });

  it("stays on its day while the reader is above the bottom", async () => {
    const { history, state } = await onLastPageOf("2026-10-02", false);

    state.hasMoreAfter = false;
    await nextTick();
    await nextTick();
    await flushPromises();

    expect(history.selectedDate.value).toBe("2026-10-02");
    expect(moves).toHaveLength(0);
  });

  it("goes live when asked from a scroll that reached the bottom", async () => {
    const { history, state } = await onLastPageOf("2026-10-02", false);
    state.hasMoreAfter = false;
    await nextTick();

    state.atBottom = true;
    history.followToLive();
    await nextTick();

    expect(history.selectedDate.value).toBe("");
  });

  it("stays on its day while newer pages are still there to fetch", async () => {
    const { history } = await onLastPageOf("2026-10-02", true);

    history.followToLive();
    await nextTick();

    expect(history.selectedDate.value).toBe("2026-10-02");
  });
});

describe("the day of the address moving under the same chat", () => {
  it("loads the day the address names, and the tail once it names none", async () => {
    const { history, load } = await chatWindow({ hasMoreAfter: true });

    await router.push("/chat?date=2026-10-03");
    await flushPromises();
    expect(load.date).toHaveBeenCalledWith("2026-10-03");

    await router.push("/chat");
    await flushPromises();
    expect(load.latest).toHaveBeenCalledOnce();
    expect(history.selectedDate.value).toBe("");
  });
});
