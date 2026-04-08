import { test, expect } from "../../fixtures/auth";

test.describe("Forum Comments", () => {
  // Note: Topic ID is a GUID, need to navigate via forum first

  test("should display topic page with comments", async ({ page }) => {
    // First go to forum to find a topic
    await page.goto("/forum/Новости");
    await page.waitForSelector(".topics-row", { timeout: 10000 });

    // Click on first topic to get to comments
    const firstTopic = page.locator(".topics-row .col-title a").first();
    if (await firstTopic.isVisible()) {
      await firstTopic.click();
      await expect(page).toHaveURL(/\/topic\//);
      // Topic page should have comments section
      await expect(page.locator(".comments-list, .topic-content")).toBeVisible({
        timeout: 10000,
      });
    }
  });

  test("should show topic opening text", async ({ page }) => {
    await page.goto("/forum/Новости");
    await page.waitForSelector(".topics-row", { timeout: 10000 });

    const firstTopic = page.locator(".topics-row .col-title a").first();
    if (await firstTopic.isVisible()) {
      await firstTopic.click();
      // Should see the topic opening (first post)
      await expect(page.locator(".topic-opening, .opening-text")).toBeVisible({
        timeout: 10000,
      });
    }
  });
});
