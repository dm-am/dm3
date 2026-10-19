import { test, expect, secondaryUser } from "../../fixtures/auth";

test.describe("Direct Messages", () => {
  test("should navigate to chat", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/messenger");

    const firstChat = authenticatedPage.locator(".chat-preview").first();
    await expect(firstChat).toBeVisible({ timeout: 10000 });
    await firstChat.click();

    await expect(authenticatedPage).toHaveURL(/\/messenger\/c\//);
  });

  test("should open direct message to user", async ({ authenticatedPage }) => {
    await authenticatedPage.goto(`/messenger/user/${secondaryUser.username}`);

    await expect(authenticatedPage.locator(".chat-view")).toBeVisible({
      timeout: 10000,
    });
  });
});
