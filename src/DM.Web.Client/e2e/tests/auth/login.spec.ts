import { test, expect, type Page } from "@playwright/test";
import { primaryUser } from "../../fixtures/auth";

/**
 * The one place that drives the login form through the UI. Everything else in
 * the tier adopts a session from global setup, so these three are what would
 * catch the form itself breaking.
 */

/** Signs in through the form as the primary account, with a password given. */
async function signIn(page: Page, password: string) {
  await page.goto("/");
  await page.click('[data-testid="login-button"]');
  await page.fill("#email", primaryUser.email);
  await page.fill("#password", password);
  // The form sits out its least fill time itself (useBotTrap): an early press
  // is held, not refused.
  await page.click('button[type="submit"]');
}

test.describe("Login Flow", () => {
  test("should login with valid credentials", async ({ page }) => {
    await signIn(page, primaryUser.password);

    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible({
      timeout: 10000,
    });
  });

  test("should show error with invalid credentials", async ({ page }) => {
    // A real address with the wrong password, so the server answers about the
    // password rather than about the account. An address nobody registered
    // produces the same deliberately indistinguishable answer, which would make
    // this test pass without exercising the rejection path.
    await signIn(page, "definitely-not-the-password");

    // The refusal goes into the strip under a field, and the strip turns red
    // with it. The strips are rendered empty too, so the red state is what is
    // waited for.
    await expect(page.locator(".auth-message--error").first()).toBeVisible({
      timeout: 10000,
    });
  });

  test("should logout successfully", async ({ page }) => {
    await signIn(page, primaryUser.password);
    await expect(page.locator('[data-testid="user-menu"]')).toBeVisible({
      timeout: 10000,
    });

    // "Выйти" opens a menu — the local sign-out and the sign-out-everywhere
    // action sit inside it, and the first of the two is what this test drives.
    await page.click('[data-testid="logout-button"]');
    await page.click('[data-testid="logout-current"]');

    await expect(page.locator('[data-testid="login-button"]')).toBeVisible({
      timeout: 10000,
    });
  });
});
