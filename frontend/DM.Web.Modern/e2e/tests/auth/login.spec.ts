import { test, expect } from '@playwright/test';

test.describe('Login Flow', () => {
  test('should login with valid credentials', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="login-button"]');

    // Fill login form
    await page.fill('#login', 'Alice');
    await page.fill('#password', 'Test123!');
    await page.click('button[type="submit"]');

    // Should see user menu after successful login
    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible({ timeout: 10000 });
  });

  test('should show error with invalid credentials', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="login-button"]');

    // Fill with invalid credentials
    await page.fill('#login', 'Alice');
    await page.fill('#password', 'wrongpassword');

    // Wait for bot protection timer (2 seconds minimum)
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');

    // Should show error message
    await expect(page.locator('.form-field-error')).toBeVisible({ timeout: 10000 });
  });

  test('should logout successfully', async ({ page }) => {
    // Login first
    await page.goto('/');
    await page.click('[data-testid="login-button"]');
    await page.fill('#login', 'Alice');
    await page.fill('#password', 'Test123!');

    // Wait for bot protection timer
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');
    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible({ timeout: 10000 });

    // Logout - the logout button is directly visible, not in a dropdown
    await page.click('[data-testid="logout-button"]');

    // Should see login button again
    await expect(page.locator('[data-testid="login-button"]')).toBeVisible({ timeout: 10000 });
  });
});
