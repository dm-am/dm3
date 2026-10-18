// Sections of the site by the first segment of a route path. The table is the
// only hand-written part of the document: a route whose first segment is in no
// row lands in "прочее", so a new page is shown the day it appears and asks for
// a row here instead of disappearing.

/** Section, then the first path segments it takes. "" is the root, "*" a path that opens with a parameter. */
export const SECTIONS = [
  ["Главная", [""]],
  [
    "Сайт и сообщество",
    ["about", "testimonials", "rules", "privacy", "agreement", "donate", "statistics", "community", "polls", "pulse", "warnings"],
  ],
  ["Вход и аккаунт", ["account", "activate", "confirm-email", "reset-password", "change-username", "two-factor"]],
  ["Игры", ["games", "game"]],
  ["Блоги", ["blogs"]],
  ["Форум", ["forum"]],
  ["Профиль", ["users"]],
  ["Личка и чат", ["messenger", "global-chat"]],
  ["Уведомления и личное", ["notifications", "subscriptions", "notepad"]],
  ["Модерация", ["moderation"]],
  ["Обращения", ["support", "complaint", "my-tickets"]],
  ["Служебные", ["forum-topic", "publication", "removed-account"]],
  ["Ошибки", ["error", "*"]],
];

export const UNASSIGNED = "Прочее";

export function sectionOf(path) {
  const first = path.split("/").filter((s) => s !== "")[0] ?? "";
  const key = first.startsWith(":") ? "*" : first;
  return SECTIONS.find(([, prefixes]) => prefixes.includes(key))?.[0] ?? UNASSIGNED;
}
