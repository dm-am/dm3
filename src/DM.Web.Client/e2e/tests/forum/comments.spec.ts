import { test, expect } from "../../fixtures/auth";
import type { Page } from "@playwright/test";

/**
 * A topic renders as a `.topic` card with its text in `.topic-description`,
 * the comments under it in `.comments-section`.
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
