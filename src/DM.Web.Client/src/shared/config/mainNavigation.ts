/** SSOT for the site's main navigation, the top menu of the header (widgets/header). */

import type { RouteLocationRaw } from "vue-router";

export interface MainNavSection {
  /** Stable list key, independent of what the route looks like. */
  key: string;
  title: string;
  to: RouteLocationRaw;
  /** Shown to moderators and above only. */
  moderatorOnly?: boolean;
}

export const MAIN_NAV_SECTIONS: readonly MainNavSection[] = [
  { key: "about", title: "О проекте", to: { name: "about" } },
  { key: "rules", title: "Правила", to: { name: "rules" } },
  { key: "games", title: "Игры", to: { name: "games" } },
  { key: "blogs", title: "Блоги", to: { name: "blogs" } },
  { key: "community", title: "Сообщество", to: { name: "community" } },
  { key: "forum", title: "Форум", to: { name: "forum-index" } },
  { key: "global-chat", title: "Чат", to: { name: "global-chat" } },
  // Always visible to everyone (owner rule): the newbies board is the site's
  // front door for prospective players, not a newbie-only tool.
  {
    key: "newbies",
    title: "Для новичков",
    to: { name: "forum", params: { alias: "newbies" } },
  },
  // The moderators, a section every moderator may open, where the address of
  // the zone leads too (product doc 4.2.1.5): the zone has no page of its own
  // to land on.
  {
    key: "moderation",
    title: "Модерация",
    to: { name: "moderation-moderators" },
    moderatorOnly: true,
  },
];

/** The sections a given reader sees, in order. */
export function visibleMainNavSections(isModerator: boolean): MainNavSection[] {
  return MAIN_NAV_SECTIONS.filter(
    (section) => !section.moderatorOnly || isModerator,
  );
}
