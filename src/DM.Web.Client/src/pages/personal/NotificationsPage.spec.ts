/**
 * @vitest-environment jsdom
 */

// The page follows the count the store owns, reading the truth back over REST,
// and has the store read the count again after it marks anything read. A row
// is NotificationsPage.rows.spec.ts's, reading NotificationsPage.reading.spec.ts's.
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
import { deferred } from "@/shared/lib/testing/tasks";
import {
  answer,
  arrive,
  item,
  items,
  markAll,
  messages,
  PAGE,
  refused,
  render,
  rows,
  unmountAll,
  unreadRows,
} from "./NotificationsPage.fixtures";

afterEach(unmountAll);

describe("NotificationsPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Error toasts never auto-dismiss and deduplicate by text.
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("reads a page as long as the reader set for lists", async () => {
    const { getNotifications } = await render([], undefined, 0, 50);

    expect(getNotifications).toHaveBeenCalledWith(0, 50);
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
    // The arrival goes on top; what was on screen stays where it was.
    expect(rows(wrapper)).toEqual(["new-1", "old-1", "old-2"]);
  });

  it("keeps the pages already loaded below the newest one", async () => {
    const held = items("held-", PAGE);
    const { wrapper, store, getNotifications } = await render(held);
    getNotifications.mockResolvedValueOnce(answer([item("tail-1")]));
    await wrapper.find(".load-more-btn").trigger("click");
    await flushPromises();
    // The newest page cannot reach a row scrolled past it; a re-read would drop it.
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
    // The window moved before this page was asked for: one row on screen, nineteen new.
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

    // Two windows answered, so the third starts at forty; counting the arrival
    // too would step over the row at that offset.
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

    // Held back, not dropped, and read exactly once.
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

    // A full page of strangers: splicing it on top would leave an unreachable hole.
    expect(rows(wrapper)).toEqual(drifted.map((n) => n.id));
    // The page in hand is the newest window, so it is not asked for twice.
    expect(getNotifications).toHaveBeenCalledTimes(1);
    expect(getNotifications).toHaveBeenCalledWith(0, PAGE);
    // A full page may have older rows below it.
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

    // There is no second request whose refusal would leave the reader nothing.
    expect(rows(wrapper)).toEqual(drifted.map((n) => n.id));
    expect(getNotifications).toHaveBeenCalledTimes(1);
  });

  it("opens the way to older rows when a full page lands on an empty list", async () => {
    const { wrapper, store, getNotifications } = await render([]);
    const arrived = items("new-", PAGE);
    getNotifications.mockResolvedValue(answer(arrived));

    await arrive(store, PAGE);

    // An empty list is the drifted case; a full page may have more below it.
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
    // Under the line of the unread, which stays for another try.
    expect(wrapper.find(".unread-line").exists()).toBe(true);
    expect(wrapper.get(".unread-refusal").text()).toBe(
      "Не удалось отметить уведомления",
    );
    expect(messages()).toEqual([]);
  });
});

// A page the reader asked for and did not get is said where the list would
// stand, or under the rows; no empty list is claimed for a dead server.
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

  it("prints a refused first page where the list would stand", async () => {
    const { wrapper } = await render([], refused(500, "Ошибка сервера"));

    expect(wrapper.get(".action-result").text()).toBe("Ошибка сервера");
    expect(wrapper.text()).not.toContain(EMPTY_LIST);
    expect(messages()).toEqual([]);
  });

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

    // The rows on screen are the server's answer now.
    expect(wrapper.find(".action-result").exists()).toBe(false);
    expect(rows(wrapper)).toEqual(["new-1"]);
  });
});

// Until the list answers, the table stands as its skeleton and no empty list is claimed.
describe("a list on its way", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("stands the skeleton of the table, then says the list is empty", async () => {
    const first = deferred<unknown>();
    const { wrapper } = await render([], first.promise);

    expect(wrapper.find(".notification-table .skeleton-row").exists()).toBe(
      true,
    );
    expect(wrapper.text()).not.toContain("Нет уведомлений");
    expect(wrapper.text()).not.toContain("Загрузка");

    first.resolve(answer([]));
    await flushPromises();

    expect(wrapper.find(".skeleton-row").exists()).toBe(false);
    expect(wrapper.text()).toContain("Нет уведомлений");
  });

  it("keeps the words of Показать еще while the next page is read", async () => {
    const { wrapper, getNotifications } = await render(items("n", PAGE));
    getNotifications.mockReturnValue(deferred<never>().promise);

    await wrapper.get(".load-more-btn").trigger("click");

    const more = wrapper.get(".load-more-btn");
    expect(more.text()).toBe("Показать еще");
    expect(more.attributes("aria-busy")).toBe("true");
  });
});
