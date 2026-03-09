import { test, expect } from '@playwright/test';

test.describe('Browse Games', () => {
  test('should display active games page', async ({ page }) => {
    await page.goto('/games');
    await expect(page.locator('.games-table')).toBeVisible();
  });

  test('should switch between game tabs', async ({ page }) => {
    await page.goto('/games');

    // Switch to recruiting tab
    await page.click('[data-testid="tab-recruiting"]');
    await expect(page).toHaveURL(/\/games\/recruiting/);
    await expect(page.locator('.games-table')).toBeVisible();

    // Switch to finished tab
    await page.click('[data-testid="tab-finished"]');
    await expect(page).toHaveURL(/\/games\/finished/);
    await expect(page.locator('.games-table')).toBeVisible();

    // Switch back to active tab
    await page.click('[data-testid="tab-active"]');
    await expect(page).toHaveURL(/\/games\/?$/);
  });

  test('should show game status in list', async ({ page }) => {
    await page.goto('/games');
    await expect(page.locator('.game-status').first()).toBeVisible();
  });
});
