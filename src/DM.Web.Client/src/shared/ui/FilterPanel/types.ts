/**
 * Filter state for three-state toggle
 */
export type FilterState = "neutral" | "include" | "exclude";

/**
 * Toggle option for StatusToggle
 */
export interface ToggleOption {
  /** Unique value for the option */
  value: string;
  /** Display label */
  label: string;
  /** Whether this option has children (dropdown) */
  hasChildren?: boolean;
  /** Child options for dropdown */
  children?: ToggleOption[];
}

/**
 * Active filter for FilterPills display
 */
export interface ActiveFilter {
  /** Unique key */
  key: string;
  /** Display label */
  label: string;
  /** Filter state */
  state: "include" | "exclude";
}

/**
 * Sort option for SortSelect
 */
export interface SortOption {
  /** Unique value for sorting */
  value: string;
  /** Display label */
  label: string;
  /** Default direction for this sort */
  defaultDirection?: "asc" | "desc";
}
