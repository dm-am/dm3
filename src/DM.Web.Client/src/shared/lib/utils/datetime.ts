// Shared date/time helpers (SSOT for sitewide date formatting)

import dayjs from "dayjs";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

/**
 * Format a date string as "DD.MM.YYYY" (local time), date only, no time.
 *
 * The empty token is a parameter so that exactly one place owns the format
 * string while a caller that needs a falsy placeholder can ask for one: a filter
 * chip renders "" where a table cell renders the sitewide missing-value token,
 * and that is the only thing the four separate implementations this replaced
 * actually disagreed about.
 *
 * Parsing goes through dayjs on purpose. A bare "YYYY-MM-DD" is local midnight
 * to dayjs and UTC midnight to `new Date`, so the naive route shifts the day by
 * one for anyone west of Greenwich.
 */
export function formatDate(
  dateStr: string | null | undefined,
  emptyToken = VALUE_UNAVAILABLE,
): string {
  if (!dateStr) return emptyToken;
  return dayjs(dateStr).format("DD.MM.YYYY");
}

/**
 * Format a date string as "DD.MM.YYYY в HH:mm" (local time).
 * Returns the sitewide missing-value token for null/undefined/empty input.
 */
export function formatDateFull(dateStr: string | null | undefined): string {
  if (!dateStr) return VALUE_UNAVAILABLE;
  return dayjs(dateStr).format("DD.MM.YYYY [в] HH:mm");
}

/**
 * Start of the week the site means by "за неделю", as an ISO string.
 *
 * Seven days back from now, not the calendar Monday. The calendar boundary
 * emptied every block built on it for the first hours of every Monday: the best
 * post of the week and the pulse both went blank while the site had a week of
 * posts behind them, and the seed had to clamp its own data to the boundary to
 * hide it. A reader who asks for the week means the last seven days, and this
 * window never has a hole in it.
 */
export function getWeekStartUtc(): string {
  const start = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000);
  return start.toISOString();
}
