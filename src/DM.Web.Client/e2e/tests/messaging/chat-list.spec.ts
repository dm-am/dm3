import { test, expect } from "../../fixtures/auth";

/**
 * The messenger list is `.messenger-list` with `.chats-list` inside it, and a
 * preview names its interlocutor in `.name`.
 */
test.describe("Chat List", () => {
  test("should show chat preview", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/messenger");

    // The seed writes conversations for the primary account.
    const chatItem = authenticatedPage.locator(".chat-preview").first();
    await expect(chatItem).toBeVisible({ timeout: 10000 });
    await expect(chatItem.locator(".name")).toBeVisible();
    await expect(chatItem.locator(".message-preview")).toBeVisible();
  });
});
