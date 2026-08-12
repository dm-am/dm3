import type {
  SortOption,
  AuthorDateFilterState,
  AuthorDateSearchParams,
} from "@/shared/lib/filters";

/**
 * Sort options for comments list
 */
export const SORT_OPTIONS: readonly SortOption[] = [
  {
    value: "created",
    label: "Дата",
    hint: "По времени создания (старые сверху)",
    defaultDirection: "asc",
  },
  {
    value: "likes",
    label: "Популярность",
    hint: "По количеству лайков",
    defaultDirection: "desc",
  },
] as const;

export type SortByValue = (typeof SORT_OPTIONS)[number]["value"];

/** Filter state stored in URL. The shape is declared once, in shared. */
export type CommentsFilterState = AuthorDateFilterState<SortByValue>;

/** API search parameters for comments. */
export type CommentsSearchParams = AuthorDateSearchParams;

/**
 * Default sort settings for comparison
 */
export const DEFAULT_SORT = {
  sortBy: "created" as SortByValue,
  sortOrder: "asc" as const,
};
