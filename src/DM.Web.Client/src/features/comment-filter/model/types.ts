import type { SortOption } from "@/shared/lib/filters";

/** The filters the menu of the bar offers. */
export type CommentsFilterKey = "author" | "dateRange";

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
    hint: "По числу отметок Нравится",
    defaultDirection: "desc",
  },
] as const;

export type SortByValue = (typeof SORT_OPTIONS)[number]["value"];

export const DEFAULT_SORT = {
  sortBy: "created" as SortByValue,
  sortOrder: "asc" as const,
};
