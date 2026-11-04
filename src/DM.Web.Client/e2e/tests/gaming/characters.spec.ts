import { test, expect } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";

/** The game roster: reaching it, and what it says while it has nothing to say. */

/** The left-sidebar block that carries per-game navigation, by its token. */
const gameMenu = (page: Page): Locator =>
  page.locator("#sidebar-list-GamePanel");

const roster = (page: Page): Locator => page.locator(".game-characters");

/** Opens the first game of /games and lands on its page. */
async function openFirstGame(page: Page): Promise<void> {
  await page.goto("/games");
  const firstGame = page
    .getByRole("table")
    .locator("tr.table-row td.col-title a")
    .first();
  await expect(firstGame).toBeVisible();
  await firstGame.click();
  await expect(page).toHaveURL(/\/game\/[^/?]+/);
}

async function openRoster(page: Page): Promise<void> {
  await openFirstGame(page);
  await gameMenu(page).getByRole("link", { name: "Персонажи" }).click();
  await expect(page).toHaveURL(/\/characters$/);
  await expect(roster(page)).toBeVisible();
}

test.describe("Game characters", () => {
  test("opens the roster from the game menu in the sidebar", async ({
    page,
  }) => {
    // Walked here rather than through the helper: this test is the route itself.
    await openFirstGame(page);
    await gameMenu(page).getByRole("link", { name: "Персонажи" }).click();

    await expect(page).toHaveURL(/\/game\/[^/?]+\/characters$/);
    await expect(roster(page)).toBeVisible();
  });

  test("answers with a roster or with the empty state, never with neither", async ({
    page,
  }) => {
    await openRoster(page);

    // The skeleton is gone by the time the page settles.
    await expect(roster(page).locator(".character-skeleton-grid")).toHaveCount(
      0,
    );

    const groups = roster(page).locator(".characters-section .section-title");
    const empty = roster(page).locator(".characters-empty");
    const [groupCount, emptyCount] = await Promise.all([
      groups.count(),
      empty.count(),
    ]);

    expect(groupCount > 0 || emptyCount === 1).toBe(true);
    // And never a red box without a way out of it.
    await expect(roster(page).locator(".error-state")).toHaveCount(0);
  });
});
