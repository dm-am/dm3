/**
 * @vitest-environment jsdom
 */

// Reading on the notifications page: one opened, all marked, and the channels line.
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, RouterLinkStub } from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
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
  seen,
  titleOf,
  unmountAll,
  unreadLine,
  unreadRows,
} from "./NotificationsPage.fixtures";

afterEach(unmountAll);

// Opening is reading: the press leads to the thing and marks the row at once;
// the bell is read again after the server answered the mark, not beside it.
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
    // Read on the page at once.
    expect(unreadRows(wrapper)).toEqual(["old-2"]);
    expect(fetchUnreadCount).not.toHaveBeenCalled();

    answerMark({ data: null, error: null });
    await flushPromises();

    expect(fetchUnreadCount).toHaveBeenCalledTimes(1);
    expect(fetchUnreadCount).toHaveBeenCalledWith(true);
    // The row stays: a read notification is history.
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

    // The count shows the truth; the row is bold again on the next visit.
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

// A line under the heading counts the bell's unread and marks them, standing
// only while there are any; marked rows stay, none of them bold.
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
    // The list answers read and unread alike: two rows can stand for four.
    const { wrapper } = await render(
      [item("old-1"), item("old-2")],
      undefined,
      4,
    );

    expect(unreadLine(wrapper)).toBe(
      "Непрочитанных: 4 | Отметить прочитанными",
    );
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
