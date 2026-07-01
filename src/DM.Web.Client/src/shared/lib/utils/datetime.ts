// Shared date/time helpers (SSOT for sitewide date formatting)

import dayjs from "dayjs";

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
