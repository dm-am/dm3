import { test, expect } from "../../fixtures/auth";

/**
 * ".polls-list" exists; ".poll-item", ".the-poll", ".sidebar-poll",
 * ".poll-option", ".poll-answer", ".poll-results" and ".voted" do not. A poll
 * is a `.poll` card — `.poll-card` on /polls, straight inside
 * `#sidebar-list-ActivePolls` in the sidebar — whose rows are
 * `.poll-option-row` and whose vote control is `.poll-option-vote`. Both
 * bodies sat behind `isVisible()` on a locator that could never match, so
 * neither ever ran.
 */
test.describe("Polls", () => {
  test("should display polls page", async ({ page }) => {
    await page.goto("/polls");
    await expect(page.getByRole("heading", { name: "Опросы" })).toBeVisible();
    await expect(page.locator(".polls-list")).toBeVisible({ timeout: 10000 });
    await expect(page.locator(".poll-card").first()).toBeVisible();
  });

  test("should show poll on sidebar", async ({ page }) => {
    await page.goto("/");
    const poll = page.locator("#sidebar-list-ActivePolls .poll").first();

    await expect(poll).toBeVisible({ timeout: 10000 });
    await expect(poll.locator(".poll-option-row").first()).toBeVisible();
  });

  test.skip("should vote on poll when authenticated", async ({
    authenticatedPage,
  }) => {
    // Switched off, and the reason is no longer the selectors: a vote is a
    // write against the shared seed, and the corpus runs every spec on the
    // same database with no per-test isolation to undo it.
    await authenticatedPage.goto("/");
    const poll = authenticatedPage
      .locator("#sidebar-list-ActivePolls .poll")
      .first();
    await expect(poll).toBeVisible();

    await poll.locator(".poll-option-vote").first().click();
    await expect(poll.locator(".poll-option-voted")).toBeVisible({
      timeout: 5000,
    });
  });
});
