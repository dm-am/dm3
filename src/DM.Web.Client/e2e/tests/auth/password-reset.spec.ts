import { test, expect } from "@playwright/test";
import { primaryUser } from "../../fixtures/auth";

test.describe("Password Reset Flow", () => {
  test("should show password reset form", async ({ page }) => {
    await page.goto("/");
    // Password reset is a separate button in header
    await page.click('[data-testid="recovery-button"]');
    // Should see email input in lightbox form
    await expect(page.locator('#email, input[type="email"]')).toBeVisible({
      timeout: 10000,
    });
  });

  test("should show form fields", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="recovery-button"]');
    // Should see the password reset form
    await expect(page.locator("form")).toBeVisible({ timeout: 10000 });
    await expect(page.locator('input[type="email"], #email')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test.skip("should accept email for reset", async ({ page }) => {
    // Skipped: requires actual email sending and success message implementation
    await page.goto("/");
    await page.click('[data-testid="recovery-button"]');
    await page.fill('input[type="email"], #email', primaryUser.email);

    // Wait for bot protection
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');
    // Should show success or confirmation message
    await expect(page.locator(".success-message, .confirmation")).toBeVisible({
      timeout: 10000,
    });
  });
});
