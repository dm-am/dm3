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
): NotificationDetail => ({ label, value, isMoment }) as NotificationDetail;

/** The row as the list hands it over, its details left unbranded here. */
const partsOf = (row: { details: NotificationDetail[]; payload: unknown }) =>
  notificationParts(row as Pick<UserNotification, "details" | "payload">);

const labels = (details: NotificationDetail[]) =>
  details.map(({ label }) => label);

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
