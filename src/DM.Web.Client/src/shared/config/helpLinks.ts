/**
 * SSOT for help links and admin links across the site.
 * Used in: HelpLinksSection.vue, AdminList.vue
 */

export type HelpIconType = "question" | "warning" | "bug" | "idea";

export interface HelpLink {
  key: string;
  icon: HelpIconType;
  problem: string;
  /** Full solution text (plain text parts) */
  solution: string;
  /** Link text within solution (optional - if set, only this part is linked) */
  linkText?: string;
  url?: string;
  external?: boolean;
}

export interface AdminLink {
  title: string;
  url: string;
  external?: boolean;
}

export const HELP_LINKS: HelpLink[] = [
  {
    key: "question",
    icon: "question",
    problem: "Возникли вопросы",
    solution: "напиши наставнику в ЛС",
    linkText: "наставнику",
    url: "/community?role=Mentor",
  },
  {
    key: "warning",
    icon: "warning",
    problem: "Кто-то нарушает правила",
    solution: "форма жалоб",
    url: "/complaint",
  },
  {
    key: "bug",
    icon: "bug",
    problem: "Вижу ошибку",
    solution: "форма ошибок",
    url: "/support",
  },
  {
    key: "idea",
    icon: "idea",
    problem: "Есть идея",
    solution: "тема улучшений",
    url: "/forum/improvements",
  },
];

// Note: "Форма жалоб" is in HELP_LINKS, not duplicated here
export const ADMIN_LINKS: AdminLink[] = [
  {
    title: "Обсуждение действий администрации",
    url: "/forum/general/2",
  },
  {
    title: "Лог предупреждений",
    url: "/warnings",
  },
  {
    title: "Пульс",
    url: "/pulse",
  },
  {
    title: "Discord",
    url: "https://discord.gg/dm-roleplay",
    external: true,
  },
];
