/**
 * useChatHistoryWindow: which part of a chat's history the feed shows, the
 * global chat's and a correspondence's alike.
 *
 * The feed lives in one of two modes; the ?date query of the chat's address is
 * the single source of truth for which one:
 *
 * LIVE (no ?date, nothing newer to fetch): the loaded window ends at the
 *   newest message, and new messages are appended to it by the page.
 *
 * ARCHIVE (?date=YYYY-MM-DD): the window starts at the picked day.
 *   - While newer pages are there to fetch, the bottom sentinel keeps paging
 *     toward "now", and the button back to the latest messages jumps straight
 *     to them (clears ?date, reloads the tail).
 *   - Once the last page is in, reaching its bottom means the reader has
 *     caught up with the present: ?date is taken off the address without a
 *     reload (the window already IS the tail) and the feed is live again.
 *
 * LIVE -> ARCHIVE only through the calendar or the address; ARCHIVE -> LIVE
 * through the button, the catch-up above, or ?date leaving the address (back
 * and forward included).
 *
 * Two ways onto a chat ask for one place in its history, a link to a message
 * and a day, and either can come back with nothing: the message is gone, or
 * the store found no window near the day. A day with no message of its own is
 * not that case: the server answers it with the messages near it, and the day
 * stays picked (loadDate). The feed then shows the latest messages, which is
 * the right fallback and a silent one: the calendar shows no date, the linked
 * message is nowhere, and the feed looks exactly like a plain visit. `notice`
 * says so in words, in the line under the strip of the frame, for as long as
 * the window it explains stands: until the reader asks for another place (a
 * day, a search hit, a link) or leaves. Not a toast: the reader is looking at
 * the feed, and the line is about the feed.
 *
 * Both pages drive their feed through this one. What stays the page's is its
 * store and the chat its address names (`chat`): a change of the chat is the
 * page's to load whole, its day included.
 */
import { computed, nextTick, ref, watch, type Ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { Message } from "@/shared/api/models/common";
import {
  DATE_FORMAT,
  formatSiteTime,
  siteTime,
} from "@/shared/lib/utils/datetime";
import { LANDING_SCROLL_MS } from "@/shared/lib/composables/useAnchoredInfiniteScroll";

/** What the window asks of the feed: the handle ChatFeed exposes. */
export interface ChatHistoryFeed {
  scrollToBottom(): void;
  scrollToMessage(id: string, align?: "center" | "start"): void;
  atBottom(): boolean;
  setupInfiniteScroll(): void;
  suspendInfiniteScroll(): void;
  resumeInfiniteScroll(): void;
}

export interface ChatHistoryWindowOptions {
  feed: Ref<ChatHistoryFeed | null>;
  /**
   * The chat's address: where ?date goes in and out. Its query holds what the
   * address keeps beside the window's own keys (the page of a list standing
   * next to the feed), and every move of the window carries it.
   */
  location: () => {
    name: string;
    params?: Record<string, string>;
    query?: Record<string, string>;
  };
  /** The chat the address names; a change of it is the page's to load. */
  chat?: () => unknown;
  /** The loaded window, oldest first. */
  messages: () => readonly Message[];
  /** The window is on its way. */
  loading: () => boolean;
  /** The window did not load. */
  failed: () => boolean;
  /** Newer pages are there to fetch: the window does not end at the newest. */
  hasMoreAfter: () => boolean;
  /**
   * The store took a window near the day it was asked for. False means it
   * fell back to the latest messages itself (or the request failed).
   */
  landedOnRequestedDate: () => boolean;
  /** The store's loads of a window. */
  load: {
    latest: () => Promise<unknown>;
    date: (date: string) => Promise<unknown>;
    message: (id: string) => Promise<unknown>;
  };
}

/** The line over a link to a message that is not there. */
export const MESSAGE_GONE_NOTICE =
  "Сообщение по ссылке не найдено или удалено, показаны последние сообщения";

/** The line over a day with no message on it. */
export function emptyDayNotice(date: string): string | null {
  const day = siteTime(date);
  // A day that does not parse (?date= typed by hand) is not a day the chat
  // could be empty on; the tail stands without a line.
  if (!day.isValid()) return null;
  return `За ${day.format(DATE_FORMAT)} сообщений нет, показаны последние сообщения`;
}

export function useChatHistoryWindow(options: ChatHistoryWindowOptions) {
  const route = useRoute();
  const router = useRouter();

  /** The day the window starts at, YYYY-MM-DD, or "" while live. */
  const selectedDate = ref("");

  /** Why the feed shows a window the reader did not ask for. */
  const notice = ref<string | null>(null);

  /** Today on the site's clock, the latest day the calendar offers. */
  const today = computed(() => siteTime().format("YYYY-MM-DD"));

  /**
   * "Строки поиска нет, пока сообщений нет": the row above the frame stands
   * while the history is on its way, failed or holds something, and not over
   * a chat the server called empty, where there is nothing to search and no
   * day to go to.
   */
  const hasHistory = computed(
    () =>
      options.loading() || options.failed() || options.messages().length > 0,
  );

  // Set right before the address loses its ?date on the page's own move: the
  // window on screen is already the one the address will name, and the
  // watcher below must not load it a second time.
  let suppressNextDateWatch = false;

  /** The day the address names, if any. */
  function dateInRoute(): string | null {
    const date = route.query.date;
    return typeof date === "string" && date ? date : null;
  }

  /** The chat's address at a day, or at none: live. */
  function withDate(date: string | null) {
    const { query, ...location } = options.location();
    return { ...location, query: date ? { ...query, date } : { ...query } };
  }

  function scrollToBottom() {
    nextTick(() => options.feed.value?.scrollToBottom());
  }

  /**
   * After a window was replaced. Its sentinels are elements drawn anew (the
   * skeleton stood in their place while it loaded, or one appeared that was
   * not there), and the observers watch the elements they were given, so they
   * are given the new ones. A landing scroll crosses the whole window, and the
   * sentinels it flies past would read it as a request for another page: they
   * wake once it has settled.
   */
  function observeWindow(landing: boolean) {
    const feed = options.feed;
    nextTick(() => {
      feed.value?.setupInfiniteScroll();
      if (!landing) {
        feed.value?.resumeInfiniteScroll();
        return;
      }
      setTimeout(() => feed.value?.resumeInfiniteScroll(), LANDING_SCROLL_MS);
    });
  }

  /** The latest messages, with the reader at their end. */
  async function loadLatest() {
    await options.load.latest();
    scrollToBottom();
    observeWindow(false);
  }

  /** The archive is left: no day selected, none in the address. */
  function leaveDate() {
    selectedDate.value = "";
    if (route.query.date) {
      suppressNextDateWatch = true;
      router.replace(withDate(null));
    }
  }

  /** A day was picked in the calendar: the address takes it, the watcher loads. */
  function pickDate(date: string) {
    if (!date) return;
    router.push(withDate(date));
  }

  /**
   * Loads the window of a day and lands on it. The day stays picked, in the
   * address and in the calendar, for every window the store found near it, as
   * it did before 5 October 2026. From 5 October it was dropped for a day with
   * no message of its own, and the calendar opened on nothing picked the next
   * time.
   *
   * The server answers a day with the window around the first message from
   * its midnight on (around the last one before it, where nothing comes
   * after), about half of the window before that message. The feed opens at
   * the first message of the day where the window holds one, and at the top
   * of the window otherwise, as it did before 5 October. For a day with no
   * message of its own that top is a message from before the day, wherever
   * the chat has one.
   *
   * Only where the store found nothing near the day and fell back to the
   * latest messages, the address gives the day up and a line names it:
   * presenting the latest window while the calendar still shows the picked
   * day would look exactly like landing on the wrong one.
   */
  async function loadDate(date: string) {
    notice.value = null;
    selectedDate.value = date;
    options.feed.value?.suspendInfiniteScroll();
    await options.load.date(date);

    if (options.landedOnRequestedDate()) {
      // The window starts before the day wherever the chat has messages
      // before it, so the day's first message is found here, on the clock
      // the feed dates its messages by.
      const held = options.messages();
      const target =
        held.find((m) => formatSiteTime(m.createdUtc, "YYYY-MM-DD") === date) ??
        held[0];
      if (target) {
        nextTick(() => options.feed.value?.scrollToMessage(target.id, "start"));
      }
      observeWindow(true);
      return;
    }

    leaveDate();
    observeWindow(false);
    // A failed request is not an empty day. The interceptor has announced a
    // 5xx or a dead network, the red box stands where an empty feed would be,
    // and the window on screen is whatever was there before the request:
    // nothing to explain and nothing to scroll.
    if (options.failed() || !options.messages().length) return;
    scrollToBottom();
    notice.value = emptyDayNotice(date);
  }

  /**
   * Lands on a message: a search hit, or a link to it on the way in. Leaves
   * any day for the live feed's window around the message. The landing scroll
   * crosses the whole loaded window, so the sentinels are off while it lasts,
   * or they would read it as a request for another page.
   *
   * A message the server does not have (removed, or never there) leaves the
   * latest messages under a line saying so. A request that failed was
   * announced already (the interceptor, the red box), and a line calling that
   * "not found" would be a second carrier saying a different thing; it gets
   * the latest messages only when the feed has nothing else to show.
   */
  async function landOnMessage(id: string) {
    notice.value = null;
    leaveDate();
    options.feed.value?.suspendInfiniteScroll();
    await options.load.message(id);
    const found = options.messages().some((m) => m.id === id);
    if (found) {
      observeWindow(true);
      return;
    }
    const answered = !options.failed();
    if (!answered && options.messages().length) {
      observeWindow(false);
      return;
    }
    await loadLatest();
    if (answered && !options.failed() && options.messages().length) {
      notice.value = MESSAGE_GONE_NOTICE;
    }
  }

  /**
   * "К последним сообщениям". Clearing ?date lets the watcher load the latest;
   * with no day in the address they load here. In LIVE mode the tail is held
   * already, and there is only somewhere to scroll.
   */
  async function jumpToLatest() {
    selectedDate.value = "";
    if (dateInRoute()) {
      router.replace(withDate(null));
      return;
    }
    if (!options.hasMoreAfter()) {
      scrollToBottom();
      return;
    }
    notice.value = null;
    await loadLatest();
  }

  /**
   * ARCHIVE -> LIVE: the reader stands at the real bottom of the last page of
   * an archive window. Asked on every scroll of the feed and when the last
   * page arrives. Where the reader stands is measured when asked: appending a
   * page grows the list without a scroll event, so a remembered answer goes
   * stale exactly when it matters.
   */
  function followToLive() {
    if (!selectedDate.value || options.hasMoreAfter() || options.loading()) {
      return;
    }
    nextTick(() => {
      if (!selectedDate.value || options.hasMoreAfter()) return;
      if (!(options.feed.value?.atBottom() ?? true)) return;
      leaveDate();
    });
  }

  watch(() => options.hasMoreAfter(), followToLive);

  /** Forgets the window's day and its line: another chat, or a plain visit. */
  function forget() {
    notice.value = null;
    selectedDate.value = "";
  }

  // ?date moving under the same chat: the calendar, the button, back and
  // forward. The page loads the first window itself, and a change of the chat
  // with it; this follows only the later moves of the day.
  watch(
    () => [options.chat?.(), route.query.date] as const,
    ([chat, date], [previousChat, previousDate]) => {
      if (chat !== previousChat || date === previousDate) return;
      if (suppressNextDateWatch) {
        suppressNextDateWatch = false;
        return;
      }
      if (typeof date === "string" && date) {
        loadDate(date);
        return;
      }
      forget();
      loadLatest();
    },
  );

  return {
    selectedDate,
    notice,
    today,
    hasHistory,
    dateInRoute,
    pickDate,
    loadDate,
    landOnMessage,
    jumpToLatest,
    followToLive,
    forget,
  };
}
