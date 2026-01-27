import { test, expect } from '../fixtures/auth';

test.describe('Direct Messages', () => {
  test('should display conversations list', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');
    // Should see conversations list or empty state
    await expect(
      authenticatedPage.locator('.conversations-list, .empty-conversations')
    ).toBeVisible({ timeout: 10000 });
  });

  test('should navigate to conversation', async ({ authenticatedPage }) => {
    await authenticatedPage.goto('/messenger');
    await authenticatedPage.waitForSelector('.conversation-item', { timeout: 10000 });

    // Click first conversation if exists
    const firstConversation = authenticatedPage.locator('.conversation-item').first();
    if (await firstConversation.isVisible()) {
      await firstConversation.click();
      await expect(authenticatedPage).toHaveURL(/\/messenger\/c\//);
    }
  });

  test('should open direct message to user', async ({ authenticatedPage }) => {
    // Navigate to a user's direct message page
    await authenticatedPage.goto('/messenger/user/Alice');
    await expect(authenticatedPage.locator('.conversation-view')).toBeVisible({ timeout: 10000 });
  });
});
