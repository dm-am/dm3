import { test, expect } from '@playwright/test';

test.describe('Registration Flow', () => {
  test('should show registration form step 1 (email)', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Registration form should be visible in a lightbox
    await expect(page.locator('form')).toBeVisible();
    await expect(page.locator('#email')).toBeVisible();
    // Password field is NOT visible in step 1
    await expect(page.locator('#password')).not.toBeVisible();
  });

  test('should validate email format', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Fill with invalid email
    await page.fill('#email', 'invalid-email');

    // Try to continue
    await page.click('button[type="submit"]');

    // Should show validation error
    await expect(page.locator('.form-field-error')).toBeVisible({ timeout: 10000 });
  });

  test('should proceed to step 2 with valid email', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Fill with valid email
    const uniqueEmail = `test${Date.now()}@example.com`;
    await page.fill('#email', uniqueEmail);

    // Wait for async validation
    await page.waitForTimeout(1000);

    // Click continue to go to step 2
    await page.click('button[type="submit"]');

    // Password field should now be visible
    await expect(page.locator('#password')).toBeVisible({ timeout: 5000 });
  });

  test('should show password requirements error', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="register-button"]');

    // Step 1: Fill email
    const uniqueEmail = `test${Date.now()}@example.com`;
    await page.fill('#email', uniqueEmail);
    await page.waitForTimeout(1000);
    await page.click('button[type="submit"]');

    // Step 2: Fill weak password
    await expect(page.locator('#password')).toBeVisible({ timeout: 5000 });
    await page.fill('#password', '123');

    // Check rules checkbox (need to view rules first)
    await page.click('a[href="/rules"]');
    await page.waitForTimeout(500);
    await page.click('#acceptedRules');

    // Wait for bot protection
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');

    // Should show validation error
    await expect(page.locator('.form-field-error')).toBeVisible({ timeout: 10000 });
  });
});
