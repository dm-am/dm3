// =============================================================================
// TYPES FOR TOPICS FILTER
// =============================================================================

import type {
  SortOption,
  AuthorDateFilterState,
  AuthorDateSearchParams,
} from "@/shared/lib/filters";

/**
 * Sort options for topics list.
 *
 * Mirrors the backend's TopicRepository sort switch — adding a value here
 * without a matching SQL case would silently fall through to the default
 * "lastActivity" ordering.
 */
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
    label: "Лайки",
    hint: "По количеству лайков",
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

/** Filter state stored in URL. The shape is declared once, in shared. */
export type TopicsFilterState = AuthorDateFilterState<SortByValue>;

/** API search parameters for topics. */
export type TopicsSearchParams = AuthorDateSearchParams;

/**
 * Default filter state for comparison
 */
export const DEFAULT_SORT = {
  sortBy: "lastActivity" as SortByValue,
  sortOrder: "desc" as const,
};
