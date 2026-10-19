import { UserRole } from "@/entities/user";
import { NEWBIE_POST_THRESHOLD } from "@/shared/lib/constants/user";

export type ActivityFilter = "active" | "inactive" | "all";

export type OnlineFilter = "all" | "online";

export type RoleFilter = "all" | UserRole;

export type ExperienceFilter = "all" | "newbie" | "experienced";

export interface UsersFilterState {
  search: string;

  activity: ActivityFilter;

  /** Only for active users. */
  onlineFilter: OnlineFilter;

  role: RoleFilter;

  experience: ExperienceFilter;

  ratingMin: number | null;

  ratingMax: number | null;

  gamesHostingMin: number | null;

  gamesHostingMax: number | null;

  gamesPlayingMin: number | null;

  gamesPlayingMax: number | null;

  blogsHostingMin: number | null;

  blogsHostingMax: number | null;

  registeredFromUtc: string | null;

  registeredToUtc: string | null;

  sortBy: string;

  sortOrder: "asc" | "desc";
}

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

// "online" sets activity=active and onlineFilter=online.
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

// From the lowest privilege to the highest.
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

export const EXPERIENCE_OPTIONS = [
  { value: "all" as const, label: "Все", hint: "Без фильтрации по опыту" },
  {
    value: "newbie" as const,
    label: "Новички",
    hint: `Менее ${NEWBIE_POST_THRESHOLD} постов`,
  },
  {
    value: "experienced" as const,
    label: "Опытные",
    hint: `${NEWBIE_POST_THRESHOLD}+ постов`,
  },
] as const;

// Without the "all" option, and derived: a hand-typed hint drifted from its list.
export function optionsHint(
  options: readonly { value: unknown; label: string }[],
): string {
  return options
    .filter((option) => option.value !== "all")
    .map((option) => option.label)
    .join(", ");
}

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
