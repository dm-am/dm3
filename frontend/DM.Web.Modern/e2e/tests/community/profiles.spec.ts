import { test, expect } from '@playwright/test';

test.describe('User Profiles', () => {
  test('should display user profile', async ({ page }) => {
    // Profile route is /:login (directly under root)
    await page.goto('/Alice');
    await expect(page.locator('.profile-info, .user-information')).toBeVisible();
  });

  test('should navigate to user games tab', async ({ page }) => {
    await page.goto('/Alice/games');
    await expect(page.locator('.user-games')).toBeVisible();
  });

  test('should navigate to user characters tab', async ({ page }) => {
    await page.goto('/Alice/characters');
    await expect(page.locator('.user-characters')).toBeVisible();
  });
});
