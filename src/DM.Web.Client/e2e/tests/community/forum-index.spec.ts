import { test, expect } from '@playwright/test';

test.describe('Forum Index', () => {
  test('should display forum index page', async ({ page }) => {
    await page.goto('/forum');
    // Should see forum boards or sections
    await expect(page.locator('.forum-index, .boards-list, .forum-section')).toBeVisible({ timeout: 10000 });
  });

  test('should show forum sections', async ({ page }) => {
    await page.goto('/forum');
    // Forums are grouped into sections
    await expect(page.locator('.forum-group, .section-title, h2')).toBeVisible({ timeout: 10000 });
  });

  test('should navigate to board', async ({ page }) => {
    await page.goto('/forum');
    await page.waitForSelector('.forum-link, .board-link, .forum-title a', { timeout: 10000 });

    // Click on first forum link
    const firstBoard = page.locator('.forum-link, .board-link, .forum-title a').first();
    if (await firstBoard.isVisible()) {
      await firstBoard.click();
      await expect(page).toHaveURL(/\/forum\//);
      // Should see topics table
      await expect(page.locator('.topics-table')).toBeVisible({ timeout: 10000 });
    }
  });
});
