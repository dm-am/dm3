import { test, expect } from '../fixtures/auth';

test.describe('Forum Topics', () => {
  test('should display topics list', async ({ page }) => {
    // Forum ID is the forum title (e.g., "Новости")
    await page.goto('/forum/Новости');
    await expect(page.locator('.topics-table')).toBeVisible({ timeout: 10000 });
  });

  test('should display topics in table format', async ({ page }) => {
    await page.goto('/forum/Новости');
    await expect(page.locator('.topics-header')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.col-title')).toBeVisible();
    await expect(page.locator('.col-author')).toBeVisible();
  });

  test('should navigate to topic detail', async ({ page }) => {
    await page.goto('/forum/Новости');
    await page.waitForSelector('.topics-row', { timeout: 10000 });

    // Click on first topic if exists
    const firstTopic = page.locator('.topics-row .col-title a').first();
    if (await firstTopic.isVisible()) {
      await firstTopic.click();
      await expect(page).toHaveURL(/\/topic\//);
    }
  });
});
