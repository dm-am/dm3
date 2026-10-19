// Shared date/time helpers (SSOT for sitewide date formatting)

import dayjs, { type ConfigType, type Dayjs } from "dayjs";
import utc from "dayjs/plugin/utc";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { accountTimeZone } from "./timeZone";

// A fixed offset on a moment (utcOffset), which is what siteTime puts a moment
// into. Idempotent: dayjs installs a plugin once however often it is asked.
dayjs.extend(utc);

/**
 * The spellings the site renders a date in, kept here only
 * (DateFormatSpellingShould). The space after "в" is a no-break one: the
 * preposition wraps with the time.
 */
export const DATE_FORMAT = "DD.MM.YYYY";
export const DATE_TIME_FORMAT = "DD.MM.YYYY [в]\u00a0HH:mm";
export const DAY_MONTH_FORMAT = "DD.MM";
export const DAY_MONTH_TIME_FORMAT = "DD.MM [в]\u00a0HH:mm";

/** A day with no time of day, the spelling of a date input and of a day key: "2026-09-27". */
export const DAY = "YYYY-MM-DD";

/** A day of the calendar with no time of day: "2026-09-27". */
const CALENDAR_DAY = /^\d{4}-\d{2}-\d{2}$/;

/** One formatter per zone: building one is the slow part of the question. */
const zoneFormats = new Map<string, Intl.DateTimeFormat | null>();

/**
 * How far `zone` stands from UTC at the moment `at`, in minutes, by the
 * browser's zone database for that moment; null for an unknown zone.
 */
function offsetAt(zone: string, at: number): number | null {
  let format = zoneFormats.get(zone);
  if (format === undefined) {
    try {
      format = new Intl.DateTimeFormat("en-US", {
        timeZone: zone,
        hourCycle: "h23",
        year: "numeric",
        month: "numeric",
        day: "numeric",
        hour: "numeric",
        minute: "numeric",
        second: "numeric",
      });
    } catch {
      // A zone name this browser does not know. It can arrive from the
      // account, written by another browser or an older zone database.
      format = null;
    }
    zoneFormats.set(zone, format);
  }
  if (!format) return null;

  const parts = format.formatToParts(new Date(at));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((one) => one.type === type)?.value);
  const wall = Date.UTC(
    part("year"),
    part("month") - 1,
    part("day"),
    part("hour") % 24,
    part("minute"),
    part("second"),
  );
  return Math.round((wall - Math.floor(at / 1000) * 1000) / 60000);
}

/**
 * A moment as the site shows it: on the account's clock for a signed-in
 * reader, the device's for a guest; every printed date goes through here. An
 * empty zone is UTC. A bare calendar day has no zone and is returned as that
 * day. Without an argument, now.
 */
export function siteTime(date?: ConfigType): Dayjs {
  const moment = dayjs(date);
  const zone = accountTimeZone.value;
  if (zone === null || !moment.isValid()) return moment;
  if (typeof date === "string" && CALENDAR_DAY.test(date)) return moment;

  const offset = offsetAt(zone, moment.valueOf());
  return offset === null ? moment : moment.utcOffset(offset);
}

/**
 * A moment in the site's clock (siteTime) in the given spelling: one of the
 * formats above, or a pattern of dayjs.
 */
export function formatSiteTime(date: ConfigType, format: string): string {
  return siteTime(date).format(format);
}

/** The value of a date-and-time field (input type="datetime-local"). */
const INPUT_FORMAT = "YYYY-MM-DDTHH:mm";
const INPUT_VALUE = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/;

/**
 * A moment as the value of a date-and-time field on the site's clock, the clock
 * the reader fills it in by. Without an argument, now. fromSiteInput is the
 * way back.
 */
export function toSiteInput(date?: ConfigType): string {
  return siteTime(date).format(INPUT_FORMAT);
}

/**
 * The moment a date-and-time field names, read on the site's clock, as ISO in
 * UTC; null for a cleared field.
 */
export function fromSiteInput(value: string): string | null {
  const match = INPUT_VALUE.exec(value);
  if (!match) return null;
  const [year, month, day, hour, minute, second] = match
    .slice(1)
    .map((part) => Number(part ?? 0));

  // The wall time as UTC, moved by the offset asked twice, so a summer-time
  // change between the two lands on the zone's own side.
  const wall = Date.UTC(year, month - 1, day, hour, minute, second);
  const zone = accountTimeZone.value;
  const first = zone === null ? null : offsetAt(zone, wall);

  // A guest, or a zone this browser does not know: the device's clock, as
  // siteTime reads it.
  if (zone === null || first === null) {
    return new Date(year, month - 1, day, hour, minute, second).toISOString();
  }
  const offset = offsetAt(zone, wall - first * 60000) ?? first;
  return new Date(wall - offset * 60000).toISOString();
}

/**
 * Format a date string as "DD.MM.YYYY" (site time, siteTime), date only. The
 * empty token is a parameter: a chip renders "", a cell the missing-value
 * token. Parsed by dayjs: `new Date` reads a bare date as UTC midnight.
 */
export function formatDate(
  dateStr: string | null | undefined,
  emptyToken = VALUE_UNAVAILABLE,
): string {
  if (!dateStr) return emptyToken;
  return formatSiteTime(dateStr, DATE_FORMAT);
}

/**
 * Format a date string as "DD.MM.YYYY в HH:mm" (site time, siteTime).
 * Returns the sitewide missing-value token for null/undefined/empty input.
 */
export function formatDateFull(dateStr: string | null | undefined): string {
  if (!dateStr) return VALUE_UNAVAILABLE;
  return formatSiteTime(dateStr, DATE_TIME_FORMAT);
}

/**
 * Start of the week the site means by "за неделю": seven days back, not the
 * calendar Monday, which emptied the week's blocks every Monday morning.
 */
export function getWeekStartUtc(): string {
  const start = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000);
  return start.toISOString();
}
