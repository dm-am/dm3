import { test, expect } from "@playwright/test";
import { primaryUser } from "../../fixtures/auth";

test.describe("Password Reset Flow", () => {
  test("should show form fields", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="recovery-button"]');
    await expect(page.locator("form")).toBeVisible({ timeout: 10000 });
    await expect(page.locator('input[type="email"], #email')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  // Asking for a recovery link writes a token and nothing else, so the account
  // it names stays as usable as before.
  test("should accept email for reset", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="recovery-button"]');
    await page.fill('input[type="email"], #email', primaryUser.email);

    await page.click('button[type="submit"]');
    // The form swaps itself for "Проверьте почту", named by its heading.
    await expect(
      page.getByRole("heading", { name: "Проверьте почту" }),
    ).toBeVisible({ timeout: 10000 });
  });
});
