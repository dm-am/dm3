import { UserRole } from "@/entities/user";

// Single source of truth for the API search param shape lives in the entity
// layer (FSD: feature imports from entity). Re-exported here so existing
// `@/features/user-filter` consumers keep working.
export type { UsersSearchParams } from "@/entities/user";

/**
 * Activity filter values
 * - active: only users with recent activity
 * - inactive: users without recent activity
 * - all: all registered users
 */
export type ActivityFilter = "active" | "inactive" | "all";

/**
 * Online sub-filter for Active users only
 * - all: all active users
 * - online: only currently online users
 */
export type OnlineFilter = "all" | "online";

/**
 * Role filter values (includes "all" option)
 */
export type RoleFilter = "all" | UserRole;

/**
 * Experience filter (applies to all roles)
 * - all: all users
 * - newbie: only newbies (< 100 posts)
 * - experienced: only experienced (>= 100 posts)
 */
export type ExperienceFilter = "all" | "newbie" | "experienced";

/**
 * Filter state for users
 */
export interface UsersFilterState {
  /** Text search query (username, name) */
  search: string;

  /** Activity filter */
  activity: ActivityFilter;

  /** Online sub-filter (only for active users) */
  onlineFilter: OnlineFilter;

  /** Role filter (single selection) */
  role: RoleFilter;

  /** Experience filter (applies to all roles) */
  experience: ExperienceFilter;

  /** Rating range filter (min) */
  ratingMin: number | null;

  /** Rating range filter (max) */
  ratingMax: number | null;

  /** Games hosting range filter (min) */
  gamesHostingMin: number | null;

  /** Games hosting range filter (max) */
  gamesHostingMax: number | null;

  /** Games playing range filter (min) */
  gamesPlayingMin: number | null;

  /** Games playing range filter (max) */
  gamesPlayingMax: number | null;

  /** Blogs hosting range filter (min) */
  blogsHostingMin: number | null;

  /** Blogs hosting range filter (max) */
  blogsHostingMax: number | null;

  /** Registration date range start (inclusive) */
  registeredFromUtc: string | null;

  /** Registration date range end (inclusive) */
  registeredToUtc: string | null;

  /** Sort field */
  sortBy: string;

  /** Sort direction */
  sortOrder: "asc" | "desc";
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: UsersFilterState = {
  search: "",
  activity: "all",
  onlineFilter: "all",
  role: "all",
  experience: "all",
  ratingMin: null,
  ratingMax: null,
  gamesHostingMin: null,
  gamesHostingMax: null,
  gamesPlayingMin: null,
  gamesPlayingMax: null,
  blogsHostingMin: null,
  blogsHostingMax: null,
  registeredFromUtc: null,
  registeredToUtc: null,
  sortBy: "lastActivity",
  sortOrder: "desc",
};

/**
 * Activity filter options
 * Note: "online" is a special value that sets activity=active + onlineFilter=online
 */
export const ACTIVITY_OPTIONS = [
  { value: "online" as const, label: "Онлайн", hint: "Сейчас на сайте" },
  {
    value: "active" as const,
    label: "Активные",
    hint: "Были на сайте за последний месяц",
  },
  {
    value: "inactive" as const,
    label: "Неактивные",
    hint: "Не были на сайте более месяца",
  },
  {
    value: "all" as const,
    label: "Все пользователи",
    hint: "Все зарегистрированные пользователи",
  },
] as const;

/**
 * Role filter options (ordered from lowest to highest privilege)
 */
export const ROLE_OPTIONS = [
  { value: "all" as const, label: "Все роли", hint: "Без фильтрации по роли" },
  {
    value: UserRole.RegularUser,
    label: "Пользователь",
    hint: "Обычные пользователи",
  },
  {
    value: UserRole.Mentor,
    label: "Наставник",
    hint: "Наставники для новичков",
  },
  { value: UserRole.Moderator, label: "Модератор", hint: "Модераторы" },
  {
    value: UserRole.SeniorModerator,
    label: "Старший модератор",
    hint: "Старшие модераторы",
  },
  {
    value: UserRole.Admin,
    label: "Администратор",
    hint: "Администраторы сайта",
  },
] as const;

/**
 * Experience filter options (applies to all roles)
 */
export const EXPERIENCE_OPTIONS = [
  { value: "all" as const, label: "Все", hint: "Без фильтрации по опыту" },
  { value: "newbie" as const, label: "Новички", hint: "Менее 100 постов" },
  { value: "experienced" as const, label: "Опытные", hint: "100+ постов" },
] as const;

/**
 * The hint under a filter name: the list the filter opens, without the "all"
 * option, which tells a reader nothing they cannot already see. Typed by hand
 * the hint drifted from the list it described — three roles of six, and a form
 * "Админ" that exists nowhere else on the site.
 */
export function optionsHint(
  options: readonly { value: unknown; label: string }[],
): string {
  return options
    .filter((option) => option.value !== "all")
    .map((option) => option.label)
    .join(", ");
}

/**
 * Sort options
 */
export const SORT_OPTIONS = [
  {
    value: "username",
    label: "Имя",
    hint: "По алфавиту",
    defaultDirection: "asc" as const,
  },
  {
    value: "rating",
    label: "Рейтинг",
    hint: "По сумме полученных оценок постов",
    defaultDirection: "desc" as const,
  },
  {
    value: "lastActivity",
    label: "Активность",
    hint: "По последней активности",
    defaultDirection: "desc" as const,
  },
  {
    value: "registered",
    label: "Регистрация",
    hint: "По дате регистрации",
    defaultDirection: "desc" as const,
  },
  {
    value: "popularity",
    label: "Популярность",
    hint: "По количеству активных пользователей среди подписчиков",
    defaultDirection: "desc" as const,
  },
  {
    value: "gamesHosting",
    label: "Игры (ведущий)",
    hint: "По количеству игр в роли ведущего",
    defaultDirection: "desc" as const,
  },
  {
    value: "gamesPlaying",
    label: "Игры (игрок)",
    hint: "По количеству игр в роли игрока",
    defaultDirection: "desc" as const,
  },
  {
    value: "blogsHosting",
    label: "Блоги",
    hint: "По количеству блогов в роли ведущего",
    defaultDirection: "desc" as const,
  },
] as const;
