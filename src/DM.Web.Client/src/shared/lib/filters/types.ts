/**
 * Shared Filter Types
 *
 * Унифицированные типы для всех фильтров на сайте.
 * Обеспечивают единообразие URL-параметров, API-параметров и UI.
 */

// =============================================================================
// SORT TYPES
// =============================================================================

/** Sort direction */
export type SortDirection = "asc" | "desc";

/** Sort option configuration */
export interface SortOption {
  /** Value used in URL and API */
  value: string;
  /** Display label */
  label: string;
  /** Hint shown in dropdown */
  hint?: string;
  /** Default direction when this sort is selected */
  defaultDirection: SortDirection;
}

// =============================================================================
// DATE RANGE TYPES
// =============================================================================

/** Date range (both dates are optional, YYYY-MM-DD format) */
export interface DateRange {
  from: string | null;
  to: string | null;
}

/** Named date range for multi-date filters */
export interface NamedDateRange extends DateRange {
  /** Field name (e.g., "created", "activated", "closed") */
  field: string;
}

// =============================================================================
// FILTER OPTION TYPES
// =============================================================================

/** Generic filter option */
export interface FilterOption<T = string> {
  value: T;
  label: string;
  hint?: string;
  /** For hierarchical options */
  indent?: boolean;
  /** Parent option (has children) */
  isParent?: boolean;
}

/** Status option with possible sub-filters */
export interface StatusOption extends FilterOption<string> {
  /** Sub-filter key (e.g., "recruitment" for Active games) */
  subFilterKey?: string;
}

// =============================================================================
// FILTER ITEM TYPES (for hierarchical dropdown)
// =============================================================================

/** Root-level filter item in dropdown */
export interface FilterMenuItem {
  /** Filter key (used internally) */
  key: string;
  /** Display label */
  label: string;
  /** Description hint */
  hint?: string;
  /** Icon name (optional) */
  icon?: string;
}

// =============================================================================
// BASE FILTER STATE
// =============================================================================

/** Common fields present in all filter states */
export interface BaseFilterState {
  /** Text search query */
  search: string;
  /** Sort field */
  sortBy: string;
  /** Sort direction */
  sortOrder: SortDirection;
}

/** Common fields in all search params */
export interface BaseSearchParams {
  search?: string;
  sortBy?: string;
  sortOrder?: string;
  number?: number;
  size?: number;
}

// =============================================================================
// FILTER BUBBLE TYPE
// =============================================================================

/** Active filter bubble for display */
export interface FilterBubble {
  /** Unique key for this bubble */
  key: string;
  /** Prefix label (e.g., "Автор:", "Тег:") */
  prefix: string;
  /** Value to display */
  value: string;
  /** Action to remove this filter */
  onRemove: () => void;
}

// =============================================================================
// AUTHOR/HOST FILTER CONSTANTS
// =============================================================================

/**
 * Унифицированные имена параметров для фильтра по автору/ведущему:
 * - URL: `hosts` (comma-separated)
 * - API: `hostUsernames` (array)
 * - State: `hostUsernames` (Set<string>)
 *
 * Для простых фильтров (Topics) используется одиночный `author` (string)
 */
export const HOST_URL_PARAM = "hosts";
export const HOST_API_PARAM = "hostUsernames";

// =============================================================================
// DATE FORMAT CONSTANTS
// =============================================================================

/**
 * Унифицированный формат дат:
 * - URL: YYYY-MM-DD (без времени, для читаемости)
 * - API: ISO 8601 с временем (YYYY-MM-DDTHH:mm:ssZ)
 *   - From: T00:00:00Z (начало дня)
 *   - To: T23:59:59Z (конец дня)
 * - Display: DD.MM.YYYY или DD.MM.YYYY [в] HH:mm
 */
export const DATE_URL_FORMAT = "YYYY-MM-DD";
export const DATE_DISPLAY_FORMAT = "DD.MM.YYYY";
export const DATETIME_DISPLAY_FORMAT = "DD.MM.YYYY [в] HH:mm";

// =============================================================================
// PAGINATION CONSTANTS
// =============================================================================

/**
 * Унифицированные имена параметров пагинации:
 * - URL: `number` (page number, 1-indexed)
 * - API: `number` (same) + `size` (items per page)
 */
export const PAGE_URL_PARAM = "number";
export const PAGE_SIZE_PARAM = "size";
