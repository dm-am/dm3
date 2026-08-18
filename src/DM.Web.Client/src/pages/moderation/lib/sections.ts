// The admin tab strip of ModerationPage and the minimum role behind every tab,
// in one table: the strip filters itself by the role and each destination page
// gates itself with the same row (useRoleGate + sectionRole), so the strip
// cannot offer a tab whose page would refuse the viewer.
import type { RequiredRole } from "./useRoleGate";

export interface ModerationSection {
  /** Route name of the tab — also the key a destination page reads its role by. */
  name: string;
  label: string;
  role: RequiredRole;
}

export const MODERATION_SECTIONS = [
  { name: "moderation", label: "Обзор", role: "Moderator" },
  {
    name: "moderation-username-changes",
    label: "Запросы на смену имени пользователя",
    role: "SeniorModerator",
  },
  { name: "moderation-tags", label: "Теги игр", role: "SeniorModerator" },
  { name: "moderation-awards", label: "Награды", role: "SeniorModerator" },
  {
    name: "moderation-achievements",
    label: "Достижения",
    role: "SeniorModerator",
  },
  { name: "moderation-fundraising", label: "Сбор средств", role: "Admin" },
] as const satisfies readonly ModerationSection[];

export type ModerationSectionName =
  (typeof MODERATION_SECTIONS)[number]["name"];

/** The tab's minimum role, for the page that gates itself with the same row. */
export function sectionRole(name: ModerationSectionName): RequiredRole {
  for (const section of MODERATION_SECTIONS) {
    if (section.name === name) return section.role;
  }
  // Unreachable: `name` is a literal drawn from the table itself.
  throw new Error(`Unknown moderation section: ${name}`);
}
