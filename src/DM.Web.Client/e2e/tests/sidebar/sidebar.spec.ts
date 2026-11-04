import { test, expect } from "../../fixtures/auth";

/**
 * The two sidebar columns as columns: both on a desktop and on the pages of the
 * site. What stands in the columns is read by `left-sidebar.spec.ts` and
 * `right-sidebar.spec.ts`, the fold of a block by `sidebar-block.spec.ts`.
 */
test.describe("Sidebars", () => {
  test.describe("Sidebar Responsiveness", () => {
    test("should show sidebars on desktop viewport", async ({ page }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.goto("/");

      await expect(page.locator(".sidebar-left")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });
  });

  test.describe("Sidebar on Different Pages", () => {
    for (const [name, path] of [
      ["home", "/"],
      ["games", "/games"],
      ["forum", "/forum"],
      ["community", "/community"],
    ]) {
      test(`should display sidebars on ${name} page`, async ({ page }) => {
        await page.goto(path);
        await expect(page.locator(".sidebar-left")).toBeVisible();
        await expect(page.locator(".sidebar-right")).toBeVisible();
      });
    }
  });
});
