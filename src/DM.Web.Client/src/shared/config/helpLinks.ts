/**
 * SSOT for help links and admin links across the site.
 * Used in: HelpLinksSection.vue, AdminList.vue
 */

export type HelpIconType = "question" | "warning" | "bug" | "idea";

export interface HelpLink {
  key: string;
  icon: HelpIconType;
  problem: string;
  solution: string;
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
  },
  {
    key: "warning",
    icon: "warning",
    problem: "Кто-то нарушает правила",
    solution: "форма жалоб",
    url: "https://l.dm.am/ComplaintReport.aspx",
    external: true,
  },
  {
    key: "bug",
    icon: "bug",
    problem: "Вижу ошибку",
    solution: "форма ошибок",
    url: "https://l.dm.am/ErrorReport.aspx",
    external: true,
  },
  {
    key: "idea",
    icon: "idea",
    problem: "Есть идея",
    solution: "тема улучшений",
    url: "https://l.dm.am/Comments.aspx?contentId=9193&contentType=5",
    external: true,
  },
];

// Note: "Форма жалоб" is in HELP_LINKS, not duplicated here
export const ADMIN_LINKS: AdminLink[] = [
  {
    title: "Обсуждение действий администрации",
    url: "https://l.dm.am/Comments.aspx?contentId=9192&contentType=5",
    external: true,
  },
  {
    title: "Лог предупреждений",
    url: "https://l.dm.am/WarningLog.aspx",
    external: true,
  },
  {
    title: "Пульс",
    url: "https://l.dm.am/VotePulse.aspx",
    external: true,
  },
  {
    title: "Discord",
    url: "https://discord.gg/dm-roleplay",
    external: true,
  },
];
