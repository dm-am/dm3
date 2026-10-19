import { test, expect } from "../../fixtures/auth";
import { blockFold, blockToggle } from "../../fixtures/sidebar";

/**
 * The fold of a sidebar block. The popular games stand for any of them: every
 * block is a `SidebarBlock`, which keeps the fold and remembers it.
 */
test.describe("Sidebars", () => {
  test.describe("SidebarBlock - Collapse/Expand", () => {
    test.beforeEach(async ({ page }) => {
      // Reset the collapse state; the key is the component's own
      // (`__HideMenuModule_<token>__`).
      await page.goto("/");
      await page.evaluate(() => {
        Object.keys(localStorage)
          .filter((key) => key.startsWith("__HideMenuModule_"))
          .forEach((key) => localStorage.removeItem(key));
      });
      await page.reload();
    });

    test("should toggle block collapse on toggle click", async ({ page }) => {
      const fold = blockFold(page, "PopularGames");
      const toggle = blockToggle(page, "PopularGames");

      // Initially expanded. Only the fold sign toggles: the heading text is
      // not clickable.
      await expect(fold).toBeVisible();

      await toggle.click();
      await expect(fold).toBeHidden();

      await toggle.click();
      await expect(fold).toBeVisible();
    });

    test("should persist collapse state in localStorage", async ({ page }) => {
      await blockToggle(page, "PopularGames").click();
      await expect(blockFold(page, "PopularGames")).toBeHidden();

      await page.reload();

      await expect(blockFold(page, "PopularGames")).toBeHidden();
    });

    test("should announce the collapse state on the toggle", async ({
      page,
    }) => {
      const toggle = blockToggle(page, "PopularGames");

      await expect(toggle).toHaveAttribute("aria-expanded", "true");
      await toggle.click();
      await expect(toggle).toHaveAttribute("aria-expanded", "false");
    });
  });
});
