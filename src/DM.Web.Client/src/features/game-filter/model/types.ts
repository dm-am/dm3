/**
 * Recruitment filter values
 * - any: show all games regardless of recruitment
 * - open: show games with open recruitment (any type)
 * - initial: show games with first-time recruitment
 * - subsequent: show games with subsequent recruitment ("донабор")
 * - closed: show games with closed recruitment
 */
export type RecruitmentFilter =
  | "any"
  | "open"
  | "initial"
  | "subsequent"
  | "closed";

/**
 * Closed reason filter values (includes 'any' option)
 * - None: closed without specific reason
 * - Finished: game completed
 * - Frozen: game frozen/paused indefinitely
 */
export type ClosedReasonFilter = "any" | "None" | "Finished" | "Frozen";

/**
 * Status values
 */
export type StatusValue = "Draft" | "Active" | "Closed";

/**
 * Filter state for games (new architecture)
 *
 * Logic:
 * - Status: Single status filter (or null for all)
 * - RecruitmentFilter: Sub-filter for Active (ignored if Active not selected)
 * - ClosedReasonFilter: Sub-filter for Closed (ignored if Closed not selected)
 * - RequiredTags: AND (game must have ALL)
 * - ExcludedTags: NOR (game must have NONE)
 */
export interface GamesFilterState {
  /** Text search query */
  search: string;

  /** Selected status (single selection, null = all) */
  status: StatusValue | null;

  /** Recruitment filter for Active games */
  recruitmentFilter: RecruitmentFilter;

  /** Closed reason filter for Closed games */
  closedReasonFilter: ClosedReasonFilter;

  /** Required tag IDs - game must have ALL (AND logic) */
  requiredTags: Set<number>;

  /** Excluded tag IDs - game must have NONE (NOR logic) */
  excludedTags: Set<number>;

  /** Hosts filter - master OR assistant (OR logic) */
  hostUsernames: Set<string>;

  /** Created date range start (inclusive) */
  createdFromUtc: string | null;

  /** Created date range end (inclusive) */
  createdToUtc: string | null;

  /** Activated date range start (inclusive). Games without ActivatedUtc are excluded. */
  activatedFromUtc: string | null;

  /** Activated date range end (inclusive). Games without ActivatedUtc are excluded. */
  activatedToUtc: string | null;

  /** Closed date range start (inclusive). Games without ClosedUtc are excluded. */
  closedFromUtc: string | null;

  /** Closed date range end (inclusive). Games without ClosedUtc are excluded. */
  closedToUtc: string | null;

  /** Recruitment started date range start (inclusive). Games without RecruitmentStartedUtc are excluded. */
  recruitmentStartedFromUtc: string | null;

  /** Recruitment started date range end (inclusive). Games without RecruitmentStartedUtc are excluded. */
  recruitmentStartedToUtc: string | null;

  /** Sort field */
  sortBy: string;

  /** Sort direction */
  sortOrder: "asc" | "desc";
}

/**
 * API search parameters for games
 * Note: "any" values are excluded from API calls (means "don't filter")
 */
export interface GamesSearchParams {
  search?: string;

  /** Status filter */
  status?: string;

  /** Recruitment filter for Active games (excludes "any") */
  recruitmentFilter?: "open" | "initial" | "subsequent" | "closed";

  /** Closed reason filter for Closed games (only actual reasons, not "any") */
  closedReasonFilter?: "None" | "Finished" | "Frozen";

  /** Required tag IDs - AND logic */
  requiredTags?: number[];

  /** Excluded tag IDs - NOR logic */
  excludedTags?: number[];

  /** Hosts filter - master OR assistant (OR logic) */
  hostUsernames?: string[];

  /** Created date range start (ISO string) */
  createdFromUtc?: string;

  /** Created date range end (ISO string) */
  createdToUtc?: string;

  /** Activated date range start (ISO string) */
  activatedFromUtc?: string;

  /** Activated date range end (ISO string) */
  activatedToUtc?: string;

  /** Closed date range start (ISO string) */
  closedFromUtc?: string;

  /** Closed date range end (ISO string) */
  closedToUtc?: string;

  /** Recruitment started date range start (ISO string) */
  recruitmentStartedFromUtc?: string;

  /** Recruitment started date range end (ISO string) */
  recruitmentStartedToUtc?: string;

  /** Sort field */
  sortBy?: string;

  /** Sort order */
  sortOrder?: string;

  /** Page number (1-indexed entity position) */
  number?: number;

  /** Page size (items per page) */
  size?: number;
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: GamesFilterState = {
  search: "",
  status: null,
  recruitmentFilter: "any",
  closedReasonFilter: "any",
  requiredTags: new Set(),
  excludedTags: new Set(),
  hostUsernames: new Set(),
  createdFromUtc: null,
  createdToUtc: null,
  activatedFromUtc: null,
  activatedToUtc: null,
  closedFromUtc: null,
  closedToUtc: null,
  recruitmentStartedFromUtc: null,
  recruitmentStartedToUtc: null,
  sortBy: "created",
  sortOrder: "desc",
};

/**
 * Status filter options
 */
export const STATUS_OPTIONS = [
  { value: "Draft" as const, label: "Оформляется" },
  { value: "Active" as const, label: "Идет игра" },
  { value: "Closed" as const, label: "Закрыта" },
] as const;

/**
 * Recruitment filter options (sub-filter for Active)
 * Nested structure:
 * - any ("Любой")
 * - open ("Набор игроков") - parent for initial/subsequent
 *   - initial ("Первый набор")
 *   - subsequent ("Донабор игроков")
 * - closed ("Набор закрыт")
 */
export const RECRUITMENT_FILTER_OPTIONS = [
  { value: "any" as const, label: "Любой" },
  { value: "open" as const, label: "Набор игроков", isParent: true },
  { value: "initial" as const, label: "Первый набор", indent: true },
  { value: "subsequent" as const, label: "Донабор игроков", indent: true },
  { value: "closed" as const, label: "Набор закрыт" },
] as const;

/**
 * Closed reason filter options (sub-filter for Closed)
 */
export const CLOSED_REASON_FILTER_OPTIONS = [
  { value: "any" as const, label: "Все" },
  { value: "None" as const, label: "Без флагов" },
  { value: "Frozen" as const, label: "Заморожена" },
  { value: "Finished" as const, label: "Завершена" },
] as const;

/**
 * Sort options (ordered as in menu)
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
