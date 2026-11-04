export type RecruitmentFilter =
  | "any"
  | "open"
  | "initial"
  | "subsequent"
  | "closed";

export type ClosedReasonFilter = "any" | "None" | "Finished" | "Frozen";

export type StatusValue = "Draft" | "Active" | "Closed";

export interface GamesFilterState {
  search: string;

  /** Null for all. */
  status: StatusValue | null;

  /** Ignored unless the status is Active. */
  recruitmentFilter: RecruitmentFilter;

  /** Ignored unless the status is Closed. */
  closedReasonFilter: ClosedReasonFilter;

  requiredTags: Set<number>;

  excludedTags: Set<number>;

  /** Master or assistant. */
  hostUsernames: Set<string>;

  createdFromUtc: string | null;

  createdToUtc: string | null;

  /** Games without ActivatedUtc are excluded. */
  activatedFromUtc: string | null;

  activatedToUtc: string | null;

  /** Games without ClosedUtc are excluded. */
  closedFromUtc: string | null;

  closedToUtc: string | null;

  /** Games without RecruitmentStartedUtc are excluded. */
  recruitmentStartedFromUtc: string | null;

  recruitmentStartedToUtc: string | null;

  sortBy: string;

  sortOrder: "asc" | "desc";
}

// The entity's own shape: what the filter never sets stays absent.
export type { GamesSearchParams } from "@/entities/game";

export const STATUS_OPTIONS = [
  { value: "Draft" as const, label: "Оформляется" },
  { value: "Active" as const, label: "Идет игра" },
  { value: "Closed" as const, label: "Закрыта" },
] as const;

export const RECRUITMENT_FILTER_OPTIONS = [
  { value: "any" as const, label: "Любой" },
  { value: "open" as const, label: "Набор игроков", isParent: true },
  { value: "initial" as const, label: "Первый набор", indent: true },
  { value: "subsequent" as const, label: "Донабор игроков", indent: true },
  { value: "closed" as const, label: "Набор закрыт" },
] as const;

export const CLOSED_REASON_FILTER_OPTIONS = [
  { value: "any" as const, label: "Все" },
  { value: "None" as const, label: "Без флагов" },
  { value: "Frozen" as const, label: "Заморожена" },
  { value: "Finished" as const, label: "Завершена" },
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
    label: "Статус игры",
    hint: "По статусу и дате создания/начала/закрытия",
    defaultDirection: "asc" as const,
  },
  {
    value: "popularity",
    label: "Популярность",
    hint: "По количеству активных пользователей среди текущих игроков и читателей",
    defaultDirection: "desc" as const,
  },
  {
    value: "created",
    label: "Дата создания",
    hint: "По дате создания игры",
    defaultDirection: "desc" as const,
  },
  {
    value: "activated",
    label: "Дата начала",
    hint: "По дате начала игры, неначавшиеся в конце",
    defaultDirection: "desc" as const,
  },
  {
    value: "availableslots",
    label: "Дата последнего набора",
    hint: "По дате начала последнего набора игроков, игры без набора в конце",
    defaultDirection: "desc" as const,
  },
  {
    value: "closed",
    label: "Дата закрытия",
    hint: "По дате закрытия игры, незакрытые в конце",
    defaultDirection: "desc" as const,
  },
] as const;
