/**
 * Sort field options for reviews
 */
export type ReviewSortBy = "created" | "author";

/**
 * Filter state for reviews
 */
export interface ReviewsFilterState {
  /** Text search query (review text, author username) */
  search: string;

  /** Sort field */
  sortBy: ReviewSortBy;

  /** Sort direction */
  sortOrder: "asc" | "desc";
}

/**
 * API search parameters for reviews
 */
export interface ReviewsSearchParams {
  search?: string;
  sortBy?: ReviewSortBy;
  sortOrder?: "asc" | "desc";
  number?: number;
  size?: number;
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: ReviewsFilterState = {
  search: "",
  sortBy: "created",
  sortOrder: "desc",
};

/**
 * Sort options
 */
export const SORT_OPTIONS = [
  {
    value: "created" as const,
    label: "Дата",
    hint: "По дате создания",
    defaultDirection: "desc" as const,
  },
  {
    value: "author" as const,
    label: "Автор",
    hint: "По имени автора",
    defaultDirection: "asc" as const,
  },
] as const;
