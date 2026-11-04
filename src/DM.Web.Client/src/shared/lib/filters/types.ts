export type SortDirection = "asc" | "desc";

export interface SortOption {
  /** Used in the URL and the API. */
  value: string;
  label: string;
  /** Shown in the dropdown. */
  hint?: string;
  /** When this sort is selected. */
  defaultDirection: SortDirection;
}

/** Both dates optional, YYYY-MM-DD. */
export interface DateRange {
  from: string | null;
  to: string | null;
}

export interface BaseFilterState {
  search: string;
  sortBy: string;
  sortOrder: SortDirection;
}

export interface BaseSearchParams {
  search?: string;
  sortBy?: string;
  sortOrder?: string;
  number?: number;
  size?: number;
}
