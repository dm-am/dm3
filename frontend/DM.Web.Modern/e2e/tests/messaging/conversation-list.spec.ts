import { test, expect } from '../fixtures/auth';

test.describe('Conversation List', () => {
  test('should display messenger page', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');
    // Should see conversations list or empty state message
    await expect(
      authenticatedPage.locator('.conversations-list, .empty-conversations, .messenger-page')
    ).toBeVisible({ timeout: 10000 });
  });

  test('should show conversation preview', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');

    // If there are conversations, they should have preview info
    const conversationItem = authenticatedPage.locator('.conversation-item').first();
    if (await conversationItem.isVisible({ timeout: 5000 }).catch(() => false)) {
      // Should see user info and last message preview
      await expect(conversationItem.locator('.user-link, .conversation-user')).toBeVisible();
    }
  });
});
