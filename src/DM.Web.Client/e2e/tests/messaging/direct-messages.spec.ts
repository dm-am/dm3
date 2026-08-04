import { test, expect, secondaryUser } from "../../fixtures/auth";

test.describe("Direct Messages", () => {
  test("should display chats list", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/messenger");

    // One selector, not "the list or an empty state": the seed writes
    // conversations for this account, so an empty messenger is a defect.
    await expect(authenticatedPage.locator(".chats-list")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should navigate to chat", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/messenger");

    // The click used to sit inside `if (await firstChat.isVisible())`, so the
    // navigation assertion never ran when the list came back empty — exactly
    // the case worth reporting.
    const firstChat = authenticatedPage.locator(".chat-preview").first();
    await expect(firstChat).toBeVisible({ timeout: 10000 });
    await firstChat.click();

    await expect(authenticatedPage).toHaveURL(/\/messenger\/c\//);
  });

  test("should open direct message to user", async ({ authenticatedPage }) => {
    // "Alice" is not an account the seeder writes; the second seeded account
    // is the one this redirect can resolve.
    await authenticatedPage.goto(`/messenger/user/${secondaryUser.username}`);

    await expect(authenticatedPage.locator(".chat-view")).toBeVisible({
      timeout: 10000,
    });
  });
});
