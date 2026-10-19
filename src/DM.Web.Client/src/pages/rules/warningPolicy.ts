import { pluralize } from "@/shared/lib/utils/pluralize";

/**
 * The numbers of WarningPolicy (DM.Domain.Moderation) the rules state in words. The server sends
 * the threshold only to moderators (Violator.pointsThreshold), and the rules are public, so the page
 * keeps copies; the spec next to this file holds them equal to the server's.
 */

/** WarningPolicy.AutoBanPointsThreshold: the total of points the site bans at by itself. */
export const AUTO_BAN_POINTS = 6;

/** WarningPolicy.AutoBanDurationHours: how long that ban runs. */
export const AUTO_BAN_HOURS = 48;

/** WarningPolicy.MaxPointsPerWarning: the most one warning costs, the top of the penalty table. */
export const MAX_WARNING_POINTS = 6;

/** "6 баллов", "1 балл": the number with its noun in agreement. */
export function points(count: number): string {
  return `${count} ${pluralize(count, "балл", "балла", "баллов")}`;
}

/** "48 часов", "24 часа". */
export function hours(count: number): string {
  return `${count} ${pluralize(count, "час", "часа", "часов")}`;
}
