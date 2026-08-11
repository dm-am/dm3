/**
 * SSOT for role information across the site: section titles/nicknames
 * (ROLE_INFO), staff role lists, and the role badge map (ROLE_BADGES) that
 * every badge-rendering component uses.
 */

import { UserRole } from "@/shared/api/models/community";

/**
 * Staff/system role badge: [A] or [M] (Latin) — gray brackets, green bold
 * letter. Only two letters exist: A for the admin tier, M for the moderator
 * tier; the exact role lives in the tooltip.
 */
export interface RoleBadge {
  /** Short badge letter shown between the brackets */
  letter: string;
  /** Full role name for tooltips */
  label: string;
  /** CSS class hook for per-role styling */
  cssClass: string;
}

export const ROLE_BADGES: Partial<Record<UserRole, RoleBadge>> = {
  [UserRole.Admin]: {
    letter: "A",
    label: "Администратор",
    cssClass: "role-admin",
  },
  [UserRole.SeniorModerator]: {
    letter: "M",
    label: "Старший модератор",
    cssClass: "role-senior-moderator",
  },
  [UserRole.Moderator]: {
    letter: "M",
    label: "Модератор",
    cssClass: "role-moderator",
  },
  [UserRole.Mentor]: {
    letter: "M",
    label: "Наставник",
    cssClass: "role-mentor",
  },
  // Same letter as Admin — the bot IS an administrator; the tooltip
  // carries the robot specifics.
  [UserRole.System]: {
    letter: "A",
    label: "Робот-администратор",
    cssClass: "role-system",
  },
};

/** Badge for a role, or null when the role has none (regular users, guests). */
export function getRoleBadge(
  role: UserRole | null | undefined,
): RoleBadge | null {
  return (role && ROLE_BADGES[role]) || null;
}

export interface RoleInfo {
  /** Section title as the staff table prints it: plural for the roles it groups
   * ("Администраторы", "Модераторы"), singular for the three that are not a group
   * ("Пользователь", "Гость", "Система") */
  title: string;
  /** Plural community nickname (RulesStaffTable groups) */
  nickname: string;
  /** Singular community nickname (profile pages, "Тролль" for one admin) */
  nicknameSingular: string;
  description: string;
}

export const ROLE_INFO: Record<UserRole, RoleInfo> = {
  [UserRole.Admin]: {
    title: "Администраторы",
    nickname: "Тролли",
    nicknameSingular: "Тролль",
    description:
      "Определяют политику сайта, технические решения, являются последней инстанцией.",
  },
  [UserRole.SeniorModerator]: {
    title: "Старшие модераторы",
    nickname: "Старшие гоблины",
    nicknameSingular: "Старший гоблин",
    description:
      "Контролируют и корректируют действия модераторов, выдают баны.",
  },
  [UserRole.Moderator]: {
    title: "Модераторы",
    nickname: "Младшие гоблины",
    nicknameSingular: "Младший гоблин",
    description:
      "Следят за порядком, выискивают нарушения правил и неактивные модули, выдают штрафные баллы.",
  },
  [UserRole.Mentor]: {
    title: "Наставники",
    nickname: "Гоблины-наставники",
    nicknameSingular: "Гоблин-наставник",
    description:
      "Помогают новичкам адаптироваться на сайте, контролируют премодерируемые модули. Им можно и нужно задавать вопросы.",
  },
  [UserRole.RegularUser]: {
    title: "Пользователь",
    nickname: "",
    nicknameSingular: "",
    description: "Обычный пользователь сайта.",
  },
  [UserRole.Guest]: {
    title: "Гость",
    nickname: "",
    nicknameSingular: "",
    description: "Неавторизованный посетитель.",
  },
  [UserRole.System]: {
    title: "Система",
    nickname: "",
    nicknameSingular: "",
    description: "Системный пользователь (робот-администратор).",
  },
};

/** Roles displayed in RulesStaffTable (moderators/admins) */
export const STAFF_ROLES: UserRole[] = [
  UserRole.Admin,
  UserRole.SeniorModerator,
  UserRole.Moderator,
  UserRole.Mentor,
];
