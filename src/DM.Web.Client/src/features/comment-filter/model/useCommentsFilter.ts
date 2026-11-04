import { createAuthorDateFilter } from "@/shared/lib/filters";
import type { SortByValue } from "./types";
import { SORT_OPTIONS, DEFAULT_SORT } from "./types";

export const useCommentsFilter = createAuthorDateFilter<SortByValue>({
  name: "useCommentsFilter",
  sortOptions: SORT_OPTIONS,
  defaultSortBy: DEFAULT_SORT.sortBy,
  defaultSortOrder: DEFAULT_SORT.sortOrder,
});
