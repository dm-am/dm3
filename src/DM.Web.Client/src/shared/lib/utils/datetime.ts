// Shared date/time helpers (SSOT for sitewide date formatting)

import dayjs from "dayjs";

/**
 * Format a date string as "DD.MM.YYYY" (local time) — date only, no time.
 *
 * The empty token is a parameter so that exactly one place owns the format
 * string while a caller that needs a falsy placeholder can ask for one: a filter
 * chip renders "" where a table cell renders "—", and that is the only thing
 * the four separate implementations this replaced actually disagreed about.
 *
 * Parsing goes through dayjs on purpose. A bare "YYYY-MM-DD" is local midnight
 * to dayjs and UTC midnight to `new Date`, so the naive route shifts the day by
 * one for anyone west of Greenwich.
 */
export function formatDate(
  dateStr: string | null | undefined,
  emptyToken = "—",
): string {
  if (!dateStr) return emptyToken;
  return dayjs(dateStr).format("DD.MM.YYYY");
}

/**
 * Format a date string as "DD.MM.YYYY в HH:mm" (local time).
 * Returns "—" for null/undefined/empty input.
 */
export function formatDateFull(dateStr: string | null | undefined): string {
  if (!dateStr) return "—";
  return dayjs(dateStr).format("DD.MM.YYYY [в] HH:mm");
}

/**
 * Get the start of the current calendar week (Monday 00:00:00 UTC)
 * as an ISO string. Week runs Monday to Sunday.
 */
export function getWeekStartUtc(): string {
  const now = new Date();
  const dayOfWeek = now.getUTCDay(); // 0 = Sunday, 1 = Monday, ..., 6 = Saturday
  // Days since Monday: Sunday goes back 6 days, Monday = 0, Tuesday = 1, etc.
  const daysSinceMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const monday = new Date(now);
  monday.setUTCDate(now.getUTCDate() - daysSinceMonday);
  monday.setUTCHours(0, 0, 0, 0);
  return monday.toISOString();
}
