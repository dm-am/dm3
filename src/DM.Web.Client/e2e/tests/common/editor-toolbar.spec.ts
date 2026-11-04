import { expect } from "../../fixtures/auth";
import { test, type Target } from "../../fixtures/roles";
import { playedRoom } from "../../fixtures/seed";
import { settle } from "../../fixtures/window";

/**
 * The tag bar of the editor in the middle column of a desktop: every tag in
 * sight, no "…", and the switch [bbcode]/wysiwyg inside the editor, on a line
 * of its own under the tags where the widest group and the switch do not fit
 * side by side.
 */
const EDITOR_WIDTHS = [1366, 1920];

test.describe("The tag bar of the editor on a desktop", () => {
  for (const target of [
    { path: "/forum/general/5", ready: "main#main .editor-toolbar" },
    { path: playedRoom, ready: "main#main .editor-toolbar" },
  ] satisfies Omit<Target, "roles">[]) {
    const where = typeof target.path === "string" ? target.path : "a room";
    test(`${where} shows every tag and keeps the switch inside at ${EDITOR_WIDTHS.join(", ")}px`, async ({
      openAs,
    }) => {
      const page = await openAs("user", EDITOR_WIDTHS[0], {
        ...target,
        roles: ["user"],
      });
      await expect(page.locator(target.ready).first()).toBeVisible({
        timeout: 15000,
      });
      const problems: string[] = [];
      for (const width of EDITOR_WIDTHS) {
        await page.setViewportSize({ width, height: 900 });
        await settle(page);
        const bar = await page.evaluate(() => {
          const wrapper = document.querySelector(
            "main#main .bbcode-editor-wrapper",
          )!;
          const frame = wrapper.getBoundingClientRect();
          const tags = [...wrapper.querySelectorAll(".tag-btn")];
          const mode = wrapper.querySelector(".mode-tabs")!;
          const switchBox = mode.getBoundingClientRect();
          return {
            tags: tags.length,
            hidden: tags.filter((tag) => !tag.getClientRects().length).length,
            more: wrapper.querySelectorAll(".tag-btn-more, .more-menu").length,
            switchInside:
              switchBox.left >= frame.left - 0.5 &&
              switchBox.right <= frame.right + 0.5,
          };
        });
        if (bar.tags < 16) problems.push(`${width}px: ${bar.tags} tags`);
        if (bar.hidden) problems.push(`${width}px: ${bar.hidden} tags hidden`);
        if (bar.more) problems.push(`${width}px: a menu of the rest`);
        if (!bar.switchInside)
          problems.push(`${width}px: the switch stands out of the editor`);
      }
      expect(problems).toEqual([]);
    });
  }
});
