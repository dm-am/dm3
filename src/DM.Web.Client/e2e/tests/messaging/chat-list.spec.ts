import { test, expect } from '../fixtures/auth';

test.describe('Chat List', () => {
  test('should display messenger page', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');
    // Should see chats list or empty state message
    await expect(
      authenticatedPage.locator('.chats-list, .empty-state, .messenger-page')
    ).toBeVisible({ timeout: 10000 });
  });

  test('should show chat preview', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');

    // If there are chats, they should have preview info
    const chatItem = authenticatedPage.locator('.chat-preview').first();
    if (await chatItem.isVisible({ timeout: 5000 }).catch(() => false)) {
      // Should see user info and last message preview
      await expect(chatItem.locator('.username, .chat-user')).toBeVisible();
    }
  });
});
