/**
 * Both menus read this one list now, so the only behaviour left in it is the
 * moderator gate. That gate is the thing the two copies could have disagreed
 * about while each carried its own check.
 */
import { describe, it, expect } from "vitest";
import { MAIN_NAV_SECTIONS, visibleMainNavSections } from "./mainNavigation";

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

  it("keys every section uniquely, so a list render stays stable", () => {
    const keys = MAIN_NAV_SECTIONS.map((section) => section.key);

    expect(new Set(keys).size).toBe(keys.length);
  });
});
