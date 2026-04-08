import type { BlogStatus } from "@/entities/blog";

/**
 * Status filter values (includes "any" option)
 */
export type StatusFilter = "any" | BlogStatus;

/**
 * Filter state for blogs
 */
export interface BlogsFilterState {
  /** Text search query (title, description) */
  search: string;

  /** Status filter */
  status: StatusFilter;

  /** Hosts filter - author (owner) OR assistant (OR logic) */
  hostUsernames: Set<string>;

  /** Creation date range start (inclusive) */
  createdFromUtc: string | null;

  /** Creation date range end (inclusive) */
  createdToUtc: string | null;

  /** Activated date range start (inclusive). Blogs without ActivatedUtc are excluded. */
  activatedFromUtc: string | null;

  /** Activated date range end (inclusive). Blogs without ActivatedUtc are excluded. */
  activatedToUtc: string | null;

  /** Closed date range start (inclusive). Blogs without ClosedUtc are excluded. */
  closedFromUtc: string | null;

  /** Closed date range end (inclusive). Blogs without ClosedUtc are excluded. */
  closedToUtc: string | null;

  /** Sort field */
  sortBy: string;

  /** Sort direction */
  sortOrder: "asc" | "desc";
}

/**
 * API search parameters for blogs
 */
export interface BlogsSearchParams {
  search?: string;
  status?: BlogStatus;
  /** Hosts filter - author (owner) OR assistant (OR logic) */
  hostUsernames?: string[];
  createdFromUtc?: string;
  createdToUtc?: string;
  activatedFromUtc?: string;
  activatedToUtc?: string;
  closedFromUtc?: string;
  closedToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
  number?: number;
  size?: number;
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: BlogsFilterState = {
  search: "",
  status: "any",
  hostUsernames: new Set(),
  createdFromUtc: null,
  createdToUtc: null,
  activatedFromUtc: null,
  activatedToUtc: null,
  closedFromUtc: null,
  closedToUtc: null,
  sortBy: "created",
  sortOrder: "desc",
};

/**
 * Status filter options (short form, unified with games)
 */
export const STATUS_OPTIONS = [
  { value: "any" as const, label: "Все статусы", hint: "Показать все блоги" },
  { value: "Draft" as const, label: "Оформляется", hint: "Подготавливаемые блоги" },
  { value: "Active" as const, label: "Открыт", hint: "Активные блоги" },
  { value: "Closed" as const, label: "Закрыт", hint: "Закрытые блоги" },
] as const;

/**
 * Sort options (unified with games)
 */
export const SORT_OPTIONS = [
  {
    value: "title",
    label: "Название",
    hint: "По алфавиту",
    defaultDirection: "asc" as const,
  },
  {
    value: "status",
    label: "Статус блога",
    hint: "По статусу и дате создания/открытия/закрытия",
    defaultDirection: "asc" as const,
  },
  {
    value: "popularity",
    label: "Популярность",
    hint: "По количеству активных пользователей среди читателей",
    defaultDirection: "desc" as const,
  },
  {
    value: "created",
    label: "Дата создания",
    hint: "По дате создания блога",
    defaultDirection: "desc" as const,
  },
  {
    value: "activated",
    label: "Дата открытия",
    hint: "По дате открытия блога, неоткрытые в конце",
    defaultDirection: "desc" as const,
  },
  {
    value: "closed",
    label: "Дата закрытия",
    hint: "По дате закрытия блога, незакрытые в конце",
    defaultDirection: "desc" as const,
  },
] as const;
