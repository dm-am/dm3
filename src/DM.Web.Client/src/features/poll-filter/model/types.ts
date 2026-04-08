import type { PollSortBy, PollStatus } from "@/entities/poll";

/**
 * Filter state for polls
 */
export interface PollsFilterState {
  /** Status filter: active, closed, or all (empty string) */
  status: PollStatus | "";

  /** Text search query (poll title) */
  search: string;

  /** Filter by minimum start date (ISO string, date only) */
  startsFrom: string;

  /** Filter by maximum start date (ISO string, date only) */
  startsTo: string;

  /** Filter by minimum end date (ISO string, date only) */
  endsFrom: string;

  /** Filter by maximum end date (ISO string, date only) */
  endsTo: string;

  /** Poll type filter: true = anonymous, false = public, "" = all */
  pollType: "anonymous" | "public" | "";

  /** Sort field */
  sortBy: PollSortBy;

  /** Sort direction */
  sortOrder: "asc" | "desc";
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: PollsFilterState = {
  status: "",
  search: "",
  startsFrom: "",
  startsTo: "",
  endsFrom: "",
  endsTo: "",
  pollType: "",
  sortBy: "status",
  sortOrder: "asc",
};

/**
 * Status options (PascalCase to match C# API)
 */
export const STATUS_OPTIONS = [
  { value: "" as const, label: "Все" },
  { value: "Pending" as const, label: "Ожидают начала" },
  { value: "Active" as const, label: "Активные" },
  { value: "Closed" as const, label: "Завершенные" },
] as const;

/**
 * Poll type filter options
 */
export const POLL_TYPE_OPTIONS = [
  { value: "" as const, label: "Все" },
  { value: "anonymous" as const, label: "Анонимные" },
  { value: "public" as const, label: "Публичные" },
] as const;

/**
 * Sort options
 */
export const SORT_OPTIONS = [
  {
    value: "status" as const,
    label: "Статус",
    hint: "Сначала активные, потом остальные",
    defaultDirection: "asc" as const,
  },
  {
    value: "starts" as const,
    label: "Начало",
    hint: "По дате начала",
    defaultDirection: "desc" as const,
  },
  {
    value: "ends" as const,
    label: "Окончание",
    hint: "По дате окончания",
    defaultDirection: "asc" as const,
  },
] as const;
