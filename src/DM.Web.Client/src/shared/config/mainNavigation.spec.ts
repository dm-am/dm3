/**
 * Both menus read this one list now, so the only behaviour left in it is the
 * moderator gate. That gate is the thing the two copies could have disagreed
 * about while each carried its own check.
 */
import { describe, it, expect } from "vitest";
import { MAIN_NAV_SECTIONS, visibleMainNavSections } from "./mainNavigation";
import { sectionRole } from "./moderationSections";

describe("the site's main navigation", () => {
  it("hides the moderation section from everyone else", () => {
    const keys = visibleMainNavSections(false).map((section) => section.key);

    expect(keys).not.toContain("moderation");
    expect(keys).toEqual(
      MAIN_NAV_SECTIONS.filter((section) => !section.moderatorOnly).map(
        (section) => section.key,
      ),
    );
  });

  it("shows it to a moderator without reordering anything else", () => {
    expect(visibleMainNavSections(true)).toEqual([...MAIN_NAV_SECTIONS]);
  });

  // The zone has no page of its own: an overview that only asked to pick a
  // section from the menu beside it was a stop on the way to every one of them.
  // The link opens the moderators, a section every moderator may open, so it
  // never lands on a 403.
  it("opens moderation on the moderators, open to every moderator", () => {
    const moderation = MAIN_NAV_SECTIONS.find(
      (section) => section.key === "moderation",
    );

    expect(moderation?.to).toEqual({ name: "moderation-moderators" });
    expect(sectionRole("moderation-moderators")).toBe("Moderator");
  });

  it("keys every section uniquely, so a list render stays stable", () => {
    const keys = MAIN_NAV_SECTIONS.map((section) => section.key);

    expect(new Set(keys).size).toBe(keys.length);
  });
});
