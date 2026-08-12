import { createAuthorDateFilter } from "@/shared/lib/filters";
import type { AuthorDateFilter } from "@/shared/lib/filters";
import type { SortByValue } from "./types";
import { SORT_OPTIONS, DEFAULT_SORT } from "./types";

/**
 * Topics filter: the shared search/authors/created-range/sort composable, with
 * the forum board sort options and the reader topics-per-page preference.
 * Everything else it does is described in createAuthorDateFilter.
 */
export type TopicsFilterComposable = AuthorDateFilter<SortByValue>;

export const useTopicsFilter = createAuthorDateFilter<SortByValue>({
  name: "useTopicsFilter",
  sortOptions: SORT_OPTIONS,
  defaultSortBy: DEFAULT_SORT.sortBy,
  defaultSortOrder: DEFAULT_SORT.sortOrder,
  pagingPreference: "topicsPerPage",
});
