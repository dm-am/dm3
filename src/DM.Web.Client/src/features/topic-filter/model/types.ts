// =============================================================================
// TYPES FOR TOPICS FILTER
// =============================================================================

import type {
  SortOption,
  BaseFilterState,
  BaseSearchParams,
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

/**
 * Filter state stored in URL
 * Follows unified naming convention:
 * - Date params use "Utc" suffix (createdFromUtc, createdToUtc)
 * - Authors use Set<string> for multi-select (like GamesFilter hosts)
 */
export interface TopicsFilterState extends BaseFilterState {
  authors: Set<string>;
  createdFromUtc: string | null; // YYYY-MM-DD format
  createdToUtc: string | null; // YYYY-MM-DD format
}

/**
 * API search parameters for topics
 * Date params are converted to ISO 8601 with time before sending
 */
export interface TopicsSearchParams extends BaseSearchParams {
  authors?: string[];
  createdFromUtc?: string; // ISO 8601 (YYYY-MM-DDTHH:mm:ssZ)
  createdToUtc?: string; // ISO 8601 (YYYY-MM-DDTHH:mm:ssZ)
}

/**
 * Default filter state for comparison
 */
export const DEFAULT_SORT = {
  sortBy: "lastActivity" as SortByValue,
  sortOrder: "desc" as const,
};
