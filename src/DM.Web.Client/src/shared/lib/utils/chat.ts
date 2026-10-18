import { formatDate, siteTime } from "@/shared/lib/utils/datetime";
/**
 * Pure utility functions for chat message rendering.
 * Shared between GlobalChatPage, ChatView, and future chat components.
 *
 * All functions are stateless — no refs, no composables, no side effects.
 */

import dayjs from "dayjs";
import type { Message } from "@/shared/api/models/common/message";
import type { User } from "@/shared/api/models/common/user";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { pluralize } from "./pluralize";

// =============================================================================
// Types
// =============================================================================

export interface DateSeparator {
  type: "date-separator";
  date: string;
  formattedDate: string;
}

export interface MessageWithContinuation extends Message {
  isContinuation: boolean;
}

export type MessageOrSeparator = MessageWithContinuation | DateSeparator;

// =============================================================================
// Constants
// =============================================================================

/** Messages from same author within this period are grouped as continuation */
export const CONTINUATION_TIME_LIMIT_MINUTES = 5;

/** Users active within this period are shown as online (single source: shared/lib/constants/user) */
export { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";

/** Time limit for editing own messages (minutes) */
export const EDIT_TIME_LIMIT_MINUTES = 15;

/** Max messages in memory to prevent unbounded growth */
export const MAX_MESSAGES = 500;

// =============================================================================
// Formatting
// =============================================================================

/** Format message time as "HH:mm", on the site's clock (siteTime). */
export function formatChatTime(dateStr: string): string {
  return siteTime(dateStr).format("HH:mm");
}

/**
 * Format chat date separator: "Сегодня", "Вчера", or canonical
 * `DD.MM.YYYY` (matches docs/conventions/CODE_STYLE.md → "Формат
 * отображения дат"). "Сегодня" / "Вчера" remain as human-readable
 * shortcuts for the two most recent days — they are NOT date
 * formats, they are calendar-relative labels.
 */
export function formatSeparatorDate(dateStr: string): string {
  // A day of the calendar, taken as it was handed. "Today" is the day on the
  // site's clock, the one the messages under the separator are counted in.
  const date = dayjs(dateStr);
  const today = dayjs(siteTime().format("YYYY-MM-DD"));
  const yesterday = today.subtract(1, "day");

  if (date.isSame(today, "day")) return "Сегодня";
  if (date.isSame(yesterday, "day")) return "Вчера";
  return formatDate(dateStr);
}

// =============================================================================
// Type Guards
// =============================================================================

/** Type guard for date separator items */
export function isDateSeparator(
  item: MessageOrSeparator,
): item is DateSeparator {
  return "type" in item && (item as DateSeparator).type === "date-separator";
}

// =============================================================================
// Message Grouping
// =============================================================================

/**
 * Group messages with date separators and continuation flags.
 *
 * Inserts DateSeparator between messages from different dates.
 * Marks messages from the same author within `continuationMinutes` as continuation
 * (to hide avatar/author for compact layout).
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
    const msgDate = siteTime(msg.createdUtc).format("YYYY-MM-DD");
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

// =============================================================================
// Virtual Rows
// =============================================================================

/**
 * Geometry a virtualizer reports for one visible row: where to draw it and
 * which item of the list it stands for. Declared structurally so this module
 * keeps its "no dependencies" promise — @tanstack's VirtualItem satisfies it.
 */
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

export interface ChatSeparatorRow extends ChatVirtualRowBase {
  kind: "separator";
  separator: DateSeparator;
}

export interface ChatMessageRow extends ChatVirtualRowBase {
  kind: "message";
  message: MessageWithContinuation;
}

export type ChatVirtualRow = ChatSeparatorRow | ChatMessageRow;

/**
 * Resolve every visible row to the item it draws, with the two kinds of item
 * already told apart.
 *
 * A virtualized chat list is mixed — messages and date separators — and the
 * virtualizer returns geometry with an index, nothing more. Re-reading the
 * array at that index inside a template binding leaves the expression typed as
 * the union, so each binding needs a cast to reach the member it wants; both
 * chat pages did that dozens of times per row, with type checking off for all
 * of them. Applying the guard once here and carrying the narrowed value on the
 * row is what gives the template something typed to read, and the `kind`
 * discriminant is what keeps that narrowing alive inside the inline handlers
 * Vue compiles into closures — a null check on a member would not.
 *
 * A row whose item is gone is dropped: the virtualizer's window can outlive a
 * list that just shrank (a jump to another archive date), and such a row has
 * nothing to draw.
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

// =============================================================================
// Likes
// =============================================================================

/** Build tooltip text for likes badge */
export function getLikesTooltip(likes: User[]): string {
  if (!likes?.length) return "Нравится";

  const names = likes.map((u) => u.username);
  const count = names.length;

  if (count === 1) return `${names[0]} оценил(а) это`;
  if (count === 2) return `${names[0]} и ${names[1]} оценили это`;
  if (count <= 5)
    return `${names.slice(0, -1).join(", ")} и ${names[count - 1]} оценили это`;
  const rest = count - 3;
  const verb = pluralize(rest, "оценил это", "оценили это", "оценили это");
  return `${names.slice(0, 3).join(", ")} и еще ${rest} ${verb}`;
}

// =============================================================================
// Online Status
// =============================================================================

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

// =============================================================================
// Images of the global chat
// =============================================================================

/** The mark the server puts on the spoiler it wraps a chat image in. */
const IMAGE_GATE = '[data-bb-gate="image"]';

/**
 * A message of the global chat as the full layout shows it.
 *
 * The server puts every image of such a message behind a spoiler of its own,
 * marked data-bb-gate="image" on the button and on the block, because the
 * layout is the reader's choice and lives on the reader's device. The compact
 * layout keeps that spoiler; the full one shows the image where it stands, as
 * a post does: the button goes and the block gives way to what it holds. A
 * spoiler the author wrote carries no mark and stays closed in both layouts.
 *
 * A transform of a string, like trimHtmlWhitespace: it parses into a detached
 * template and touches nothing on the page.
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
