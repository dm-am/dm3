/**
 * The details of a notification sorted into who, where and the rest by the
 * payload field each one was written from. The payloads below are the ones the
 * generators of the dispatcher write, as the store hands them back (camelCase),
 * and the details the ones NotificationWording.ReadDetails makes of them.
 */
import { describe, expect, it } from "vitest";
import type {
  NotificationDetail,
  UserNotification,
} from "@/shared/api/models/notifications";
import { notificationParts } from "./parts";

const detail = (
  label: string,
  value: string,
  isMoment = false,
  isDestination = false,
): NotificationDetail =>
  ({ label, value, isMoment, isDestination }) as NotificationDetail;

/** The detail the server marks as the place the notification leads to. */
const destination = (label: string, value: string) =>
  detail(label, value, false, true);

/** The row as the list hands it over, its details left unbranded here. */
const partsOf = (row: { details: NotificationDetail[]; payload: unknown }) =>
  notificationParts(row as Pick<UserNotification, "details" | "payload">);

const labels = (details: NotificationDetail[]) =>
  details.map(({ label }) => label);

/**
 * A bag in the order jsonb keeps its keys: the shorter first, and keys of one
 * length by their bytes (lengthCompareJsonbString in PostgreSQL).
 */
const storedOrder = (payload: Record<string, unknown>) =>
  Object.fromEntries(
    Object.entries(payload).sort(
      ([a], [b]) => a.length - b.length || (a < b ? -1 : a > b ? 1 : 0),
    ),
  );

describe("notificationParts", () => {
  it("sorts a post owed in a room into the game and the room, the character, and who asked", () => {
    const parts = partsOf({
      details: [
        detail("Игра", "Ведьмак"),
        detail("Комната", "Каэр Морхен"),
        detail("Персонаж", "Цири"),
        detail("Создал", "SolohinLex"),
      ],
      payload: {
        pendencyId: "p1",
        gameId: "vedmak~UK5L",
        gameTitle: "Ведьмак",
        roomId: "r1",
        roomTitle: "Каэр Морхен",
        characterName: "Цири",
        createdByUsername: "SolohinLex",
      },
    });

    expect(labels(parts.places)).toEqual(["Игра", "Комната"]);
    expect(labels(parts.people)).toEqual(["Создал"]);
    expect(labels(parts.rest)).toEqual(["Персонаж"]);
  });

  // The addressee is carried under "username" for the link, and the person
  // the row names may have the very same name: the master told about his own
  // game, a reader warned by himself. The name goes to the person all the same.
  // A warning with no reason: the server drops the empty field, and the first
  // detail is the moderator, whose name the addressee field carries as well.
  it("never pairs a detail with the field of the addressee", () => {
    const parts = partsOf({
      details: [detail("Модератор", "TestUser"), detail("Баллы", "1")],
      payload: {
        warningId: "w1",
        username: "TestUser",
        reason: "",
        moderatorUsername: "TestUser",
        points: 1,
      },
    });

    expect(labels(parts.people)).toEqual(["Модератор"]);
    expect(labels(parts.rest)).toEqual(["Баллы"]);
    expect(parts.places).toEqual([]);
  });

  // A title of a place that is also the name of somebody in the same
  // notification is told apart by the field it stands in, not by its words.
  it("tells a place from a person of the same name by the field", () => {
    const parts = partsOf({
      details: [detail("Игра", "SolohinLex"), detail("Мастер", "SolohinLex")],
      payload: {
        gameId: "g1",
        gameTitle: "SolohinLex",
        masterUsername: "SolohinLex",
      },
    });

    expect(labels(parts.places)).toEqual(["Игра"]);
    expect(labels(parts.people)).toEqual(["Мастер"]);
  });

  it("pairs a number and a moment with the field they were written from", () => {
    const parts = partsOf({
      details: [
        detail("Модератор", "TestSeniorMod"),
        detail("Окончание", "2026-10-20T06:10:00Z", true),
        detail("Баллы", "2"),
      ],
      payload: {
        banId: "b1",
        username: "TestUser",
        moderatorUsername: "TestSeniorMod",
        endedUtc: "2026-10-20T06:10:00Z",
        points: 2,
      },
    });

    expect(labels(parts.people)).toEqual(["Модератор"]);
    expect(labels(parts.rest)).toEqual(["Окончание", "Баллы"]);
  });

  // Two places in one bag, the one the notification leads to and the larger
  // one it stands in. The store hands the bag back as jsonb, which orders keys
  // by length and then by their bytes, and the server reads the details out
  // of that same bag in that same order: the blog comes back ahead of its
  // publication, the board ahead of its topic. The order says nothing, and
  // the place first in the column is the one the server marks.
  describe.each<{
    about: string;
    written: Record<string, unknown>;
    wording: Record<string, NotificationDetail>;
    places: string[];
  }>([
    {
      about: "a comment under a publication",
      written: {
        commentId: "c1",
        publicationId: "pervaja~mNoP",
        publicationTitle: "Первая партия",
        blogId: "zapiski~iJkL",
        blogTitle: "Записки мастера",
        authorUsername: "TestHonorary",
      },
      wording: {
        publicationTitle: destination("Публикация", "Первая партия"),
        blogTitle: detail("Блог", "Записки мастера"),
        authorUsername: detail("Автор", "TestHonorary"),
      },
      places: ["Публикация", "Блог"],
    },
    {
      about: "a changed topic",
      written: {
        topicId: "faq~eFgH",
        topicTitle: "FAQ",
        boardId: "obshchee~aBcD",
        boardTitle: "Общее",
        authorUsername: "TestModerator",
      },
      wording: {
        topicTitle: destination("Топик", "FAQ"),
        boardTitle: detail("Раздел", "Общее"),
        authorUsername: detail("Автор", "TestModerator"),
      },
      places: ["Топик", "Раздел"],
    },
  ])("$about", ({ written, wording, places }) => {
    /** The details the server reads out of a bag, in the order of the bag. */
    const rowOf = (payload: Record<string, unknown>) => ({
      payload,
      details: Object.keys(payload).flatMap((field) =>
        field in wording ? [wording[field]] : [],
      ),
    });

    it.each([
      ["in the order the generator wrote", written],
      ["in the order the store hands back", storedOrder(written)],
    ])("puts first the place it leads to, %s", (_order, payload) => {
      const parts = partsOf(rowOf(payload));

      expect(labels(parts.places)).toEqual(places);
      expect(labels(parts.people)).toEqual(["Автор"]);
      expect(parts.rest).toEqual([]);
    });
  });

  // A detail with no field behind it (a payload the client cannot read) is
  // still said, under the title.
  it("keeps every detail it cannot pair, in the rest", () => {
    const parts = partsOf({
      details: [detail("Игра", "Ведьмак"), detail("Мастер", "SolohinLex")],
      payload: undefined,
    });

    expect(labels(parts.rest)).toEqual(["Игра", "Мастер"]);
    expect(parts.places).toEqual([]);
    expect(parts.people).toEqual([]);
  });
});
