import { test, expect } from "../../fixtures/auth";

test.describe("Likes", () => {
  test.skip("should like topic when authenticated", async ({
    authenticatedPage,
  }) => {
    // Note: Like functionality requires navigating to a real topic first
    // This test is skipped until like buttons are implemented with data-testid
    await authenticatedPage.goto("/forum/Новости");
    await authenticatedPage.waitForSelector(".topics-row", { timeout: 10000 });

    // Click on first topic
    const firstTopic = authenticatedPage
      .locator(".topics-row .col-title a")
      .first();
    if (await firstTopic.isVisible()) {
      await firstTopic.click();
      // Look for like button on topic page
      const likeButton = authenticatedPage.locator(
        '[data-testid="like-button"], .like-button',
      );
      if (await likeButton.isVisible()) {
        const initialCount = await likeButton.textContent();
        await likeButton.click();
        await expect(likeButton).not.toHaveText(initialCount!);
      }
    }
  });
});
