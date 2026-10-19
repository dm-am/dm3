import type { PollSortBy, PollStatus } from "@/entities/poll";

export interface PollsFilterState {
  /** "" is all. */
  status: PollStatus | "";

  search: string;

  /** ISO date only, as are the three below. */
  startsFromUtc: string;

  startsToUtc: string;

  endsFromUtc: string;

  endsToUtc: string;

  /** "" is all. */
  pollType: "anonymous" | "public" | "";

  sortBy: PollSortBy;

  sortOrder: "asc" | "desc";
}

export const DEFAULT_FILTER_STATE: PollsFilterState = {
  status: "",
  search: "",
  startsFromUtc: "",
  startsToUtc: "",
  endsFromUtc: "",
  endsToUtc: "",
  pollType: "",
  sortBy: "status",
  sortOrder: "asc",
};

// PascalCase, as the API spells them.
export const STATUS_OPTIONS = [
  { value: "" as const, label: "Все", hint: "Все опросы" },
  {
    value: "Pending" as const,
    label: "Ожидают начала",
    hint: "Еще не начались",
  },
  { value: "Active" as const, label: "Активные", hint: "Сейчас идут" },
  { value: "Closed" as const, label: "Завершенные", hint: "Уже закончились" },
] as const;

export const POLL_TYPE_OPTIONS = [
  { value: "" as const, label: "Все", hint: "Все опросы" },
  { value: "anonymous" as const, label: "Анонимные", hint: "Голоса скрыты" },
  { value: "public" as const, label: "Публичные", hint: "Видно кто голосовал" },
] as const;

export const SORT_OPTIONS = [
  {
    value: "status" as const,
    label: "Статус",
    hint: "Сначала запланированные, затем активные и завершенные",
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
