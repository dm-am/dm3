import type { SortOption, BaseFilterState, BaseSearchParams } from "@/shared/lib/filters";

/**
 * Sort options for comments list
 */
export const SORT_OPTIONS: readonly SortOption[] = [
  { value: "created", label: "Дата", hint: "По времени создания (новые сверху)", defaultDirection: "desc" },
  { value: "likes", label: "Популярность", hint: "По количеству лайков", defaultDirection: "desc" },
] as const;

export type SortByValue = (typeof SORT_OPTIONS)[number]["value"];

/**
 * Filter state stored in URL
 * Follows unified naming convention:
 * - Date params use "Utc" suffix (createdFromUtc, createdToUtc)
 * - Authors use Set<string> for multi-select (like GamesFilter hosts)
 */
export interface CommentsFilterState extends BaseFilterState {
  authors: Set<string>;
  createdFromUtc: string | null; // YYYY-MM-DD format
  createdToUtc: string | null;   // YYYY-MM-DD format
  sortBy: SortByValue;
}

/**
 * API search parameters for comments
 * Date params are converted to ISO 8601 with time before sending
 */
export interface CommentsSearchParams extends BaseSearchParams {
  authors?: string[];
  createdFromUtc?: string; // ISO 8601 (YYYY-MM-DDTHH:mm:ssZ)
  createdToUtc?: string;   // ISO 8601 (YYYY-MM-DDTHH:mm:ssZ)
}

/**
 * Default sort settings for comparison
 */
export const DEFAULT_SORT = {
  sortBy: "created" as SortByValue,
  sortOrder: "desc" as const,
};
