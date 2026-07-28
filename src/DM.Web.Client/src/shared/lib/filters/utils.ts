/**
 * Shared Filter Utilities
 *
 * Unified helpers for working with filters:
 * - Date parsing and formatting
 * - Conversion between URL, State and API formats
 */

import { formatDate } from "@/shared/lib/utils/datetime";
import type { DateRange, SortDirection } from "./types";

// =============================================================================
// QUERY VALUE NORMALIZATION
// =============================================================================

/**
 * Normalize a raw route query value (which may include `null` entries,
 * e.g. from repeated query params like `?foo&foo=bar`) into the
 * `string | string[] | undefined` shape expected by the parse* helpers below.
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

// =============================================================================
// DATE UTILITIES
// =============================================================================

/** Regex for YYYY-MM-DD format */
const ISO_DATE_REGEX = /^\d{4}-\d{2}-\d{2}$/;

/** Regex for full ISO 8601 with time */
const ISO_DATETIME_REGEX = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

/**
 * Validate YYYY-MM-DD date string
 */
export function isValidDate(value: string | null | undefined): value is string {
  if (!value) return false;
  if (!ISO_DATE_REGEX.test(value)) return false;
  const date = new Date(value);
  return !isNaN(date.getTime());
}

/**
 * Parse date from URL query parameter.
 * Accepts both YYYY-MM-DD and ISO 8601 formats.
 * Returns YYYY-MM-DD string or null.
 */
export function parseDateFromUrl(
  value: string | string[] | undefined,
): string | null {
  if (!value || Array.isArray(value)) return null;

  // If it's a full ISO datetime, extract date part
  if (ISO_DATETIME_REGEX.test(value)) {
    return value.substring(0, 10);
  }

  // If it's YYYY-MM-DD, validate and return
  if (isValidDate(value)) {
    return value;
  }

  return null;
}

/**
 * Convert YYYY-MM-DD date to ISO 8601 for API (start of day).
 * Returns string like "2024-01-15T00:00:00Z"
 */
export function dateToApiStart(date: string | null): string | undefined {
  if (!date || !isValidDate(date)) return undefined;
  return `${date}T00:00:00Z`;
}

/**
 * Convert YYYY-MM-DD date to ISO 8601 for API (end of day).
 * Returns string like "2024-01-15T23:59:59Z"
 */
export function dateToApiEnd(date: string | null): string | undefined {
  if (!date || !isValidDate(date)) return undefined;
  return `${date}T23:59:59Z`;
}

/**
 * Format YYYY-MM-DD to DD.MM.YYYY for display
 */
export function formatDateForDisplay(date: string | null | undefined): string {
  return formatDate(date, "");
}

/**
 * Format date range for display in filter bubble
 */
export function formatDateRangeForDisplay(range: DateRange): string {
  const from = formatDateForDisplay(range.from);
  const to = formatDateForDisplay(range.to);

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

// =============================================================================
// STRING UTILITIES
// =============================================================================

/**
 * Parse string from URL query parameter
 */
export function parseStringFromUrl(
  value: string | string[] | undefined,
  maxLength?: number,
): string {
  if (!value) return "";
  const str = Array.isArray(value) ? value[0] : value;
  if (!str) return "";
  return maxLength ? str.slice(0, maxLength) : str;
}

// =============================================================================
// SORT UTILITIES
// =============================================================================

/**
 * Parse sort direction from URL
 */
export function parseSortDirection(
  value: string | string[] | undefined,
  defaultDir: SortDirection = "desc",
): SortDirection {
  if (!value) return defaultDir;
  const str = Array.isArray(value) ? value[0] : value;
  return str === "asc" || str === "desc" ? str : defaultDir;
}

/**
 * Validate sort field against allowed options
 */
export function validateSortField(
  value: string | string[] | undefined,
  validValues: readonly string[],
  defaultValue: string,
): string {
  if (!value) return defaultValue;
  const str = Array.isArray(value) ? value[0] : value;
  return validValues.includes(str ?? "") ? str! : defaultValue;
}

// =============================================================================
// QUERY BUILDING UTILITIES
// =============================================================================

/**
 * Build URL query object, excluding undefined/null/empty values
 */
export function buildQuery(
  params: Record<string, string | number | boolean | null | undefined>,
): Record<string, string> {
  const query: Record<string, string> = {};

  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    query[key] = String(value);
  }

  return query;
}
