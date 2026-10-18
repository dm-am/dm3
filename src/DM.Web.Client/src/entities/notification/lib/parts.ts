import type {
  NotificationDetail,
  UserNotification,
} from "@/shared/api/models/notifications";

/**
 * The details of a notification sorted into who, where and the rest, for a
 * list that writes each of them in a column of its own.
 *
 * The server words every detail (label and value) and keeps the payload it
 * words them from beside them, in the same order: the details are the fields
 * of the payload a person reads, less the identifiers, the addressee and the
 * time of the row (NotificationWording.ReadDetails). So each detail is paired
 * with the payload field it was written from by walking both in order, and
 * the name of that field says what it is: a field ending in "Username" is a
 * person, the title of a game, room, topic, board, blog or publication is a
 * place. The words stay the server's, and the client names no event.
 */
export interface NotificationParts {
  /** The people the notification names, in the order the server wrote them. */
  people: NotificationDetail[];
  /** Where it happened; the first is the place the notification leads to. */
  places: NotificationDetail[];
  /** Everything else the server wrote under the title. */
  rest: NotificationDetail[];
}

/**
 * The field of the addressee: the reader's own name, carried for the link and
 * never written out, so it is not a person of the row.
 */
const ADDRESSEE = "username";

const PERSON = /username$/i;

const PLACES = new Set([
  "gametitle",
  "roomtitle",
  "topictitle",
  "boardtitle",
  "blogtitle",
  "publicationtitle",
]);

/** A payload value as the server writes it into a detail. */
function textOf(value: unknown): string {
  if (typeof value === "string") return value;
  if (typeof value === "number") return String(value);
  if (typeof value === "boolean") return value ? "Да" : "Нет";
  if (value == null) return "";
  return JSON.stringify(value);
}

type Kind = keyof NotificationParts;

function kindOf(field: string): Kind {
  const name = field.toLowerCase();
  if (name !== ADDRESSEE && PERSON.test(name)) return "people";
  if (PLACES.has(name)) return "places";
  return "rest";
}

export function notificationParts(
  notification: Pick<UserNotification, "details" | "payload">,
): NotificationParts {
  const parts: NotificationParts = { people: [], places: [], rest: [] };
  const payload: unknown = notification.payload;
  const fields =
    payload && typeof payload === "object"
      ? Object.entries(payload as Record<string, unknown>).filter(
          ([name]) => name.toLowerCase() !== ADDRESSEE,
        )
      : [];

  let next = 0;
  for (const detail of notification.details) {
    let kind: Kind = "rest";
    for (let i = next; i < fields.length; i++) {
      const [name, value] = fields[i];
      if (textOf(value) !== detail.value) continue;
      kind = kindOf(name);
      next = i + 1;
      break;
    }
    parts[kind].push(detail);
  }
  return parts;
}
