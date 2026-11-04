/**
 * @vitest-environment jsdom
 */

// What a row says is the server's: title, path and details from the table the
// letter and the bots are written from. The page names no event of its own.
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, RouterLinkStub } from "@vue/test-utils";
import { NotificationType } from "@/shared/api/models/notifications";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  detail,
  item,
  NBSP,
  render,
  seen,
  titleOf,
  unmountAll,
  unreadRows,
  type Wrapper,
} from "./NotificationsPage.fixtures";

afterEach(unmountAll);

// A row says what happened, where, who and when, in the server's words, and
// nothing is cut: a detail that does not fit goes to the next line.
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
    // The server leaves a null path out of the row.
    const { wrapper } = await render([
      item("n1", { title: "Пароль изменен", url: undefined }),
    ]);

    expect(wrapper.findAllComponents(RouterLinkStub)).toHaveLength(
      // Only the line about the channels.
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
    // The caption line's one rule for a part that does not fit.
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
    // Inside the title, so the link is heard with ", не прочитано".
    expect(hidden("unread-1")).toEqual([", не прочитано"]);
    expect(hidden("unread-2")).toEqual([", не прочитано"]);
    expect(hidden("read-1")).toEqual([]);
  });
});

// The details sorted into columns by the payload field each was written from:
// a person to "Кто", a place to "Где", the first place leading where the
// notification leads, the rest under the title.
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

  // jsonb orders the bag's keys by length, so the blog comes back ahead of the
  // publication; the server marks the publication as the place.
  const publicationComment = () =>
    item("Новый комментарий к публикации", {
      url: "/publication/pervaja~mNoP",
      details: [
        detail("Блог", "Записки мастера"),
        detail("Автор", "TestHonorary"),
        detail("Публикация", "Первая партия", false, true),
      ],
      payload: {
        blogId: "zapiski~iJkL",
        blogTitle: "Записки мастера",
        commentId: "c1",
        publicationId: "pervaja~mNoP",
        authorUsername: "TestHonorary",
        publicationTitle: "Первая партия",
      },
    });

  it("leads from the place the server marks, the publication and not its blog, whatever order the store kept", async () => {
    const { wrapper } = await render([publicationComment()]);

    const place = cell(wrapper, "place");
    const link = place.getComponent(RouterLinkStub);
    expect(link.text()).toBe("Первая партия");
    expect(link.props("to")).toBe("/publication/pervaja~mNoP");
    expect(place.get(".notification-details").text()).toBe(
      `Блог:${NBSP}Записки мастера`,
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

// The look of the list is the visual stand's to guard; the widths are the columns the page declares.
describe("the widths of the columns", () => {
  // A table breaks a word where its column is narrower; only the date has a width.
  it("gives the place and the people the width of their words", async () => {
    const { wrapper } = await render([item("n1")]);

    const width = (key: string) =>
      (wrapper.get(`th.col-${key}`).element as HTMLElement).style.width;
    expect(width("event")).toBe("");
    expect(width("place")).toBe("");
    expect(width("people")).toBe("");
    expect(width("when")).toBe("86px");
  });
});
