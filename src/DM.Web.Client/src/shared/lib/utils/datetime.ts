// Shared date/time helpers (SSOT for sitewide date formatting)

import dayjs, { type ConfigType, type Dayjs } from "dayjs";
import utc from "dayjs/plugin/utc";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { accountTimeZone } from "./timeZone";

// A fixed offset on a moment (utcOffset), which is what siteTime puts a moment
// into. Idempotent: dayjs installs a plugin once however often it is asked.
dayjs.extend(utc);

/**
 * The two spellings the site renders a date in, and the two short forms used
 * where the year is implied by context.
 *
 * Written here and nowhere else. The pattern used to be a literal at every call
 * site — seventeen of them across nine files — and a literal repeated that many
 * times is not a format but a coincidence: one of the copies said "DD.MM" where
 * its neighbour two lines down said "DD.MM.YYYY", and nothing in either file
 * says which was meant. DateFormatSpellingShould keeps them here.
 *
 * The space after "в" is a no-break one (U+00A0): the preposition goes to the
 * next line together with the time. In a narrow cell the line broke after it,
 * "01.01.2020 в" on one line and "03:00" alone on the next.
 */
export const DATE_FORMAT = "DD.MM.YYYY";
export const DATE_TIME_FORMAT = "DD.MM.YYYY [в]\u00a0HH:mm";
export const DAY_MONTH_FORMAT = "DD.MM";
export const DAY_MONTH_TIME_FORMAT = "DD.MM [в]\u00a0HH:mm";

/** A day of the calendar with no time of day: "2026-09-27". */
const CALENDAR_DAY = /^\d{4}-\d{2}-\d{2}$/;

/** One formatter per zone: building one is the slow part of the question. */
const zoneFormats = new Map<string, Intl.DateTimeFormat | null>();

/**
 * How far the clock of `zone` stands from UTC at the moment `at`, in
 * minutes, or null for a zone this browser does not know.
 *
 * Asked of the browser's own zone database for that very moment, so a zone on
 * summer time is read at the offset it had then and not the one it has today.
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
 * A moment as the site shows it: on the clock of the account for a signed-in
 * reader, on the clock of the device for a guest. The owner's word, and the
 * one place it is kept: every date and time the site prints goes through
 * here, the formatters below and HumanDate included. An empty zone on the
 * account is UTC, as the letters read it.
 *
 * A bare day of the calendar ("2026-09-27", a filter's bound, a separator of
 * the chat) is not a moment and has no zone: it is the same day on every
 * clock, and it is returned as that day. Moved to a zone, the midnight it was
 * read as would fall on the previous day west of the device.
 *
 * Without an argument, now.
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
 * A moment as the value of a date-and-time field, on the site's clock
 * (siteTime). Without an argument, now.
 *
 * The field holds a time of day with no zone, and the reader fills it in by the
 * clock the site shows them: the card of a poll printed "20:00" by the account,
 * so its editor has to open on 20:00 as well, not on the device's reading of
 * the same moment. fromSiteInput is the way back.
 */
export function toSiteInput(date?: ConfigType): string {
  return siteTime(date).format(INPUT_FORMAT);
}

/**
 * The moment a date-and-time field names, read on the site's clock, as ISO in
 * UTC; null for a field that holds no date and time (cleared).
 *
 * `new Date(value)` read the field on the device's clock, so a reader whose
 * account is in Tokyo on a device in Moscow typed 20:00 and sent 20:00 Moscow:
 * six hours off the time the site then printed back to them.
 */
export function fromSiteInput(value: string): string | null {
  const match = INPUT_VALUE.exec(value);
  if (!match) return null;
  const [year, month, day, hour, minute, second] = match
    .slice(1)
    .map((part) => Number(part ?? 0));

  // The wall time taken as if it were UTC, then moved by the zone's offset.
  // The offset is asked twice, at the wall time and at the moment the first
  // answer gives, so a change to or from summer time between the two lands on
  // the side the zone itself keeps.
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
 * Format a date string as "DD.MM.YYYY" (site time, siteTime), date only, no time.
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
