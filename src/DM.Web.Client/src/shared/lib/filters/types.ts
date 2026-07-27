/**
 * Shared Filter Types
 *
 * Unified types for all filters on the site.
 * Ensure consistency of URL params, API params and the UI.
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
