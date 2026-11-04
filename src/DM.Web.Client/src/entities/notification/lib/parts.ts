import type {
  NotificationDetail,
  UserNotification,
} from "@/shared/api/models/notifications";

// A detail is paired with the payload field it was written from by walking both in order.
// The destination comes from the server's mark: jsonb orders the payload keys by length.
export interface NotificationParts {
  people: NotificationDetail[];
  /** The first is the place the notification leads to. */
  places: NotificationDetail[];
  rest: NotificationDetail[];
}

// The reader's own name, carried for the link and never written out.
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
    if (detail.isDestination) parts.places.unshift(detail);
    else parts[kind].push(detail);
  }
  return parts;
}
