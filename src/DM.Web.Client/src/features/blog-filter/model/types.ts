import type { BlogStatus } from "@/entities/blog";
import type { SortDirection } from "@/shared/lib/filters/types";

export type StatusFilter = "any" | BlogStatus;

/** The filters the menu of the bar offers. */
export type BlogsFilterKey = "status" | "host" | "dates";

export interface BlogsFilterState {
  search: string;

  status: StatusFilter;

  /** Author or assistant. */
  hostUsernames: Set<string>;

  createdFromUtc: string | null;

  createdToUtc: string | null;

  /** Blogs without ActivatedUtc are excluded. */
  activatedFromUtc: string | null;

  activatedToUtc: string | null;

  /** Blogs without ClosedUtc are excluded. */
  closedFromUtc: string | null;

  closedToUtc: string | null;

  sortBy: string;

  sortOrder: SortDirection;
}

// The store's own shape, taken from the entity that owns the query.
export type { BlogsSearchParams } from "@/entities/blog";

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

export const STATUS_OPTIONS = [
  { value: "any" as const, label: "Все статусы", hint: "Показать все блоги" },
  {
    value: "Draft" as const,
    label: "Оформляется",
    hint: "Оформляющиеся блоги",
  },
  { value: "Active" as const, label: "Открыт", hint: "Активные блоги" },
  { value: "Closed" as const, label: "Закрыт", hint: "Закрытые блоги" },
] as const;

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
