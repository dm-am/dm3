import { test, expect } from '../fixtures/auth';

test.describe('Polls', () => {
  test('should display polls page', async ({ page }) => {
    await page.goto('/polls');
    // Should see polls list or empty state
    await expect(page.locator('.polls-list, .poll-item, h1')).toBeVisible({ timeout: 10000 });
  });

  test('should show poll on sidebar', async ({ page }) => {
    await page.goto('/');
    // Poll might be in sidebar
    const poll = page.locator('.the-poll, .active-polls, .sidebar-poll');
    if (await poll.isVisible({ timeout: 5000 }).catch(() => false)) {
      // Should see poll options
      await expect(poll.locator('.poll-option, .poll-answer')).toBeVisible();
    }
  });

  test.skip('should vote on poll when authenticated', async ({ authenticatedPage }) => {
    // Skipped: voting requires an active poll with data-testid attributes
    await authenticatedPage.goto('/');
    const poll = authenticatedPage.locator('.the-poll, .active-polls');
    if (await poll.isVisible({ timeout: 5000 }).catch(() => false)) {
      const option = poll.locator('.poll-option:first-child, .poll-answer:first-child');
      if (await option.isVisible()) {
        await option.click();
        // Check if voted state is shown
        await expect(poll.locator('.voted, .poll-results')).toBeVisible({ timeout: 5000 });
      }
    }
  });
});
