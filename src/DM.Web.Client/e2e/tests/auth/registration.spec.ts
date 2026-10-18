import { test, expect } from "@playwright/test";

/**
 * The registration card names its fields after itself: #registration-email and
 * #registration-password. The bare #email and #password belong to the login
 * card, so a spec that asked for them here waited thirty seconds for a field
 * this card does not have and reported it as a broken form.
 *
 * The answer to a field sits in the strip under it (#registration-email-message)
 * and turns red with the auth-message--error class. The strip is rendered empty
 * as well, so what is waited for is the strip in its red state, not the strip.
 */
test.describe("Registration Flow", () => {
  test("should show registration form step 1 (email)", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="register-button"]');

    // Registration form should be visible in a lightbox
    await expect(page.locator("form")).toBeVisible();
    await expect(page.locator("#registration-email")).toBeVisible();
    // Password field is NOT visible in step 1
    await expect(page.locator("#registration-password")).not.toBeVisible();
  });

  test("should validate email format", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="register-button"]');

    // Fill with invalid email
    await page.fill("#registration-email", "invalid-email");

    // Leave the field. The validator runs on blur, and the submit button stays
    // disabled until it has: clicking it while the field still holds focus
    // waits for a control that will never be enabled.
    await page.locator("#registration-email").blur();

    // Should show validation error
    await expect(
      page.locator("#registration-email-message.auth-message--error"),
    ).toBeVisible({ timeout: 10000 });
  });

  test("should proceed to step 2 with valid email", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="register-button"]');

    // Fill with valid email
    const uniqueEmail = `test${Date.now()}@example.com`;
    await page.fill("#registration-email", uniqueEmail);

    // Leave the field: the availability check runs on blur, and the submit
    // button is disabled until it has answered.
    await page.locator("#registration-email").blur();
    await page.waitForTimeout(1000);

    // Click continue to go to step 2
    await page.click('button[type="submit"]');

    // Password field should now be visible
    await expect(page.locator("#registration-password")).toBeVisible({
      timeout: 5000,
    });
  });

  test("refuses to register on a password that is too short", async ({
    page,
  }) => {
    await page.goto("/");
    await page.click('[data-testid="register-button"]');

    // Step 1: Fill email
    const uniqueEmail = `test${Date.now()}@example.com`;
    await page.fill("#registration-email", uniqueEmail);
    await page.locator("#registration-email").blur();
    await page.waitForTimeout(1000);
    await page.click('button[type="submit"]');

    // Step 2: Fill weak password
    await expect(page.locator("#registration-password")).toBeVisible({
      timeout: 5000,
    });
    await page.fill("#registration-password", "123");

    await page.locator("#registration-password").blur();

    // What this form actually does with a short password, which is not what the
    // old body asserted. There is no error strip to wait for: that slot carries
    // the refusal the server sends back, and nothing is sent. The requirement
    // is stated up front as a hint, the strength indicator answers while you
    // type, and the button stays disabled - so the previous version waited for
    // a button that will never be enabled and died at the test timeout, thirty
    // seconds per run, reporting it as a missing error message.
    await expect(page.getByText("Минимум 8 символов").first()).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeDisabled();
  });
});
