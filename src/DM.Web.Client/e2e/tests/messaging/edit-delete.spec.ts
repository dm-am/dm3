import { test, expect } from "../../fixtures/auth";

test.describe("Message Edit/Delete", () => {
  test.skip("should edit message", async ({ authenticatedPage }) => {
    // Note: This test requires existing messages and edit functionality
    // Skipped until edit/delete buttons are implemented with data-testid
    await authenticatedPage.goto("/messenger");
    await authenticatedPage.waitForSelector(".chat-preview", {
      timeout: 10000,
    });

    const firstChat = authenticatedPage.locator(".chat-preview").first();
    if (await firstChat.isVisible()) {
      await firstChat.click();

      // Wait for messages to load
      await authenticatedPage.waitForSelector(".message-item", {
        timeout: 10000,
      });

      // Hover over first message and look for edit button
      await authenticatedPage.locator(".message-item").first().hover();
      const editButton = authenticatedPage.locator(
        '[data-testid="edit-message"]',
      );
      if (await editButton.isVisible()) {
        await editButton.click();
        await authenticatedPage.fill(".message-editor", "Edited message");
        await authenticatedPage.click('[data-testid="save-edit"]');
        await expect(authenticatedPage.locator(".message-item")).toContainText(
          "Edited message",
        );
      }
    }
  });
});
