/**
 * @vitest-environment jsdom
 */

/**
 * The notifications page read its list once, on mount, and then stood still:
 * the bell in the header counted arrivals the whole time while the page under
 * it kept showing the state of the moment it was opened, and marking a row read
 * left the badge counting a notification that was no longer there.
 *
 * The page now follows the count the notification store owns — the socket feeds
 * the store, and per the realtime contract the truth is read back over REST —
 * and tells the store to re-read that count after it marks anything read.
 *
 * What a row says is the server's: the title, the path it leads to and the
 * details come from the table the letter and the bots are written from, with
 * the date and whether the reader has read it. The page names no event and
 * builds no address of its own (owner, 12 October 2026, line 22 of the sheet:
 * the list called 35 of 59 events "Уведомление" and led nowhere from them).
 */
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { join } from "node:path";
import { notificationApi, useNotificationStore } from "@/entities/notification";
import {
  NotificationType,
  type NotificationDetail,
  type UserNotification,
} from "@/shared/api/models/notifications";
import { useToast } from "@/shared/lib/composables/useToast";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import NotificationsPage from "./NotificationsPage.vue";

/** Mirrors the page size the page asks for; a full page of strangers is the drift signal. */
const PAGE = 20;

/** The space that keeps a label with its value. */
const NBSP = "\u00A0";

const detail = (
  label: string,
  value: string,
  isMoment = false,
): NotificationDetail => ({ label, value, isMoment }) as NotificationDetail;

/** The fields of a row a test sets, as plain values. */
type RowFields = {
  eventType?: NotificationType;
  createdUtc?: string;
  isRead?: boolean;
  title?: string;
  url?: string;
  details?: NotificationDetail[];
  payload?: Record<string, unknown>;
};

/**
 * One row as the server answers it. The title is the id, so a row can be told
 * by its title; it leads somewhere and is unread unless told otherwise.
 */
const item = (id: string, extra: RowFields = {}): UserNotification =>
  ({
    id,
    eventType: NotificationType.NewMessage,
    payload: {},
    createdUtc: "2026-10-12T08:43:00Z",
    isRead: false,
    title: id,
    url: `/somewhere/${id}`,
    details: [],
    ...extra,
  }) as UserNotification;

const items = (prefix: string, count: number) =>
  Array.from({ length: count }, (_, i) => item(`${prefix}${i}`));

const answer = (resources: UserNotification[]) =>
  ({ data: { resources }, error: null }) as never;

/** A problem document with the fields the page reads. */
const refused = (status: number, title = "") =>
  ({
    data: null,
    error: { type: "about:blank", title, status, traceId: "t" },
  }) as never;

/**
 * The page mounted and its first read settled. The first answer is the page
 * given, unless a refusal is passed in its place. `unread` is the bell's
 * count when the page opens; a re-read of it answers what the server holds by
 * then, which is nothing once everything has been marked.
 */
async function render(
  firstPage: UserNotification[],
  firstAnswer: unknown = answer(firstPage),
  unread = 0,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useNotificationStore();
  // Before the mount: the page re-reads its list when the count grows.
  store.unreadCount = unread;
  const server = { unread };
  const fetchUnreadCount = vi
    .spyOn(store, "fetchUnreadCount")
    .mockImplementation(async () => {
      store.unreadCount = server.unread;
    });
  const getNotifications = vi
    .spyOn(notificationApi, "getNotifications")
    .mockResolvedValue(firstAnswer as never);
  const markAsRead = vi
    .spyOn(notificationApi, "markAsRead")
    .mockImplementation(async (id?: string) => {
      if (id === undefined) server.unread = 0;
      else server.unread = Math.max(0, server.unread - 1);
      return { data: null, error: null } as never;
    });

  const wrapper = mount(NotificationsPage, {
    // In the document, so the focus the line hands on can be read off it.
    attachTo: document.body,
    global: {
      plugins: [pinia],
      stubs: {
        // The heading the line puts the focus on.
        PageTitle: { template: "<h1><slot /></h1>" },
        RouterLink: RouterLinkStub,
      },
    },
  });
  mounted.push(wrapper);
  await flushPromises();
  return { wrapper, store, getNotifications, markAsRead, fetchUnreadCount };
}

const mounted: ReturnType<typeof mount>[] = [];

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
});

type Wrapper = ReturnType<typeof mount>;

/** What the eye reads of an element: the text a screen reader alone hears left out. */
function seen(element: Element): string {
  const copy = element.cloneNode(true) as Element;
  copy
    .querySelectorAll(".visually-hidden")
    .forEach((hidden) => hidden.remove());
  // Ordinary white space only: a no-break space is part of what is said.
  return copy.textContent!.replace(/[ \t\r\n]+/g, " ").trim();
}

/** The list as drawn, in order: each row is told by its title. */
const rows = (wrapper: Wrapper) =>
  wrapper.findAll(".notification-title").map((title) => seen(title.element));

/** The rows whose title is bold, by title. */
const unreadRows = (wrapper: Wrapper) =>
  wrapper
    .findAll(".notification-title--unread")
    .map((title) => seen(title.element));

/** The title of the row told by its title. */
const titleOf = (wrapper: Wrapper, title: string) => {
  const found = wrapper
    .findAll(".notification-title")
    .find((one) => seen(one.element) === title);
  if (!found) throw new Error(`no row titled ${title}`);
  return found;
};

/** Raise the count the way an arrival does, and let the page react. */
async function arrive(store: ReturnType<typeof useNotificationStore>, n = 1) {
  store.unreadCount += n;
  await flushPromises();
}

/** "Отметить прочитанными" of the line under the heading, pressed. */
async function markAll(wrapper: Wrapper) {
  await wrapper.get(".unread-mark").trigger("click");
  await flushPromises();
}

/** The line under the heading as a reader sees it. */
function unreadLine(wrapper: Wrapper): string | null {
  const line = wrapper.find(".unread-line");
  if (!line.exists()) return null;
  return line.element.textContent!.replace(/\s+/g, " ").trim();
}

/** Everything the site is currently saying in a floating message. */
const messages = () => useToast().toasts.value.map((t) => t.message);

describe("NotificationsPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Error toasts never auto-dismiss and deduplicate by text.
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("re-reads the newest page when the bell count grows", async () => {
    const { wrapper, store, getNotifications } = await render([
      item("old-1"),
      item("old-2"),
    ]);
    getNotifications.mockResolvedValue(
      answer([item("new-1"), item("old-1"), item("old-2")]),
    );

    await arrive(store);

    expect(getNotifications).toHaveBeenLastCalledWith(0, PAGE);
    // The arrival goes on top, and what was on screen stays exactly where it
    // was: nothing dropped, nothing doubled, no jump to the top of the list.
    expect(rows(wrapper)).toEqual(["new-1", "old-1", "old-2"]);
  });

  it("keeps the pages already loaded below the newest one", async () => {
    const held = items("held-", PAGE);
    const { wrapper, store, getNotifications } = await render(held);
    getNotifications.mockResolvedValueOnce(answer([item("tail-1")]));
    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();
    // The newest page cannot reach a row scrolled past it, so re-reading the
    // list instead of prepending would drop that row and the scroll with it.
    getNotifications.mockResolvedValue(
      answer([item("new-1"), ...held.slice(0, PAGE - 1)]),
    );

    await arrive(store);

    expect(rows(wrapper)).toEqual([
      "new-1",
      ...held.map((n) => n.id),
      "tail-1",
    ]);
  });

  it("asks for the next page without stepping over a row", async () => {
    const held = items("held-", PAGE);
    const { wrapper, store, getNotifications } = await render(held);
    // The window had already moved when this page was asked for, so it answers
    // with one row that is on screen plus nineteen new ones: deduplication has
    // absorbed that shift here, at the far end of the list.
    getNotifications.mockResolvedValueOnce(
      answer([held[PAGE - 1], ...items("mid-", PAGE - 1)]),
    );
    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();
    getNotifications.mockResolvedValue(
      answer([item("new-1"), ...held.slice(0, PAGE - 1)]),
    );
    await arrive(store);
    getNotifications.mockClear();

    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();

    // Two windows of twenty rows have been answered, so the third starts at
    // forty. Counting the arrival as a shift on top of that would step over the
    // row at that offset, and nothing would ever ask for it again.
    expect(getNotifications).toHaveBeenCalledWith(2 * PAGE, PAGE);
  });

  it("re-reads the newest page after a growth that landed during a load", async () => {
    const held = items("held-", PAGE);
    const { wrapper, store, getNotifications } = await render(held);
    let answerLoadMore: (page: unknown) => void = () => {};
    getNotifications.mockReturnValueOnce(
      new Promise((resolve) => {
        answerLoadMore = resolve;
      }) as never,
    );
    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();
    getNotifications.mockResolvedValue(
      answer([item("new-1"), ...held.slice(0, PAGE - 1)]),
    );

    // The reader's own page is in flight, so the re-read has to wait for it.
    await arrive(store);
    expect(getNotifications).toHaveBeenCalledTimes(2);

    answerLoadMore(answer([item("tail-1")]));
    await flushPromises();

    // Held back, not dropped: the arrival reaches the screen once the page it
    // waited for settles, and it is read exactly once.
    expect(getNotifications).toHaveBeenCalledTimes(3);
    expect(getNotifications).toHaveBeenLastCalledWith(0, PAGE);
    expect(rows(wrapper)).toEqual([
      "new-1",
      ...held.map((n) => n.id),
      "tail-1",
    ]);
  });

  it("leaves the list alone when the count falls", async () => {
    const { wrapper, store, getNotifications } = await render([item("old-1")]);
    store.unreadCount = 5;
    await flushPromises();
    getNotifications.mockClear();

    store.unreadCount = 1;
    await flushPromises();

    expect(getNotifications).not.toHaveBeenCalled();
    expect(rows(wrapper)).toEqual(["old-1"]);
  });

  it("replaces the list with the newest page when nothing on it is known", async () => {
    const { wrapper, store, getNotifications } = await render(
      items("held-", PAGE),
    );
    const drifted = items("gone-", PAGE);
    getNotifications.mockResolvedValue(answer(drifted));
    getNotifications.mockClear();

    await arrive(store);

    // A full page of strangers means the shown window drifted off; splicing it
    // on top would leave an unreachable hole between the two windows.
    expect(rows(wrapper)).toEqual(drifted.map((n) => n.id));
    // The page in hand is the newest window, so it is not asked for twice.
    expect(getNotifications).toHaveBeenCalledTimes(1);
    expect(getNotifications).toHaveBeenCalledWith(0, PAGE);
    // A full page answered means there may be older rows below it.
    expect(wrapper.find(".load-more-btn").exists()).toBe(true);
  });

  it("cannot lose the drifted page to a refusal of a second request", async () => {
    const { wrapper, store, getNotifications } = await render(
      items("held-", PAGE),
    );
    const drifted = items("gone-", PAGE);
    getNotifications.mockClear();
    getNotifications.mockResolvedValueOnce(answer(drifted)).mockResolvedValue({
      data: null,
      error: { type: "about:blank", title: "", status: 500, traceId: "t" },
    } as never);

    await arrive(store);

    // Reading the list again from scratch empties it before asking, so a refused
    // second request would leave the reader nothing at all. There is no second
    // request to refuse.
    expect(rows(wrapper)).toEqual(drifted.map((n) => n.id));
    expect(getNotifications).toHaveBeenCalledTimes(1);
  });

  it("opens the way to older rows when a full page lands on an empty list", async () => {
    const { wrapper, store, getNotifications } = await render([]);
    const arrived = items("new-", PAGE);
    getNotifications.mockResolvedValue(answer(arrived));

    await arrive(store, PAGE);

    // An empty list is the same case as a drifted one, and a page this full says
    // there may be more below it. Treating it as a plain prepend would leave the
    // list unable to reach past its twentieth row.
    expect(rows(wrapper)).toEqual(arrived.map((n) => n.id));
    expect(wrapper.find(".load-more-btn").exists()).toBe(true);
  });

  it("asks the store to re-read the count after marking all read", async () => {
    const { wrapper, markAsRead, fetchUnreadCount } = await render(
      [item("old-1")],
      undefined,
      1,
    );

    await markAll(wrapper);

    expect(markAsRead).toHaveBeenCalledWith();
    expect(fetchUnreadCount).toHaveBeenCalledWith(true);
  });

  it("keeps the list when the server refuses to mark everything read", async () => {
    const { wrapper, markAsRead } = await render([item("old-1")], undefined, 1);
    markAsRead.mockResolvedValue({
      data: null,
      error: { type: "about:blank", title: "", status: 404, traceId: "t" },
    } as never);

    await markAll(wrapper);

    expect(rows(wrapper)).toEqual(["old-1"]);
    // Still unread: nothing was marked.
    expect(unreadRows(wrapper)).toEqual(["old-1"]);
    // Under the line of the unread, which stays for another try; no plate.
    expect(wrapper.find(".unread-line").exists()).toBe(true);
    expect(wrapper.get(".unread-refusal").text()).toBe(
      "Не удалось отметить уведомления",
    );
    expect(messages()).toEqual([]);
  });
});

/**
 * A row of the table says what happened, where, who and when, in the server's
 * words, a row of a table and not a paragraph of text: the title names the
 * event and leads to it, the place and the people stand in columns of their
 * own, the date in the last one, and the other details under the title
 * whole, in the words of the letter about the same event. Nothing is cut: a
 * detail that does not fit a line goes to the next one, the way every caption
 * of the site does.
 */
describe("a row of the list", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("names the notification by the server's title and leads where the server says", async () => {
    const { wrapper } = await render([
      item("n1", {
        eventType: NotificationType.NewTopic,
        title: "Персонаж принят",
        url: "/game/vedmak~eFgH-1_2",
      }),
    ]);

    const link = wrapper
      .get(".notification-table")
      .getComponent(RouterLinkStub);
    expect(seen(link.element)).toBe("Персонаж принят");
    expect(link.props("to")).toBe("/game/vedmak~eFgH-1_2");
    expect(link.classes()).toContain("notification-title");
  });

  it("writes the title of a notification that leads nowhere as text", async () => {
    // The server leaves a null path out of the row (WhenWritingNull).
    const { wrapper } = await render([
      item("n1", { title: "Пароль изменен", url: undefined }),
    ]);

    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(
      // Only the line about the channels leads somewhere.
      1,
    );
    const title = wrapper.get(".notification-title");
    expect(title.element.tagName).toBe("SPAN");
    expect(seen(title.element)).toBe("Пароль изменен");
  });

  it("dates the notification in a column of its own, the title alone", async () => {
    const { wrapper } = await render([
      item("n1", { createdUtc: "2026-10-12T08:43:00Z" }),
    ]);

    expect(seen(wrapper.get(".notification-title").element)).toBe("n1");
    expect(wrapper.get(".notification-date").text()).toBe(
      formatDateFull("2026-10-12T08:43:00Z"),
    );
    const headers = wrapper.findAll("thead th").map((th) => th.text());
    expect(headers).toEqual(["Событие", "Где", "Кто", "Когда"]);
  });

  it("says every detail whole as a part of a caption line, the label held to its value", async () => {
    const { wrapper } = await render([
      item("n1", {
        details: [
          detail("Персонаж", "Цири"),
          detail("Игра", "Ведьмак: Дикая Охота"),
          detail("Мастер", "Player-Two"),
        ],
      }),
    ]);

    const line = wrapper.get(".notification-details");
    expect(line.classes()).toContain("caption-line");
    const parts = line.findAll(".caption-line__part");
    expect(parts.map((part) => part.text())).toEqual([
      `Персонаж:${NBSP}Цири`,
      `Игра:${NBSP}Ведьмак: Дикая Охота`,
      `Мастер:${NBSP}Player-Two`,
    ]);
    // A part of the line and nothing of its own: the line's rule for a part
    // that does not fit (to the next line whole, wrapping inside only when it
    // is wider than a line) is the one rule for every caption of the site.
    for (const part of parts) {
      expect(part.findAll("[class]").map((one) => one.classes())).toEqual([
        ["notification-detail"],
      ]);
    }
  });

  it("writes a moment in the reader's clock", async () => {
    const { wrapper } = await render([
      item("n1", {
        details: [
          detail("Окончание", "2026-10-19T12:00:00+00:00", true),
          detail("Модератор", "TestModerator"),
        ],
      }),
    ]);

    expect(
      wrapper.findAll(".notification-detail").map((one) => one.text()),
    ).toEqual([
      `Окончание:${NBSP}${formatDateFull("2026-10-19T12:00:00+00:00")}`,
      `Модератор:${NBSP}TestModerator`,
    ]);
  });

  it("draws no line under a notification with nothing to say about it", async () => {
    const { wrapper } = await render([item("n1", { details: [] })]);

    expect(wrapper.find(".notification-details").exists()).toBe(false);
  });

  it("marks the unread bold and tells a screen reader, and the read neither", async () => {
    const { wrapper } = await render([
      item("unread-1", { isRead: false }),
      item("read-1", { isRead: true }),
      item("unread-2", { isRead: false, url: undefined }),
    ]);

    expect(unreadRows(wrapper)).toEqual(["unread-1", "unread-2"]);
    const hidden = (title: string) =>
      titleOf(wrapper, title)
        .findAll(".visually-hidden")
        .map((one) => one.text());
    // Inside the title, so the link is heard as its title and ", не прочитано".
    expect(hidden("unread-1")).toEqual([", не прочитано"]);
    expect(hidden("unread-2")).toEqual([", не прочитано"]);
    expect(hidden("read-1")).toEqual([]);
  });

  it("has no cross and no word of its own to go or to mark", async () => {
    const { wrapper } = await render([item("n1"), item("n2")]);

    expect(wrapper.find("button[aria-label]").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Перейти");
    expect(wrapper.text()).not.toContain("Отметить прочитанным");
    // A row that names no place and nobody has one link, and it is the title.
    const body = wrapper.findAll("tbody tr");
    expect(body).toHaveLength(2);
    for (const row of body) {
      const links = row.findAllComponents(RouterLinkStub);
      expect(links).toHaveLength(1);
      expect(links[0].classes()).toContain("notification-title");
    }
  });
});

/**
 * The details a notification carries, sorted into the columns of its row by
 * the payload field each one was written from: a name of a person goes to
 * "Кто" as a link to the profile, the title of a game, room, topic, board,
 * blog or publication to "Где", the first of them leading where the
 * notification leads, and the rest stays under the title.
 */
describe("the columns of a row", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** A post owed in a room, as the generator of the event writes it. */
  const pendency = () =>
    item("Ожидание поста", {
      url: "/game/vedmak~UK5L",
      details: [
        detail("Игра", "Ведьмак: Дикая Охота"),
        detail("Комната", "Каэр Морхен"),
        detail("Персонаж", "Цири"),
        detail("Создал", "SolohinLex"),
      ],
      payload: {
        pendencyId: "p1",
        gameId: "vedmak~UK5L",
        gameTitle: "Ведьмак: Дикая Охота",
        roomId: "r1",
        roomTitle: "Каэр Морхен",
        characterName: "Цири",
        createdByUsername: "SolohinLex",
      },
    });

  /** The cell of the column a key names, in the first row. */
  const cell = (wrapper: Wrapper, key: string) => {
    const index = ["event", "place", "people", "when"].indexOf(key);
    return wrapper.findAll("tbody tr")[0].findAll("td")[index];
  };

  it("puts the place in Где, leading where the notification leads, and the larger place under it", async () => {
    const { wrapper } = await render([pendency()]);

    const place = cell(wrapper, "place");
    const link = place.getComponent(RouterLinkStub);
    expect(link.text()).toBe("Ведьмак: Дикая Охота");
    expect(link.props("to")).toBe("/game/vedmak~UK5L");
    expect(place.get(".notification-details").text()).toBe(
      `Комната:${NBSP}Каэр Морхен`,
    );
  });

  it("puts the people in Кто, each a link to the profile", async () => {
    const { wrapper } = await render([pendency()]);

    const people = cell(wrapper, "people");
    const link = people.getComponent(RouterLinkStub);
    expect(link.text()).toBe("SolohinLex");
    expect(link.props("to")).toEqual({
      name: "profile",
      params: { username: "SolohinLex" },
    });
  });

  it("keeps under the title only what is neither a place nor a person", async () => {
    const { wrapper } = await render([pendency()]);

    const event = cell(wrapper, "event");
    expect(
      event.findAll(".notification-detail").map((one) => one.text()),
    ).toEqual([`Персонаж:${NBSP}Цири`]);
    expect(event.text()).not.toContain("SolohinLex");
    expect(event.text()).not.toContain("Каэр Морхен");
  });

  it("marks the row read when its place is opened, as when its title is", async () => {
    const { wrapper, markAsRead } = await render([pendency()]);

    await cell(wrapper, "place").getComponent(RouterLinkStub).trigger("click");
    await flushPromises();

    expect(markAsRead).toHaveBeenCalledWith("Ожидание поста");
    expect(unreadRows(wrapper)).toEqual([]);
  });

  it("leaves Где empty for a notification about no place", async () => {
    const { wrapper } = await render([
      item("Вынесено предупреждение", {
        url: "/warnings?username=TestUser",
        details: [
          detail("Причина", "Флуд"),
          detail("Модератор", "TestModerator"),
          detail("Баллы", "1"),
        ],
        payload: {
          warningId: "w1",
          username: "TestUser",
          reason: "Флуд",
          moderatorUsername: "TestModerator",
          points: 1,
        },
      }),
    ]);

    expect(cell(wrapper, "place").text()).toBe("");
    expect(cell(wrapper, "people").text()).toBe("TestModerator");
    expect(
      cell(wrapper, "event")
        .findAll(".notification-detail")
        .map((one) => one.text()),
    ).toEqual([`Причина:${NBSP}Флуд`, `Баллы:${NBSP}1`]);
  });
});

/**
 * Opening a notification is reading it, the way a letter is read in the mail:
 * the press on the title leads to the thing and marks the row read in the same
 * moment, without the way there waiting on the request. The bell counts what
 * the server holds, so it is re-read once the server has answered the mark and
 * not beside it: a count read alongside the mark can overtake it and show the
 * old number, and one read without `immediate` waits up to two seconds.
 */
describe("opening a notification", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("marks it read, and re-reads the count only after the server answered", async () => {
    const { wrapper, markAsRead, fetchUnreadCount } = await render(
      [item("old-1"), item("old-2")],
      undefined,
      2,
    );
    let answerMark: (result: unknown) => void = () => {};
    markAsRead.mockReturnValueOnce(
      new Promise((resolve) => {
        answerMark = resolve;
      }) as never,
    );

    await titleOf(wrapper, "old-1").trigger("click");
    await flushPromises();

    expect(markAsRead).toHaveBeenCalledTimes(1);
    expect(markAsRead).toHaveBeenCalledWith("old-1");
    // Read on the page at once: the reader is on the way to it.
    expect(unreadRows(wrapper)).toEqual(["old-2"]);
    expect(fetchUnreadCount).not.toHaveBeenCalled();

    answerMark({ data: null, error: null });
    await flushPromises();

    expect(fetchUnreadCount).toHaveBeenCalledTimes(1);
    expect(fetchUnreadCount).toHaveBeenCalledWith(true);
    // The row stays where it was: a read notification is history, not gone.
    expect(rows(wrapper)).toEqual(["old-1", "old-2"]);
  });

  it("does not hold the way to it", async () => {
    const { wrapper } = await render([item("old-1")], undefined, 1);
    const event = new MouseEvent("click", { bubbles: true, cancelable: true });

    titleOf(wrapper, "old-1").element.dispatchEvent(event);
    await flushPromises();

    expect(event.defaultPrevented).toBe(false);
  });

  it("re-reads the count after a refused mark too, and says nothing", async () => {
    const { wrapper, markAsRead, fetchUnreadCount } = await render(
      [item("old-1")],
      undefined,
      1,
    );
    markAsRead.mockResolvedValue(refused(500, "Ошибка сервера"));

    await titleOf(wrapper, "old-1").trigger("click");
    await flushPromises();

    // The reader is on the page the title led to by now: the count shows the
    // truth, and the row is bold again on the next visit.
    expect(fetchUnreadCount).toHaveBeenCalledWith(true);
    expect(wrapper.find(".action-result").exists()).toBe(false);
    expect(messages()).toEqual([]);
  });

  it("sends nothing for a notification already read", async () => {
    const { wrapper, markAsRead, fetchUnreadCount } = await render([
      item("old-1", { isRead: true }),
    ]);

    await titleOf(wrapper, "old-1").trigger("click");
    await flushPromises();

    expect(markAsRead).not.toHaveBeenCalled();
    expect(fetchUnreadCount).not.toHaveBeenCalled();
  });
});

/**
 * "Прочитать все" was a large grey button beside the heading: taller than the
 * heading row, half of it on a phone, named for reading while it marked, and
 * there with nothing unread. The page had no number of the unread at all. Now
 * a line under the heading counts the bell's unread and marks them, and stands
 * only while there are any (shared/ui/UnreadLine holds the line itself).
 *
 * Marking everything read leaves the list as it was, every row in place and
 * none of them bold: the list is the history of what happened, and the rows
 * used to vanish only because the list could not tell read from unread.
 */
describe("the line of the unread", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("is not there while nothing is unread, and no button stands for it", async () => {
    const { wrapper } = await render([item("old-1"), item("old-2")]);

    expect(wrapper.find(".unread-line").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Прочитать все");
    expect(wrapper.text()).not.toContain("Отметить прочитанными");
  });

  it("counts the bell's unread under the line about the channels, not the rows on screen", async () => {
    // The list answers read and unread alike, so two rows can stand for four.
    const { wrapper } = await render(
      [item("old-1"), item("old-2")],
      undefined,
      4,
    );

    expect(unreadLine(wrapper)).toBe(
      "Непрочитанных: 4 | Отметить прочитанными",
    );
    // The heading, the line about the channels, then the line of the unread.
    const heading = wrapper.get("h1").element;
    expect(heading.nextElementSibling).toBe(
      wrapper.get(".channels-hint").element,
    );
    expect(heading.nextElementSibling!.nextElementSibling).toBe(
      wrapper.get(".unread-place").element,
    );
  });

  it("writes a thousand as 999+", async () => {
    const { wrapper } = await render([item("old-1")], undefined, 1234);

    expect(unreadLine(wrapper)).toBe(
      "Непрочитанных: 999+ | Отметить прочитанными",
    );
  });

  it("marks everything at one press, then puts the reader on the heading", async () => {
    const { wrapper, markAsRead } = await render(
      [item("old-1"), item("old-2")],
      undefined,
      4,
    );

    await markAll(wrapper);

    expect(markAsRead).toHaveBeenCalledTimes(1);
    expect(markAsRead).toHaveBeenCalledWith();
    expect(wrapper.find(".unread-line").exists()).toBe(false);
    // The link that had the focus went with the line.
    expect(document.activeElement).toBe(wrapper.get("h1").element);
    expect(wrapper.get("[role=status]").text()).toBe(
      "Все уведомления прочитаны",
    );
  });

  it("leaves every row in place, none of them bold and none heard as unread", async () => {
    const { wrapper } = await render(
      [item("old-1"), item("old-2", { isRead: true }), item("old-3")],
      undefined,
      2,
    );

    await markAll(wrapper);

    expect(rows(wrapper)).toEqual(["old-1", "old-2", "old-3"]);
    expect(unreadRows(wrapper)).toEqual([]);
    expect(wrapper.find(".notification-list .visually-hidden").exists()).toBe(
      false,
    );
    expect(wrapper.text()).not.toContain("Нет уведомлений");
    // The page says it, so nothing floats over the page to say it again.
    expect(messages()).toEqual([]);
  });

  it("keeps the way to older rows", async () => {
    const { wrapper } = await render(items("held-", PAGE), undefined, PAGE);

    await markAll(wrapper);

    // Everything below is read as well now, and still the reader's history.
    expect(wrapper.find(".load-more-btn").exists()).toBe(true);
  });

  it("puts what arrives after it on top of the rows it marked", async () => {
    const { wrapper, store, getNotifications } = await render(
      [item("old-1"), item("old-2")],
      undefined,
      2,
    );
    await markAll(wrapper);
    // The list endpoint answers with read notifications too.
    getNotifications.mockResolvedValue(
      answer([
        item("new-1"),
        item("old-1", { isRead: true }),
        item("old-2", { isRead: true }),
      ]),
    );

    await arrive(store);

    expect(rows(wrapper)).toEqual(["new-1", "old-1", "old-2"]);
    expect(unreadRows(wrapper)).toEqual(["new-1"]);
  });
});

/**
 * Where Telegram and Discord are switched on: the last section of a long
 * account page, which a reader looking for them on this page would not find.
 */
describe("the line about the channels", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("leads to the notifications of the account", async () => {
    const { wrapper } = await render([]);

    const hint = wrapper.get(".channels-hint");
    expect(seen(hint.element)).toBe(
      "Уведомления в Telegram и Discord подключаются в настройках аккаунта",
    );
    expect(hint.getComponent(RouterLinkStub).props("to")).toEqual({
      name: "account",
      hash: "#notifications",
    });
  });
});

/**
 * jsdom applies no scoped styles, so what the rows look like is read off the
 * page's own stylesheet, compiled the way the build compiles it.
 */
describe("the look of the list", () => {
  const css = componentCss(join(__dirname, "NotificationsPage.vue"));
  const rules = ruleMap(css);

  // The date goes to two lines in its narrow column, over the time, as the
  // date of the last activity of a board does; the time after "в" is held to
  // it by the date's own no-break space.
  it("cuts nothing and holds nothing on one line", () => {
    expect(css).not.toMatch(/text-overflow|line-clamp|overflow: hidden/);
    const unbroken = [...rules]
      .filter(([, declarations]) => declarations["white-space"] === "nowrap")
      .map(([selector]) => selector);
    expect(unbroken).toEqual([]);
  });

  // A read title is grey, as a read letter of the mail list of DM2 is.
  it("greys the read title", () => {
    expect(rules.get(".notification-title--read")?.color).toBe(
      "var(--text-muted)",
    );
  });

  it("is bold for the unread title and nothing else", () => {
    expect(rules.get(".notification-title--unread")?.["font-weight"]).toBe(
      "bold",
    );
    const bold = [...rules]
      .filter(([, declarations]) => declarations["font-weight"])
      .map(([selector]) => selector);
    expect(bold).toEqual([".notification-title--unread"]);
  });

  it("puts nothing in the middle of the column", () => {
    for (const [selector, declarations] of rules) {
      expect(declarations["justify-content"], selector).not.toBe("center");
      expect(declarations["text-align"], selector).not.toBe("center");
    }
  });

  // A table breaks a word inside wherever its column comes out narrower than
  // the word, and a column with a width of its own takes it before the
  // columns without one. So only the date, whose words are the same in every
  // row, has a width; the place and the people are as wide as their words,
  // and what happened takes the rest.
  it("gives the place and the people the width of their words", async () => {
    const { wrapper } = await render([item("n1")]);

    const width = (key: string) =>
      (wrapper.get(`th.col-${key}`).element as HTMLElement).style.width;
    expect(width("event")).toBe("");
    expect(width("place")).toBe("");
    expect(width("people")).toBe("");
    expect(width("when")).toBe("86px");
  });

  // A word of a cell, a login and a date included, breaks the way the words
  // of every table of the site do (DataTable), so the page says nothing of
  // its own about it.
  it("leaves the breaking of words to the table", () => {
    expect(css).not.toMatch(/overflow-wrap|word-break|max-width|cqi/);
    expect(rules.get(".notifications-page")?.["container-type"]).toBe(
      undefined,
    );
  });
});

/**
 * A page the reader asked for and did not get.
 *
 * A failed load is the state of the page and not a plate over it, and a plate
 * in the corner over an empty list explains nothing (UI_STANDARDS,
 * "Всплывающие сообщения"). The page prints the refusal itself - where the
 * list would stand for the first page, under the rows already drawn for a
 * later one - the statuses the interceptor used to plate included, whose plate
 * is taken down (refusalHere). An expired session and a dead connection keep
 * the plate, and the page claims no empty list for them either, because "Нет
 * уведомлений" over a dead server is a lie.
 *
 * No plate is raised in any test here: the interceptor lives in the axios
 * instance and the api is doubled above it, so what is measured is that the
 * page itself says nothing in the corner.
 */
describe("a refused page", () => {
  const EMPTY_LIST = "Нет уведомлений";

  beforeEach(() => {
    vi.restoreAllMocks();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("prints why the first page did not arrive where the list would stand, in the server's words", async () => {
    const { wrapper } = await render(
      [],
      refused(404, "Уведомления недоступны"),
    );

    const line = wrapper.get(".action-result");
    expect(line.text()).toBe("Уведомления недоступны");
    expect(line.attributes("role")).toBe("alert");
    expect(wrapper.text()).not.toContain(EMPTY_LIST);
    expect(messages()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    const { wrapper } = await render([], refused(404));

    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось загрузить уведомления",
    );
  });

  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])(
    "prints a first page refused with status %i where the list would stand",
    async (status, line) => {
      const { wrapper } = await render([], refused(status, "Ошибка сервера"));

      expect(wrapper.get(".action-result").text()).toBe(line);
      expect(wrapper.text()).not.toContain(EMPTY_LIST);
      expect(messages()).toEqual([]);
    },
  );

  it.each([0, 401])(
    "leaves a first page refused with status %i to the interceptor, and claims no empty list",
    async (status) => {
      const { wrapper } = await render([], refused(status, "Ошибка сервера"));

      expect(wrapper.find(".action-result").exists()).toBe(false);
      expect(wrapper.text()).not.toContain(EMPTY_LIST);
      expect(messages()).toEqual([]);
    },
  );

  it("keeps the rows, and the way to older ones, when a later page is refused, and says so under them", async () => {
    const held = items("held-", PAGE);
    const { wrapper, getNotifications } = await render(held);
    getNotifications.mockResolvedValueOnce(refused(404, "Страница недоступна"));

    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();

    expect(rows(wrapper)).toEqual(held.map((n) => n.id));
    expect(wrapper.get(".action-result").text()).toBe("Страница недоступна");
    // A failed page is not the end of the list.
    expect(wrapper.find(".load-more-btn").exists()).toBe(true);
    expect(messages()).toEqual([]);
  });

  it("takes the refusal off the screen when the page is asked for again", async () => {
    const held = items("held-", PAGE);
    const { wrapper, getNotifications } = await render(held);
    getNotifications
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce(answer([item("tail-1")]));
    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();
    expect(wrapper.find(".action-result").exists()).toBe(true);

    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();

    expect(wrapper.find(".action-result").exists()).toBe(false);
    expect(rows(wrapper)).toEqual([...held.map((n) => n.id), "tail-1"]);
  });

  it("takes the refusal off the screen when an arrival lands rows on it", async () => {
    const { wrapper, store, getNotifications } = await render([], refused(404));
    expect(wrapper.find(".action-result").exists()).toBe(true);
    getNotifications.mockResolvedValue(answer([item("new-1")]));

    await arrive(store);

    // The rows on screen are the server's answer now, and a line under them
    // about a page that did not arrive would be about another list.
    expect(wrapper.find(".action-result").exists()).toBe(false);
    expect(rows(wrapper)).toEqual(["new-1"]);
  });
});
