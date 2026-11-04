import { formatDate } from "@/shared/lib/utils/datetime";
import type { DateRange, SortDirection } from "./types";

/**
 * A raw route query value (null entries come from `?foo&foo=bar`) in the
 * shape the parse helpers take.
 */
export function toQueryValue(
  value: string | null | (string | null)[] | undefined,
): string | string[] | undefined {
  if (value === null || value === undefined) return undefined;
  if (Array.isArray(value)) {
    return value.filter((v): v is string => v !== null);
  }
  return value;
}

const ISO_DATE_REGEX = /^\d{4}-\d{2}-\d{2}$/;

const ISO_DATETIME_REGEX = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

export function isValidDate(value: string | null | undefined): value is string {
  if (!value) return false;
  if (!ISO_DATE_REGEX.test(value)) return false;
  const date = new Date(value);
  return !isNaN(date.getTime());
}

/** YYYY-MM-DD from a query value in that form or in full ISO 8601, else null. */
export function parseDateFromUrl(
  value: string | string[] | undefined,
): string | null {
  if (!value || Array.isArray(value)) return null;

  if (ISO_DATETIME_REGEX.test(value)) {
    return value.substring(0, 10);
  }

  if (isValidDate(value)) {
    return value;
  }

  return null;
}

/** YYYY-MM-DD as the API's start of that day. */
export function dateToApiStart(date: string | null): string | undefined {
  if (!date || !isValidDate(date)) return undefined;
  return `${date}T00:00:00Z`;
}

/** YYYY-MM-DD as the API's end of that day. */
export function dateToApiEnd(date: string | null): string | undefined {
  if (!date || !isValidDate(date)) return undefined;
  return `${date}T23:59:59Z`;
}

/** A numeric range for a filter bubble, with the prepositions of a count. */
export function formatNumberRangeForDisplay(
  from: number | null | undefined,
  to: number | null | undefined,
): string {
  const hasFrom = from !== null && from !== undefined;
  const hasTo = to !== null && to !== undefined;

  if (hasFrom && hasTo) {
    return `${from} — ${to}`;
  }
  if (hasFrom) {
    return `от ${from}`;
  }
  if (hasTo) {
    return `до ${to}`;
  }
  return "";
}

export function formatDateRangeForDisplay(range: DateRange): string {
  const from = formatDate(range.from, "");
  const to = formatDate(range.to, "");

  if (from && to) {
    return `${from} — ${to}`;
  }
  if (from) {
    return `с ${from}`;
  }
  if (to) {
    return `до ${to}`;
  }
  return "";
}

/** The first of a repeated query parameter. */
const firstOf = (value: string | string[]): string | undefined =>
  Array.isArray(value) ? value[0] : value;

export function parseStringFromUrl(
  value: string | string[] | undefined,
  maxLength?: number,
): string {
  if (!value) return "";
  const str = firstOf(value);
  if (!str) return "";
  return maxLength ? str.slice(0, maxLength) : str;
}

/**
 * The page of `?number=`, or undefined for anything that is not one: callers
 * leave it out of the request or fall back with `?? 1`.
 */
export function parsePageNumber(
  value: string | null | (string | null)[] | undefined,
): number | undefined {
  if (!value) return undefined;
  const parsed = parseInt(String(value), 10);
  return !isNaN(parsed) && parsed > 0 ? parsed : undefined;
}

export function parseSortDirection(
  value: string | string[] | undefined,
  defaultDir: SortDirection = "desc",
): SortDirection {
  if (!value) return defaultDir;
  const str = firstOf(value);
  return str === "asc" || str === "desc" ? str : defaultDir;
}

export function validateSortField(
  value: string | string[] | undefined,
  validValues: readonly string[],
  defaultValue: string,
): string {
  if (!value) return defaultValue;
  const str = firstOf(value);
  return validValues.includes(str ?? "") ? str! : defaultValue;
}
