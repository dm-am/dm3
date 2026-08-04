import { test, expect } from "../../fixtures/auth";

test.describe("Game Subscription", () => {
  test("should display game page with subscribe button", async ({
    authenticatedPage,
  }) => {
    // First navigate to games list
    await authenticatedPage.goto("/games");
    await authenticatedPage.waitForSelector(".games-row", { timeout: 10000 });

    // Click on first game in the list
    const firstGameLink = authenticatedPage
      .locator(".games-row .col-title a")
      .first();
    if (await firstGameLink.isVisible()) {
      await firstGameLink.click();
      await expect(authenticatedPage).toHaveURL(/\/game\//);

      // Should see the game details page
      await expect(authenticatedPage.locator(".game-page")).toBeVisible({
        timeout: 10000,
      });
    }
  });

  test("should show the game menu in the sidebar", async ({ page }) => {
    // ".game-tabs" and ".games-row" are in no template of the client: per-game
    // navigation is the left-sidebar "Меню игры" block, and the games table is
    // a DataTable. Both locators matched nothing, so the `if` was false and the
    // test asserted nothing — see characters.spec.ts for the same pair.
    await page.goto("/games");
    const firstGameLink = page
      .getByRole("table")
      .locator("tr.table-row td.col-title a")
      .first();
    await expect(firstGameLink).toBeVisible();
    await firstGameLink.click();

    await expect(
      page.locator(".sidebar-block").filter({ hasText: "Меню игры" }),
    ).toBeVisible();
  });
});
