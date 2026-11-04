import { DAY, formatDate, siteTime } from "@/shared/lib/utils/datetime";
// Pure functions of chat message rendering, shared by both chat views.

import dayjs from "dayjs";
import type { Message } from "@/shared/api/models/common/message";
import type { User } from "@/shared/api/models/common/user";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";

export interface DateSeparator {
  type: "date-separator";
  date: string;
  formattedDate: string;
}

export interface MessageWithContinuation extends Message {
  isContinuation: boolean;
}

export type MessageOrSeparator = MessageWithContinuation | DateSeparator;

/** Messages from same author within this period are grouped as continuation */
const CONTINUATION_TIME_LIMIT_MINUTES = 5;

/**
 * The author's edit window, in minutes: MessagingConfiguration and PostReviewService
 * refuse past it, for posts and comments it is the client's own. EditWindowShould
 * compares the numbers.
 */
export const EDIT_TIME_LIMIT_MINUTES = 15;

/** Format message time as "HH:mm", on the site's clock (siteTime). */
export function formatChatTime(dateStr: string): string {
  return siteTime(dateStr).format("HH:mm");
}

/** "Сегодня", "Вчера", or DD.MM.YYYY (CODE_STYLE, "Формат отображения дат"). */
function formatSeparatorDate(dateStr: string): string {
  // A day of the calendar, taken as it was handed. "Today" is the day on the
  // site's clock, the one the messages under the separator are counted in.
  const date = dayjs(dateStr);
  const today = dayjs(siteTime().format(DAY));
  const yesterday = today.subtract(1, "day");

  if (date.isSame(today, "day")) return "Сегодня";
  if (date.isSame(yesterday, "day")) return "Вчера";
  return formatDate(dateStr);
}

/** Type guard for date separator items */
export function isDateSeparator(
  item: MessageOrSeparator,
): item is DateSeparator {
  return "type" in item && (item as DateSeparator).type === "date-separator";
}

/**
 * Inserts date separators and marks messages of one author within
 * `continuationMinutes` as continuations.
 */
export function groupMessagesWithSeparators(
  messages: Message[],
  continuationMinutes = CONTINUATION_TIME_LIMIT_MINUTES,
): MessageOrSeparator[] {
  if (!messages.length) return [];

  const result: MessageOrSeparator[] = [];
  let lastDate: string | null = null;
  let lastAuthor: string | null = null;
  let lastMessageTime: dayjs.Dayjs | null = null;

  for (const msg of messages) {
    // The day on the site's clock, so a message sent at 23:30 in the zone of
    // the account stands under that day and not the device's next one.
    const msgDate = siteTime(msg.createdUtc).format(DAY);
    const msgTime = dayjs(msg.createdUtc);

    // Insert date separator when date changes
    if (lastDate !== msgDate) {
      result.push({
        type: "date-separator",
        date: msgDate,
        formattedDate: formatSeparatorDate(msgDate),
      });
      lastDate = msgDate;
      lastAuthor = null;
      lastMessageTime = null;
    }

    // Detect continuation (same author, not removed, within time limit)
    const isContinuation = !!(
      !msg.isRemoved &&
      lastAuthor === msg.author?.username &&
      lastMessageTime &&
      msgTime.diff(lastMessageTime, "minute") < continuationMinutes
    );

    result.push({ ...msg, isContinuation });

    if (!msg.isRemoved) {
      lastAuthor = msg.author?.username ?? null;
      lastMessageTime = msgTime;
    } else {
      lastAuthor = null;
      lastMessageTime = null;
    }
  }

  return result;
}

/** A virtualizer's row, declared structurally to keep @tanstack out (VirtualItem fits). */
export interface VirtualRowGeometry {
  index: number;
  key: string | number | bigint;
  start: number;
}

interface ChatVirtualRowBase {
  /** Stable key for v-for, already stringified. */
  key: string;
  /** Offset in px inside the virtualizer's spacer. */
  start: number;
  /** Position of the item in the list the row was built from. */
  index: number;
}

interface ChatSeparatorRow extends ChatVirtualRowBase {
  kind: "separator";
  separator: DateSeparator;
}

interface ChatMessageRow extends ChatVirtualRowBase {
  kind: "message";
  message: MessageWithContinuation;
}

export type ChatVirtualRow = ChatSeparatorRow | ChatMessageRow;

/**
 * Each visible row with its item already narrowed by `kind`, so the template
 * and its inline handlers read a typed value without casts. A row whose item is
 * gone (the list just shrank) is dropped.
 */
export function toChatVirtualRows(
  items: MessageOrSeparator[],
  rows: VirtualRowGeometry[],
): ChatVirtualRow[] {
  const result: ChatVirtualRow[] = [];
  for (const row of rows) {
    const item = items[row.index];
    if (!item) continue;
    const base = { key: String(row.key), start: row.start, index: row.index };
    result.push(
      isDateSeparator(item)
        ? { ...base, kind: "separator", separator: item }
        : { ...base, kind: "message", message: item },
    );
  }
  return result;
}

/** The names the likers' tooltip spells out: a line or two, the rest counted. */
const LIKERS_NAMED = 5;

/**
 * The likers' tooltip, "Нравится: Anna, Boris", past five names
 * "Нравится: A, B, C, D, E и еще 4"; empty while nobody likes it.
 */
export function getLikesTooltip(
  likes: readonly Pick<User, "username">[],
): string {
  if (!likes?.length) return "";
  const names = likes.map((u) => u.username);
  const named = names.slice(0, LIKERS_NAMED).join(", ");
  const rest = names.length - LIKERS_NAMED;
  return rest > 0 ? `Нравится: ${named} и еще ${rest}` : `Нравится: ${named}`;
}

/** Check if user is online based on their last activity timestamp */
export function isUserOnline(
  lastActivityUtc: string | null | undefined,
  thresholdMinutes = ONLINE_THRESHOLD_MINUTES,
): boolean {
  if (!lastActivityUtc) return false;
  return (
    dayjs().diff(dayjs(lastActivityUtc), "minute", true) <= thresholdMinutes
  );
}

/**
 * Each author's latest activity among the messages, by username: ISO strings
 * compare in time order, and an author's older messages carry older stamps.
 */
export function latestActivityByUsername(
  messages: readonly Pick<Message, "author">[],
): Map<string, string> {
  const latest = new Map<string, string>();
  for (const { author } of messages) {
    const username = author?.username;
    const seen = author?.lastActivityUtc;
    if (!username || !seen) continue;
    const known = latest.get(username);
    if (!known || seen > known) latest.set(username, seen);
  }
  return latest;
}

/** The mark the server puts on the spoiler it wraps a chat image in. */
const IMAGE_GATE = '[data-bb-gate="image"]';

/**
 * A global chat message for the full layout: the server's image spoilers
 * (data-bb-gate="image") give way to the image; an author's spoiler stays. A
 * string transform on a detached template.
 */
export function openImageGates(html: string): string {
  if (!html || !html.includes("data-bb-gate")) return html;
  const template = document.createElement("template");
  template.innerHTML = html;
  template.content.querySelectorAll(IMAGE_GATE).forEach((element) => {
    if (element.classList.contains("spoiler-head")) element.remove();
    else element.replaceWith(...Array.from(element.childNodes));
  });
  return template.innerHTML;
}
