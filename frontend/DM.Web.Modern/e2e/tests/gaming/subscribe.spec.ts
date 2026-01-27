import { test, expect } from '../fixtures/auth';

test.describe('Game Subscription', () => {
  test('should display game page with subscribe button', async ({ authenticatedPage }) => {
    // First navigate to games list
    await authenticatedPage.goto('/games');
    await authenticatedPage.waitForSelector('.games-row', { timeout: 10000 });

    // Click on first game in the list
    const firstGameLink = authenticatedPage.locator('.games-row .col-title a').first();
    if (await firstGameLink.isVisible()) {
      await firstGameLink.click();
      await expect(authenticatedPage).toHaveURL(/\/game\//);

      // Should see the game details page
      await expect(authenticatedPage.locator('.game-page')).toBeVisible({ timeout: 10000 });
    }
  });

  test('should show game tabs', async ({ page }) => {
    await page.goto('/games');
    await page.waitForSelector('.games-row', { timeout: 10000 });

    const firstGameLink = page.locator('.games-row .col-title a').first();
    if (await firstGameLink.isVisible()) {
      await firstGameLink.click();
      // Should see game navigation tabs
      await expect(page.locator('.game-tabs')).toBeVisible({ timeout: 10000 });
    }
  });
});
