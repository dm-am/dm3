// The pages of moderation gate themselves by the row of the one table of
// sections (shared/config/moderationSections.ts): the menu offers a section,
// the zone shell (ModerationPage) opens its page, and the page loads its data
// by the same role. This module only hands the pages that table's lookup.
export {
  sectionRole,
  type ModerationSectionName,
} from "@/shared/config/moderationSections";
