/**
 * @vitest-environment node
 */

/**
 * Which part of a chat's history the feed shows: a day picked in the
 * calendar, a message a link or a search lands on, the way back to the latest
 * messages and the catch-up that makes an archive window live again. The
 * address is a stand-in that moves when the window moves it, so a change of
 * ?date reaches the window the way a navigation does.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { effectScope, nextTick, reactive, ref, type EffectScope } from "vue";
import type { Message } from "@/shared/api/models/common";
import { LANDING_SCROLL_MS } from "@/shared/lib/composables/useAnchoredInfiniteScroll";

type Location = { name: string; query?: Record<string, string> };

const nav = vi.hoisted(() => ({
  route: { query: {} as Record<string, string> },
  router: {
    push: vi.fn<(to: Location) => void>(),
    replace: vi.fn<(to: Location) => void>(),
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => nav.route,
  useRouter: () => nav.router,
}));

import {
  MESSAGE_GONE_NOTICE,
  emptyDayNotice,
  useChatHistoryWindow,
} from "./useChatHistoryWindow";

const message = (id: string, createdUtc: string) =>
  ({ id, createdUtc }) as Message;

/** A chat whose store answers as the test sets it, behind a feed that records. */
function chatWindow(
  over: {
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
  const history = scope.run(() =>
    useChatHistoryWindow({
      feed: ref(feed),
      location: () => ({ name: "global-chat", query: { page: "2" } }),
      messages: () => state.held,
      loading: () => state.loading,
      failed: () => state.failed,
      hasMoreAfter: () => state.hasMoreAfter,
      landedOnRequestedDate: () => over.landed ?? true,
      load,
    }),
  )!;
  return { history, feed, load, state };
}

let scope: EffectScope;

beforeEach(() => {
  vi.clearAllMocks();
  scope = effectScope();
  nav.route = reactive({ query: {} });
  const go = (to: Location) => {
    nav.route.query = { ...(to.query ?? {}) };
  };
  nav.router.push.mockImplementation(go);
  nav.router.replace.mockImplementation(go);
});

afterEach(() => {
  scope.stop();
  vi.useRealTimers();
});

/**
 * The day picked in "Перейти к дате" is the day the calendar shows picked when
 * it opens again, as it was before 5 October 2026. The server answers a day
 * with the window of messages near it, and the page kept the day for every
 * such answer: the day selected in the calendar, the feed at the first message
 * of the day, or at the top of the window where the day has none.
 * From 5 October a day with no message of its own was dropped as soon as it
 * was picked, and the calendar opened on nothing picked. The day goes only
 * where the store found no window near it and fell back to the latest
 * messages, or where the reader goes back to them.
 */
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

  beforeEach(() => {
    nav.route.query = { date: "2026-10-02" };
  });

  it("goes into the address with the rest of its query, and the window follows it", async () => {
    nav.route.query = {};
    const { history, load } = chatWindow({ held: NEAR_THE_SECOND });

    history.pickDate("2026-10-02");
    await nextTick();

    expect(nav.router.push).toHaveBeenCalledWith({
      name: "global-chat",
      query: { page: "2", date: "2026-10-02" },
    });
    expect(load.date).toHaveBeenCalledWith("2026-10-02");
    expect(history.selectedDate.value).toBe("2026-10-02");
  });

  it("is no move at all when nothing is picked", () => {
    const { history } = chatWindow();

    history.pickDate("");

    expect(nav.router.push).not.toHaveBeenCalled();
  });

  it("stays picked over the window near it, though no message is on it", async () => {
    const { history, feed, load } = chatWindow({
      held: NEAR_THE_SECOND,
      hasMoreAfter: true,
    });

    await history.loadDate("2026-10-02");
    await nextTick();

    expect(history.selectedDate.value).toBe("2026-10-02");
    expect(history.notice.value).toBeNull();
    expect(nav.router.replace).not.toHaveBeenCalled();
    expect(load.latest).not.toHaveBeenCalled();
    // The feed opens at the top of the window, as it did before 5 October:
    // m1, before the day, and not m6, the first message after it.
    expect(feed.scrollToMessage).toHaveBeenCalledWith("m1", "start");
  });

  it("opens at the first message of the day where the day has one", async () => {
    nav.route.query = { date: "2026-10-07" };
    const { history, feed } = chatWindow({
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
    const { history, load } = chatWindow({
      held: NEAR_THE_SECOND,
      landed: false,
      hasMoreAfter: true,
    });

    await history.loadDate("2026-10-02");

    expect(history.selectedDate.value).toBe("");
    expect(nav.router.replace).toHaveBeenCalledWith({
      name: "global-chat",
      query: { page: "2" },
    });
    expect(history.notice.value).toBe(
      "За 02.10.2026 сообщений нет, показаны последние сообщения",
    );
    expect(load.latest).not.toHaveBeenCalled();
  });

  it("is dropped without a line where the request failed", async () => {
    const { history } = chatWindow({ landed: false, failed: true });

    await history.loadDate("2026-10-02");

    expect(history.selectedDate.value).toBe("");
    expect(history.notice.value).toBeNull();
  });

  it("is dropped when the reader goes back to the latest messages", async () => {
    const { history } = chatWindow({
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
    vi.useFakeTimers();
    nav.route.query = { date: "2026-10-02" };
    const found = [
      message("m1", "2026-10-01T00:00:00Z"),
      message("hit", "2026-10-01T01:00:00Z"),
    ];
    const { history, feed, load } = chatWindow({ found: () => found });

    await history.landOnMessage("hit");
    await nextTick();

    expect(nav.router.replace).toHaveBeenCalledWith({
      name: "global-chat",
      query: { page: "2" },
    });
    expect(history.selectedDate.value).toBe("");
    expect(feed.suspendInfiniteScroll).toHaveBeenCalled();
    expect(load.message).toHaveBeenCalledWith("hit");
    expect(feed.setupInfiniteScroll).toHaveBeenCalled();
    expect(feed.resumeInfiniteScroll).not.toHaveBeenCalled();
    vi.advanceTimersByTime(LANDING_SCROLL_MS);
    expect(feed.resumeInfiniteScroll).toHaveBeenCalledOnce();
    expect(load.latest).not.toHaveBeenCalled();
    expect(history.notice.value).toBeNull();
  });

  it("shows the latest messages under a line when the message is gone", async () => {
    const { history, feed, load } = chatWindow({ found: () => [] });

    await history.landOnMessage("gone");
    await nextTick();

    expect(load.latest).toHaveBeenCalledOnce();
    expect(feed.scrollToBottom).toHaveBeenCalled();
    expect(history.notice.value).toBe(MESSAGE_GONE_NOTICE);
  });

  it("leaves the window on screen and says nothing when the request failed", async () => {
    const { history, feed, load } = chatWindow({ failed: true });

    await history.landOnMessage("lost");
    await nextTick();

    expect(load.latest).not.toHaveBeenCalled();
    expect(feed.resumeInfiniteScroll).toHaveBeenCalled();
    expect(history.notice.value).toBeNull();
  });

  it("fetches the latest messages, without a line, when it failed over an empty feed", async () => {
    const { history, load } = chatWindow({ failed: true, held: [] });

    await history.landOnMessage("lost");

    expect(load.latest).toHaveBeenCalledOnce();
    expect(history.notice.value).toBeNull();
  });
});

describe("the way back to the latest messages", () => {
  it("takes the day off the address, and the window loads the tail", async () => {
    nav.route.query = { date: "2026-10-02", page: "2" };
    const { history, load } = chatWindow({ hasMoreAfter: true });

    await history.jumpToLatest();
    await nextTick();

    expect(nav.router.replace).toHaveBeenCalledWith({
      name: "global-chat",
      query: { page: "2" },
    });
    expect(load.latest).toHaveBeenCalledOnce();
  });

  it("only scrolls down where the feed holds the tail already", async () => {
    const { history, feed, load } = chatWindow({ hasMoreAfter: false });

    await history.jumpToLatest();
    await nextTick();

    expect(load.latest).not.toHaveBeenCalled();
    expect(feed.scrollToBottom).toHaveBeenCalled();
  });

  it("loads the tail itself where the window ends short of it, and clears the line", async () => {
    const { history, load } = chatWindow({
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
    nav.route.query = { date };
    const opened = chatWindow({ hasMoreAfter: true, atBottom });
    await opened.history.loadDate(date);
    return opened;
  }

  it("goes live without a reload once its last page is in and the reader is at the bottom", async () => {
    const { history, load, state } = await onLastPageOf("2026-10-02", true);

    state.hasMoreAfter = false;
    await nextTick();
    await nextTick();
    // The address it moved reaches its watcher a tick later.
    await nextTick();

    expect(history.selectedDate.value).toBe("");
    expect(nav.route.query).toEqual({ page: "2" });
    expect(load.latest).not.toHaveBeenCalled();
  });

  it("stays on its day while the reader is above the bottom", async () => {
    const { history, state } = await onLastPageOf("2026-10-02", false);

    state.hasMoreAfter = false;
    await nextTick();
    await nextTick();

    expect(history.selectedDate.value).toBe("2026-10-02");
    expect(nav.router.replace).not.toHaveBeenCalled();
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
    const { history, load } = chatWindow({ hasMoreAfter: true });

    nav.route.query = { date: "2026-10-03" };
    await nextTick();
    expect(load.date).toHaveBeenCalledWith("2026-10-03");

    nav.route.query = {};
    await nextTick();
    expect(load.latest).toHaveBeenCalledOnce();
    expect(history.selectedDate.value).toBe("");
  });
});
