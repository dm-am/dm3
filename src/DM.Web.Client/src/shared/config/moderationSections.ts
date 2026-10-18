/**
 * SSOT for the sections of moderation: the route of each, the words that name
 * it, the lowest role it opens to and the group of the menu it stands in.
 *
 * Three readers lean on one row. The left sidebar builds its moderation menu
 * from the table, the zone shell (pages/moderation) answers a viewer below the
 * row's role with the 403 page, and every page gates its own data with the same
 * role. While the menu and the tab strip each carried a list of their own, one
 * offered the templates of schemas twice and the other knew nothing of tags or
 * awards.
 *
 * The label is the menu row and the heading of the page at once, so a page
 * does not get a second name for itself. The rows stand group by group, in the
 * order of MODERATION_GROUPS, and within a group in the order of the menu.
 *
 * Lives in shared because a widget (the sidebar) and the pages both read it,
 * and a widget cannot import a page.
 */

/** The three roles a moderation page can ask for, lowest first. */
export type ModerationRole = "Moderator" | "SeniorModerator" | "Admin";

/**
 * The groups of the menu in its order. A row stands in the group of what the
 * moderator does there: decides what waits for him, looks after people, reads
 * back what was done, or sets up the site itself; the fundraising, the
 * administrators' alone, stands apart at the end. The menu draws no heading
 * over a group: the groups are parted by an empty line, as DM2 parts the
 * groups of the menu of a game.
 */
export const MODERATION_GROUPS = [
  "queues",
  "users",
  "logs",
  "site",
  "funds",
] as const;

export type ModerationGroup = (typeof MODERATION_GROUPS)[number];

export interface ModerationSection {
  /** Route name of the section's page. */
  name: string;
  /** The menu row and the heading of the page. */
  label: string;
  /** The lowest role the page opens to. */
  role: ModerationRole;
  group: ModerationGroup;
}

export const MODERATION_SECTIONS = [
  {
    name: "moderation-games",
    label: "Премодерируемые игры",
    role: "Moderator",
    group: "queues",
  },
  {
    name: "moderation-blogs",
    label: "Премодерируемые блоги",
    role: "Moderator",
    group: "queues",
  },
  {
    name: "moderation-username-changes",
    label: "Запросы на смену имени",
    role: "SeniorModerator",
    group: "queues",
  },
  {
    name: "moderation-tickets",
    label: "Обращения",
    role: "Moderator",
    group: "queues",
  },
  {
    name: "moderation-new-users",
    label: "Новые пользователи",
    role: "Moderator",
    group: "users",
  },
  {
    name: "moderation-removed-accounts",
    label: "Удаленные пользователи",
    role: "SeniorModerator",
    group: "users",
  },
  {
    name: "moderation-violators",
    label: "Нарушители",
    role: "Moderator",
    group: "users",
  },
  {
    name: "moderation-moderators",
    label: "Модераторы",
    role: "Moderator",
    group: "users",
  },
  {
    name: "moderation-rated-posts",
    label: "Последние оцененные посты",
    role: "Moderator",
    group: "logs",
  },
  {
    name: "moderation-warnings",
    label: "Последние предупреждения",
    role: "Moderator",
    group: "logs",
  },
  {
    name: "moderation-bans",
    label: "Последние баны",
    role: "Moderator",
    group: "logs",
  },
  {
    name: "moderation-uploads",
    label: "Все загруженные файлы",
    role: "Moderator",
    group: "logs",
  },
  {
    name: "moderation-tags",
    label: "Игровые теги",
    role: "SeniorModerator",
    group: "site",
  },
  {
    name: "moderation-schema-templates",
    label: "Шаблоны схем атрибутов",
    role: "SeniorModerator",
    group: "site",
  },
  {
    name: "moderation-achievements",
    label: "Достижения",
    role: "SeniorModerator",
    group: "site",
  },
  {
    name: "moderation-awards",
    label: "Награды",
    role: "SeniorModerator",
    group: "site",
  },
  {
    name: "moderation-fundraising",
    label: "Сбор средств",
    role: "Admin",
    group: "funds",
  },
] as const satisfies readonly ModerationSection[];

export type ModerationSectionName =
  (typeof MODERATION_SECTIONS)[number]["name"];

/** The row's lowest role. */
export function sectionRole(name: ModerationSectionName): ModerationRole {
  for (const section of MODERATION_SECTIONS) {
    if (section.name === name) return section.role;
  }
  // Unreachable: `name` is a literal drawn from the table itself.
  throw new Error(`Unknown moderation section: ${name}`);
}

/**
 * Pages of the zone that are not a row of the menu, and the role each opens
 * to. A series and the catalogue of award types sit under "Награды", a
 * template under "Шаблоны схем атрибутов", and they take the role of that
 * row. One ticket opens to any moderator who reads the queue "Обращения", and
 * the server answers for the rest: a ticket whose reason his role is not
 * shown is "not found".
 *
 * This is about who may open a page, and only that. The menu marks no row on
 * these pages: the address names no section, and the heading says where the
 * reader is.
 */
const SUBPAGE_ROLES: Readonly<Record<string, ModerationRole>> = {
  "moderation-schema-template-new": sectionRole("moderation-schema-templates"),
  "moderation-schema-template": sectionRole("moderation-schema-templates"),
  "moderation-awards-series": sectionRole("moderation-awards"),
  "moderation-award-types": sectionRole("moderation-awards"),
  "moderation-ticket": sectionRole("moderation-tickets"),
};

/**
 * The lowest role the page behind a route name opens to, or undefined when the
 * name is no page of moderation at all.
 */
export function moderationPageRole(
  routeName: string,
): ModerationRole | undefined {
  const row = MODERATION_SECTIONS.find((section) => section.name === routeName);
  return row?.role ?? SUBPAGE_ROLES[routeName];
}
