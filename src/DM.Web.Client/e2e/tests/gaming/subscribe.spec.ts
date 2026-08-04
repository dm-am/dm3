import { test, expect } from "../../fixtures/auth";
import type { Page } from "@playwright/test";

/**
 * ".game-tabs", ".games-row" and ".game-page" are in no template of the
 * client: per-game navigation is the left-sidebar "Меню игры" block
 * (`#sidebar-list-GamePanel`), the games table is a DataTable of
 * `tr.table-row`, and the game page itself renders `.game-header` with
 * `.game-details` under it. Every locator here matched nothing, so the `if`
 * was false and the test asserted nothing — see characters.spec.ts for the
 * same pair.
 */

/** Opens the first game of /games and lands on its page. */
async function openFirstGame(page: Page): Promise<void> {
  await page.goto("/games");
  const firstGame = page
    .getByRole("table")
    .locator("tr.table-row td.col-title a")
    .first();
  await expect(firstGame).toBeVisible();
  await firstGame.click();
  await expect(page).toHaveURL(/\/game\//);
}

test.describe("Game Subscription", () => {
  test("should display the game page for a signed-in reader", async ({
    authenticatedPage,
  }) => {
    await openFirstGame(authenticatedPage);

    await expect(authenticatedPage.locator(".game-header")).toBeVisible();
    await expect(authenticatedPage.locator(".game-details")).toBeVisible();
  });

  test("should show the game menu in the sidebar", async ({ page }) => {
    await openFirstGame(page);

    await expect(page.locator("#sidebar-list-GamePanel")).toBeVisible();
  });
});
