import { createAuthorDateFilter } from "@/shared/lib/filters";
import type { SortByValue } from "./types";
import { SORT_OPTIONS, DEFAULT_SORT } from "./types";

/**
 * Comments filter: the shared search/authors/created-range/sort composable,
 * with the discussion sort options and the reader comments-per-page
 * preference. Everything else it does is described in createAuthorDateFilter.
 */
export const useCommentsFilter = createAuthorDateFilter<SortByValue>({
  name: "useCommentsFilter",
  sortOptions: SORT_OPTIONS,
  defaultSortBy: DEFAULT_SORT.sortBy,
  defaultSortOrder: DEFAULT_SORT.sortOrder,
  pagingPreference: "commentsPerPage",
});
