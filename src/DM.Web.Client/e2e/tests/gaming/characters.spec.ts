import { test, expect } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";

/**
 * The game roster: reaching it, and what it says while it has nothing to say.
 *
 * What this file was: every assertion sat inside `if (await x.isVisible())`,
 * and none of the selectors existed. `.games-row` is not in the client (the
 * games table is a DataTable of `tr.table-row`), `.game-tabs` is not either
 * (per-game navigation lives in the left-sidebar "Меню игры" block), and the
 * headings it claimed to check — `.characters-group-title`, `.group-title` —
 * are written `.section-title`. Both tests reported green having executed no
 * assertion at all, over the very page whose dead end was the highest finding
 * of this slice.
 *
 * So: no `if` around an assertion. A locator that finds nothing must fail.
 */

/**
 * The left-sidebar block that carries per-game navigation. SidebarBlock
 * identifies itself by token, not by a class: `.sidebar-block` was the second
 * invented selector in this file's history, and it went in as the fix for the
 * first.
 */
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
    // Walked here rather than through the helper: this test is the route
    // itself, and a body whose only statement is a call to a helper reports
    // green on the day the helper stops asserting — the failure this file was
    // rewritten to prevent, one level up.
    await openFirstGame(page);
    await gameMenu(page).getByRole("link", { name: "Персонажи" }).click();

    await expect(page).toHaveURL(/\/game\/[^/?]+\/characters$/);
    await expect(roster(page)).toBeVisible();
  });

  test("answers with a roster or with the empty state, never with neither", async ({
    page,
  }) => {
    await openRoster(page);

    // The skeleton is gone by the time the page settles: "В этой игре пока нет
    // персонажей" used to be printed while the request was still on the wire.
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
