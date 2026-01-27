import { test, expect } from '@playwright/test';

test.describe('Registration Flow', () => {
  test('should show registration form', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Registration form should be visible in a lightbox
    await expect(page.locator('form')).toBeVisible();
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('#login')).toBeVisible();
    await expect(page.locator('#password')).toBeVisible();
  });

  test('should validate email format', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Fill with invalid email
    await page.fill('#email', 'invalid-email');
    await page.fill('#login', 'TestUser');
    await page.fill('#password', 'Test123!');

    // Wait for bot protection
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');

    // Should show validation error
    await expect(page.locator('.form-field-error')).toBeVisible({ timeout: 10000 });
  });

  test('should show password requirements error', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Fill with weak password
    await page.fill('#email', 'test@example.com');
    await page.fill('#login', 'TestUser');
    await page.fill('#password', '123');

    // Wait for bot protection
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');

    // Should show validation error
    await expect(page.locator('.form-field-error')).toBeVisible({ timeout: 10000 });
  });
});
