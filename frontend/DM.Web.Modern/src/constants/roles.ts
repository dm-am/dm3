/**
 * SSOT for role information across the site.
 * Used in: AdminList.vue, ProfilePage.vue
 */

import { UserRole } from "@/api/models/community";

export interface RoleInfo {
  title: string;
  nickname: string;
  description: string;
}

export const ROLE_INFO: Record<UserRole, RoleInfo> = {
  [UserRole.Admin]: {
    title: "Администратор",
    nickname: "Тролль",
    description:
      "Определяет политику сайта, технические решения, является последней инстанцией.",
  },
  [UserRole.SeniorModerator]: {
    title: "Старшие модераторы",
    nickname: "Старшие гоблины",
    description:
      "Контролируют и корректируют действия модераторов, выдают баны.",
  },
  [UserRole.Moderator]: {
    title: "Модераторы",
    nickname: "Младшие гоблины",
    description:
      "Следят за порядком, выискивают нарушения правил и неактивные модули, выдают штрафные баллы.",
  },
  [UserRole.Mentor]: {
    title: "Наставники",
    nickname: "Гоблины-наставники",
    description:
      "Помогают новичкам адаптироваться на сайте, контролируют премодерируемые модули. Им можно и нужно задавать вопросы.",
  },
  [UserRole.RegularUser]: {
    title: "Пользователь",
    nickname: "",
    description: "Обычный пользователь сайта.",
  },
  [UserRole.Guest]: {
    title: "Гость",
    nickname: "",
    description: "Неавторизованный посетитель.",
  },
};

/** Roles displayed in AdminList (staff only) */
export const STAFF_ROLES: UserRole[] = [
  UserRole.Admin,
  UserRole.SeniorModerator,
  UserRole.Moderator,
  UserRole.Mentor,
];
