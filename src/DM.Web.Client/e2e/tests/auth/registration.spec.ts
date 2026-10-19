import { test, expect, type Page } from "@playwright/test";

/**
 * The registration card names its fields after itself: #registration-email and
 * #registration-password; the bare #email and #password belong to the login card.
 *
 * The answer to a field sits in the strip under it (#registration-email-message)
 * and turns red with the auth-message--error class. The strip is rendered empty
 * as well, so what is waited for is the strip in its red state, not the strip.
 */

/** Opens the card and sends it a fresh address, the first step. */
async function submitAddress(page: Page) {
  await page.goto("/");
  await page.click('[data-testid="register-button"]');
  await page.fill("#registration-email", `test${Date.now()}@example.com`);
  // Leave the field: the availability check runs on blur and holds the button.
  await page.locator("#registration-email").blur();
  await expect(page.locator('button[type="submit"]')).toBeEnabled();
  await page.click('button[type="submit"]');
}

test.describe("Registration Flow", () => {
  test("should show registration form step 1 (email)", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="register-button"]');

    await expect(page.locator("form")).toBeVisible();
    await expect(page.locator("#registration-email")).toBeVisible();
    await expect(page.locator("#registration-password")).not.toBeVisible();
  });

  test("should validate email format", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="register-button"]');

    await page.fill("#registration-email", "invalid-email");

    // The validator runs on blur.
    await page.locator("#registration-email").blur();

    await expect(
      page.locator("#registration-email-message.auth-message--error"),
    ).toBeVisible({ timeout: 10000 });
  });

  test("should proceed to step 2 with valid email", async ({ page }) => {
    await submitAddress(page);

    await expect(page.locator("#registration-password")).toBeVisible({
      timeout: 5000,
    });
  });

  test("refuses to register on a password that is too short", async ({
    page,
  }) => {
    await submitAddress(page);

    await expect(page.locator("#registration-password")).toBeVisible({
      timeout: 5000,
    });
    await page.fill("#registration-password", "123");

    await page.locator("#registration-password").blur();

    // Nothing is sent: the hint states the requirement, the strength indicator
    // answers while typing, and the button stays disabled.
    await expect(page.getByText("Минимум 8 символов").first()).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeDisabled();
  });
});
