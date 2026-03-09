import { test, expect } from '../fixtures/auth';

test.describe('Direct Messages', () => {
  test('should display chats list', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');
    // Should see chats list or empty state
    await expect(
      authenticatedPage.locator('.chats-list, .empty-state')
    ).toBeVisible({ timeout: 10000 });
  });

  test('should navigate to chat', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');
    await authenticatedPage.waitForSelector('.chat-preview', { timeout: 10000 });

    // Click first chat if exists
    const firstChat = authenticatedPage.locator('.chat-preview').first();
    if (await firstChat.isVisible()) {
      await firstChat.click();
      await expect(authenticatedPage).toHaveURL(/\/messenger\/c\//);
    }
  });

  test('should open direct message to user', async ({ authenticatedPage }) => {
    // Navigate to a user's direct message page
    await authenticatedPage.goto('/messenger/user/Alice');
    await expect(authenticatedPage.locator('.chat-view')).toBeVisible({ timeout: 10000 });
  });
});
