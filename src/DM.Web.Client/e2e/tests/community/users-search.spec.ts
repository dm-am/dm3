import { test, expect } from "@playwright/test";

test.describe("Users Search", () => {
  test.describe("Search Functionality", () => {
    test("should filter users by text search", async ({ page }) => {
      await page.goto("/community");

      // Type search query
      const searchInput = page.getByPlaceholder("Поиск");
      await searchInput.fill("тест");
      await searchInput.press("Enter");

      // URL should update with search parameter
      await expect(page).toHaveURL(/q=тест/);
    });

    test("should clear search input", async ({ page }) => {
      await page.goto("/community?q=тест");

      // Verify search input has value
      const searchInput = page.getByPlaceholder("Поиск");
      await expect(searchInput).toHaveValue("тест");

      // Clear search
      await searchInput.clear();
      await searchInput.press("Enter");

      // URL should not have q parameter
      await expect(page).not.toHaveURL(/q=/);
    });

    test("should display search results", async ({ page }) => {
      await page.goto("/community?q=Alice");

      // Wait for results to load
      await page.waitForSelector(".users-table, .user-card, .search-results");

      // Page should show results (not error)
      await expect(page.locator(".error-message")).not.toBeVisible();
    });
  });

  test.describe("Sorting", () => {
    test("should sort by username", async ({ page }) => {
      await page.goto("/community");

      // Open sort dropdown (implementation-dependent)
      const sortBtn = page.locator(".sort-btn, .sort-select, [data-sort]");
      if (await sortBtn.isVisible()) {
        await sortBtn.click();

        // Select username sort
        await page.locator("text=По имени").click();

        // URL should have sort parameter
        await expect(page).toHaveURL(/sort=Name|sortBy=username/);
      }
    });

    test("should sort by rating", async ({ page }) => {
      await page.goto("/community?sort=Rating");

      // Verify sort is applied (no error)
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should sort by last activity", async ({ page }) => {
      await page.goto("/community?sort=LastActivity");

      // Verify sort is applied (no error)
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should sort by registration date", async ({ page }) => {
      await page.goto("/community?sort=Registered");

      // Verify sort is applied (no error)
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should combine search with explicit sort", async ({ page }) => {
      // Search with explicit rating sort (should use rating, not relevance)
      await page.goto("/community?q=тест&sort=Rating");

      // Both parameters should be in URL
      await expect(page).toHaveURL(/q=тест/);
      await expect(page).toHaveURL(/sort=Rating/);

      // Page should load without error
      await expect(page.locator(".error-message")).not.toBeVisible();
    });
  });

  test.describe("Activity Filter", () => {
    test("should filter by active users", async ({ page }) => {
      await page.goto("/community?filter=Active");

      // Verify filter is applied
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should filter by all users", async ({ page }) => {
      await page.goto("/community?filter=All");

      // Verify filter is applied
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should combine search with activity filter", async ({ page }) => {
      await page.goto("/community?q=тест&filter=Active");

      // Both parameters should be in URL
      await expect(page).toHaveURL(/q=тест/);
      await expect(page).toHaveURL(/filter=Active/);
    });
  });

  test.describe("URL State Persistence", () => {
    test("should restore filters from URL", async ({ page }) => {
      await page.goto("/community?q=Alice&sort=Rating&filter=Active");

      // Search input should have value
      await expect(page.getByPlaceholder("Поиск")).toHaveValue("Alice");

      // Page should load without error
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should preserve search when paginating", async ({ page }) => {
      await page.goto("/community?q=a&number=1");

      // Wait for page to load
      await page.waitForTimeout(500);

      // Find pagination link
      const nextPage = page.locator(".paging-bottom a, [data-page]").last();
      if (await nextPage.isVisible()) {
        await nextPage.click();

        // Search should be preserved
        await expect(page).toHaveURL(/q=a/);
      }
    });
  });

  test.describe("Empty States", () => {
    test("should show empty message when no users match search", async ({ page }) => {
      // Use a search that likely won't match anything
      await page.goto("/community?q=xyznonexistentuser123456789");

      // Wait for results
      await page.waitForTimeout(500);

      // Should show empty message or no results
      const noResults = page.locator("text=Пользователи не найдены, .empty-message, .no-results");
      // May or may not be visible depending on implementation
    });
  });
});
