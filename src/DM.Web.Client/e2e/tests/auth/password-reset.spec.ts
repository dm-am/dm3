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

  // Runs again. The reason it was off - "requires actual email sending and a
  // success message implementation" - outlived both: the block exists and is
  // .success-content, and what is asserted here is what the page does, not
  // what the mail worker does. Asking for a recovery link writes a token and
  // nothing else, so the account it names is as usable afterwards as before.
  test("should accept email for reset", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="recovery-button"]');
    await page.fill('input[type="email"], #email', primaryUser.email);

    // Wait for bot protection
    await page.waitForTimeout(2500);

    await page.click('button[type="submit"]');
    // The form swaps itself for "Проверьте почту". ".success-message" and
    // ".confirmation" named nothing: the block is .success-content.
    await expect(page.locator(".success-content")).toBeVisible({
      timeout: 10000,
    });
  });
});
