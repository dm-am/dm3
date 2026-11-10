/** Which part of a chat's history the feed shows; ?date in the address is its one source of truth. */
import { computed, nextTick, ref, watch, type Ref } from "vue";
import {
  useRoute,
  useRouter,
  type RouteLocationNormalizedLoaded,
  type Router,
} from "vue-router";
import type { Message } from "@/shared/api/models/common";
import {
  DATE_FORMAT,
  formatSiteTime,
  siteTime,
} from "@/shared/lib/utils/datetime";
import { LANDING_SCROLL_MS } from "@/shared/lib/composables/useAnchoredInfiniteScroll";
import type { MessageAlign } from "./useFeedPosition";

/** What the window asks of the feed: the handle ChatFeed exposes. */
interface ChatHistoryFeed {
  scrollToBottom(): void;
  scrollToMessage(id: string, align?: MessageAlign): void;
  atBottom(): boolean;
  setupInfiniteScroll(): void;
  suspendInfiniteScroll(): void;
  resumeInfiniteScroll(): void;
}

export interface ChatHistoryWindowOptions {
  feed: Ref<ChatHistoryFeed | null>;
  /** The chat's address: ?date goes in and out of it, beside the keys it already holds. */
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
  /** The store found a window near the day; false when it fell back to the latest. */
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
  // A ?date typed by hand that does not parse is not an empty day.
  if (!day.isValid()) return null;
  return `За ${day.format(DATE_FORMAT)} сообщений нет, показаны последние сообщения`;
}

/** What a move of the window reads and writes. */
interface HistoryWindow {
  options: ChatHistoryWindowOptions;
  route: RouteLocationNormalizedLoaded;
  router: Router;
  /** The day the window starts at, YYYY-MM-DD, or "" while live. */
  selectedDate: Ref<string>;
  /** Why the feed shows a window the reader did not ask for. */
  notice: Ref<string | null>;
  /** Set before the page's own move drops ?date, so the watcher does not load twice. */
  suppressNextDateWatch: boolean;
}

/** The day the address names, if any. */
function dateInRoute(w: HistoryWindow): string | null {
  const date = w.route.query.date;
  return typeof date === "string" && date ? date : null;
}

/** The chat's address at a day, or at none: live. */
function withDate(w: HistoryWindow, date: string | null) {
  const { query, ...location } = w.options.location();
  return { ...location, query: date ? { ...query, date } : { ...query } };
}

function scrollToBottom(w: HistoryWindow) {
  nextTick(() => w.options.feed.value?.scrollToBottom());
}

/** Hands the observers a replaced window's new sentinels, awake once a landing scroll settles. */
function observeWindow(w: HistoryWindow, landing: boolean) {
  const feed = w.options.feed;
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
async function loadLatest(w: HistoryWindow) {
  await w.options.load.latest();
  scrollToBottom(w);
  observeWindow(w, false);
}

/** The archive is left: no day selected, none in the address. */
function leaveDate(w: HistoryWindow) {
  w.selectedDate.value = "";
  if (w.route.query.date) {
    w.suppressNextDateWatch = true;
    w.router.replace(withDate(w, null));
  }
}

/** A day was picked in the calendar: the address takes it, the watcher loads. */
function pickDate(w: HistoryWindow, date: string) {
  if (!date) return;
  w.router.push(withDate(w, date));
}

/** Lands on a day's window; the day is given up only where the store fell back to the latest. */
async function loadDate(w: HistoryWindow, date: string) {
  const { options } = w;
  w.notice.value = null;
  w.selectedDate.value = date;
  options.feed.value?.suspendInfiniteScroll();
  await options.load.date(date);

  if (options.landedOnRequestedDate()) {
    // The window starts before the day, so its first message is found on the feed's clock.
    const held = options.messages();
    const target =
      held.find((m) => formatSiteTime(m.createdUtc, "YYYY-MM-DD") === date) ??
      held[0];
    if (target) {
      nextTick(() => options.feed.value?.scrollToMessage(target.id, "start"));
    }
    observeWindow(w, true);
    return;
  }

  leaveDate(w);
  observeWindow(w, false);
  // A failed request is not an empty day: it was announced, and there is nothing to explain.
  if (options.failed() || !options.messages().length) return;
  scrollToBottom(w);
  w.notice.value = emptyDayNotice(date);
}

/** Lands on a message; a missing one leaves the latest messages under a line saying so. */
async function landOnMessage(w: HistoryWindow, id: string) {
  const { options } = w;
  w.notice.value = null;
  leaveDate(w);
  options.feed.value?.suspendInfiniteScroll();
  await options.load.message(id);
  const found = options.messages().some((m) => m.id === id);
  if (found) {
    observeWindow(w, true);
    return;
  }
  const answered = !options.failed();
  if (!answered && options.messages().length) {
    observeWindow(w, false);
    return;
  }
  await loadLatest(w);
  if (answered && !options.failed() && options.messages().length) {
    w.notice.value = MESSAGE_GONE_NOTICE;
  }
}

/** "К последним сообщениям": clearing ?date lets the watcher load; without one, here. */
async function jumpToLatest(w: HistoryWindow) {
  w.selectedDate.value = "";
  if (dateInRoute(w)) {
    w.router.replace(withDate(w, null));
    return;
  }
  if (!w.options.hasMoreAfter()) {
    scrollToBottom(w);
    return;
  }
  w.notice.value = null;
  await loadLatest(w);
}

/** Back to live at the real bottom of an archive's last page, measured when asked. */
function followToLive(w: HistoryWindow) {
  const { options, selectedDate } = w;
  if (!selectedDate.value || options.hasMoreAfter() || options.loading()) {
    return;
  }
  nextTick(() => {
    if (!selectedDate.value || options.hasMoreAfter()) return;
    if (!(options.feed.value?.atBottom() ?? true)) return;
    leaveDate(w);
  });
}

/** Forgets the window's day and its line: another chat, or a plain visit. */
function forget(w: HistoryWindow) {
  w.notice.value = null;
  w.selectedDate.value = "";
}

export function useChatHistoryWindow(options: ChatHistoryWindowOptions) {
  const w: HistoryWindow = {
    options,
    route: useRoute(),
    router: useRouter(),
    selectedDate: ref(""),
    notice: ref<string | null>(null),
    suppressNextDateWatch: false,
  };

  /** Today on the site's clock, the latest day the calendar offers. */
  const today = siteTime().format("YYYY-MM-DD");

  /** The search row stands unless the server called the chat empty. */
  const hasHistory = computed(
    () =>
      options.loading() || options.failed() || options.messages().length > 0,
  );

  watch(
    () => options.hasMoreAfter(),
    () => followToLive(w),
  );

  // Later moves of ?date under the same chat; the page loads the first window and a new chat.
  watch(
    () => [options.chat?.(), w.route.query.date] as const,
    ([chat, date], [previousChat, previousDate]) => {
      if (chat !== previousChat || date === previousDate) return;
      if (w.suppressNextDateWatch) {
        w.suppressNextDateWatch = false;
        return;
      }
      const day = dateInRoute(w);
      if (day) {
        loadDate(w, day);
        return;
      }
      forget(w);
      loadLatest(w);
    },
  );

  return {
    selectedDate: w.selectedDate,
    notice: w.notice,
    today,
    hasHistory,
    dateInRoute: () => dateInRoute(w),
    pickDate: (date: string) => pickDate(w, date),
    loadDate: (date: string) => loadDate(w, date),
    landOnMessage: (id: string) => landOnMessage(w, id),
    jumpToLatest: () => jumpToLatest(w),
    followToLive: () => followToLive(w),
    forget: () => forget(w),
  };
}
