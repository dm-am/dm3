import type { SortOption } from "@/shared/lib/filters";

/** The filters the menu of the bar offers. */
export type TopicsFilterKey = "author" | "dateRange";

// Mirrors TopicRepository's sort switch: a value without a SQL case falls through to lastActivity.
export const SORT_OPTIONS: readonly SortOption[] = [
  {
    value: "lastActivity",
    label: "Последняя активность",
    hint: "По дате последнего комментария",
    defaultDirection: "desc",
  },
  {
    value: "created",
    label: "Дата создания",
    hint: "По дате создания топика",
    defaultDirection: "desc",
  },
  {
    value: "likes",
    label: "Нравится",
    hint: "По числу отметок Нравится",
    defaultDirection: "desc",
  },
  {
    value: "title",
    label: "Заголовок",
    hint: "По алфавиту",
    defaultDirection: "asc",
  },
] as const;

export type SortByValue = (typeof SORT_OPTIONS)[number]["value"];

export const DEFAULT_SORT = {
  sortBy: "lastActivity" as SortByValue,
  sortOrder: "desc" as const,
};
