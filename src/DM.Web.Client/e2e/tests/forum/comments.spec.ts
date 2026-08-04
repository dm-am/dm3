import { test, expect } from "../../fixtures/auth";
import type { Page } from "@playwright/test";

/**
 * ".topics-row", ".topic-content", ".topic-opening" and ".opening-text" are
 * in no template: the topics table is a DataTable whose title cell holds an
 * `a.topic-link`, and a topic renders as a `.topic` card with its text in
 * `.topic-description`, the comments under it in `.comments-section`.
 * "Новости" is not a board alias either — the seed writes "general" and nine
 * more. Both tests clicked behind an `if` that was never true.
 */

/** Opens the first topic of the seeded "general" board. */
async function openFirstTopic(page: Page): Promise<void> {
  await page.goto("/forum/general");
  const firstTopic = page.locator("td.col-title a.topic-link").first();
  await expect(firstTopic).toBeVisible({ timeout: 10000 });
  await firstTopic.click();
  await expect(page).toHaveURL(/\/forum\/general\/\d+/);
}

test.describe("Forum Comments", () => {
  test("should display topic page with comments", async ({ page }) => {
    await openFirstTopic(page);

    await expect(page.locator(".comments-section")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should show topic opening text", async ({ page }) => {
    await openFirstTopic(page);

    const opening = page.locator(".topic").first();
    await expect(opening.locator(".topic-title")).toBeVisible();
    await expect(opening.locator(".topic-description")).toBeVisible();
  });
});
