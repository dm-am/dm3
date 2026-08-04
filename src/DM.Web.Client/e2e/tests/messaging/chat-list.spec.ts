import { test, expect } from "../../fixtures/auth";

/**
 * ".messenger-page" and ".chat-user" are in no template: the list is
 * `.messenger-list` with `.chats-list` inside it, and a preview names its
 * interlocutor in `.username`. The second test also hid its only assertion
 * behind `isVisible()`, so an empty messenger reported green.
 */
test.describe("Chat List", () => {
  test("should display messenger page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/messenger");

    await expect(authenticatedPage.locator(".messenger-list")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should show chat preview", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/messenger");

    // The seed writes conversations for the primary account.
    const chatItem = authenticatedPage.locator(".chat-preview").first();
    await expect(chatItem).toBeVisible({ timeout: 10000 });
    await expect(chatItem.locator(".username")).toBeVisible();
    await expect(chatItem.locator(".message-preview")).toBeVisible();
  });
});
