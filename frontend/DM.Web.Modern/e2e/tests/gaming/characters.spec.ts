import { test, expect } from '@playwright/test';

test.describe('Game Characters', () => {
  test('should navigate to characters tab from game page', async ({ page }) => {
    // First get a real game ID
    await page.goto('/games');
    await page.waitForSelector('.games-row', { timeout: 10000 });

    const firstGameLink = page.locator('.games-row .col-title a').first();
    if (await firstGameLink.isVisible()) {
      await firstGameLink.click();
      await expect(page).toHaveURL(/\/game\//);

      // Navigate to characters tab
      const charactersTab = page.locator('.game-tabs a[href*="characters"]');
      if (await charactersTab.isVisible()) {
        await charactersTab.click();
        await expect(page).toHaveURL(/\/characters/);
        await expect(page.locator('.characters-groups, .game-characters')).toBeVisible({ timeout: 10000 });
      }
    }
  });

  test('should display character groups by status', async ({ page }) => {
    await page.goto('/games');
    await page.waitForSelector('.games-row', { timeout: 10000 });

    const firstGameLink = page.locator('.games-row .col-title a').first();
    if (await firstGameLink.isVisible()) {
      await firstGameLink.click();

      const charactersTab = page.locator('.game-tabs a[href*="characters"]');
      if (await charactersTab.isVisible()) {
        await charactersTab.click();
        // Should see character status groups (Active, Registration, etc.)
        await expect(page.locator('.characters-group-title, .group-title')).toBeVisible({ timeout: 10000 });
      }
    }
  });
});
