import { test, expect } from "@playwright/test";
import { primaryUser } from "../../fixtures/auth";

/**
 * The one place that drives the login form through the UI. Everything else in
 * the tier adopts a session from global setup, so these three are what would
 * catch the form itself breaking.
 *
 * Credentials come from the shared fixture. They used to be a hardcoded
 * "alice@example.com" that the seeder never creates, which the fixture's own
 * comment describes as fixed — it was fixed for the specs that use the fixture
 * and missed here, so two of these three asserted that a user menu appears after
 * signing in as a non-existent account and failed on every run.
 *
 * Each test signs in at most once: the auth endpoint allows five attempts a
 * minute per address, and global setup has already spent one.
 */
test.describe("Login Flow", () => {
  test("should login with valid credentials", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="login-button"]');

    await page.fill("#email", primaryUser.email);
    await page.fill("#password", primaryUser.password);

    // The form holds submit for two seconds as bot protection; clicking sooner
    // is rejected and the test would read that as bad credentials.
    await page.waitForTimeout(2500);
    await page.click('button[type="submit"]');

    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible({
      timeout: 10000,
    });
  });

  test("should show error with invalid credentials", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="login-button"]');

    // A real address with the wrong password, so the server answers about the
    // password rather than about the account. An address nobody registered
    // produces the same deliberately indistinguishable answer, which would make
    // this test pass without exercising the rejection path.
    await page.fill("#email", primaryUser.email);
    await page.fill("#password", "definitely-not-the-password");

    await page.waitForTimeout(2500);
    await page.click('button[type="submit"]');

    await expect(page.locator(".form-field-error")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should logout successfully", async ({ page }) => {
    await page.goto("/");
    await page.click('[data-testid="login-button"]');
    await page.fill("#email", primaryUser.email);
    await page.fill("#password", primaryUser.password);

    await page.waitForTimeout(2500);
    await page.click('button[type="submit"]');
    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible({
      timeout: 10000,
    });

    // The logout control is directly visible, not inside a dropdown.
    await page.click('[data-testid="logout-button"]');

    await expect(page.locator('[data-testid="login-button"]')).toBeVisible({
      timeout: 10000,
    });
  });
});
