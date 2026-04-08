import { test, expect } from "@playwright/test";

test.describe("Forum Topics", () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to a forum board
    await page.goto("/forum/news");
    // Wait for board to load
    await page.waitForSelector(".topics-page", { timeout: 10000 });
  });

  test("should display topics list with DataTable", async ({ page }) => {
    // DataTable component should be visible
    await expect(page.locator(".data-table")).toBeVisible();
  });

  test("should display board navigation", async ({ page }) => {
    // Board navigation should show links to all boards
    await expect(page.locator(".board-navigation")).toBeVisible();
    // At least one board link should exist
    const boardLinks = page.locator(".board-link");
    const count = await boardLinks.count();
    expect(count).toBeGreaterThan(0);
  });

  test("should highlight current board in navigation", async ({ page }) => {
    // Current board should have 'active' class
    const activeBoard = page.locator(".board-link.active");
    await expect(activeBoard).toBeVisible();
  });

  test("should navigate between boards", async ({ page }) => {
    // Click on a different board
    const boards = page.locator(".board-link:not(.active)");
    const boardCount = await boards.count();

    if (boardCount > 0) {
      const targetBoard = boards.first();
      const boardText = await targetBoard.textContent();
      await targetBoard.click();

      // URL should change
      await page.waitForURL(/\/forum\//);

      // Clicked board should now be active
      await expect(page.locator(".board-link.active")).toContainText(boardText || "");
    }
  });
});

test.describe("Forum Topics Filters", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/forum/news");
    await page.waitForSelector(".topics-filter", { timeout: 10000 });
  });

  test("should display filter controls", async ({ page }) => {
    // Search input
    await expect(page.locator(".search-input")).toBeVisible();

    // Sort select
    await expect(page.locator(".sort-select")).toBeVisible();

    // Sort order button
    await expect(page.locator(".sort-order-button")).toBeVisible();
  });

  test("should filter topics by search", async ({ page }) => {
    const searchInput = page.locator(".search-input");

    // Type in search
    await searchInput.fill("test");

    // Wait for debounced search
    await page.waitForTimeout(500);

    // URL should contain search parameter
    await expect(page).toHaveURL(/search=test/);
  });

  test("should clear search when clicking clear button", async ({ page }) => {
    const searchInput = page.locator(".search-input");

    // Type in search
    await searchInput.fill("test");
    await page.waitForTimeout(500);

    // Clear button should appear
    const clearButton = page.locator(".clear-button");
    await expect(clearButton).toBeVisible();

    // Click clear
    await clearButton.click();

    // Search should be cleared
    await expect(searchInput).toHaveValue("");
  });

  test("should change sort field", async ({ page }) => {
    const sortSelect = page.locator(".sort-select");

    // Change sort to created date
    await sortSelect.selectOption("created");

    // URL should reflect sort change
    await expect(page).toHaveURL(/sortBy=created/);
  });

  test("should toggle sort order", async ({ page }) => {
    const sortButton = page.locator(".sort-order-button");

    // Click to toggle order
    await sortButton.click();

    // URL should show asc order
    await expect(page).toHaveURL(/sortOrder=asc/);

    // Click again to toggle back
    await sortButton.click();

    // URL should show desc order
    await expect(page).toHaveURL(/sortOrder=desc/);
  });

  test("should show clear filters button when filters active", async ({ page }) => {
    // Initially no clear button
    await expect(page.locator(".clear-filters-button")).not.toBeVisible();

    // Activate a filter
    await page.locator(".search-input").fill("test");
    await page.waitForTimeout(500);

    // Clear filters button should appear
    await expect(page.locator(".clear-filters-button")).toBeVisible();
  });

  test("should reset all filters when clicking clear filters", async ({ page }) => {
    // Activate filters
    await page.locator(".search-input").fill("test");
    await page.locator(".sort-select").selectOption("created");
    await page.waitForTimeout(500);

    // Click clear filters
    await page.locator(".clear-filters-button").click();

    // Search should be empty
    await expect(page.locator(".search-input")).toHaveValue("");

    // Sort should be back to default
    await expect(page.locator(".sort-select")).toHaveValue("lastActivity");
  });
});

test.describe("Forum Topics Pinned", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/forum/news");
    await page.waitForSelector(".data-table", { timeout: 10000 });
  });

  test("should display pinned topics with bold style", async ({ page }) => {
    // If there are pinned topics, they should have the 'pinned' class
    const pinnedTopics = page.locator(".topic-link.pinned");
    const count = await pinnedTopics.count();

    if (count > 0) {
      // Pinned topics should have bold font weight
      const firstPinned = pinnedTopics.first();
      await expect(firstPinned).toHaveCSS("font-weight", "700");
    }
  });

  test("should show pin icon for pinned topics", async ({ page }) => {
    const pinnedTopics = page.locator(".topic-link.pinned");
    const count = await pinnedTopics.count();

    if (count > 0) {
      // Each pinned topic should have a pin icon
      const firstPinned = pinnedTopics.first();
      await expect(firstPinned.locator(".topic-icon")).toBeVisible();
    }
  });
});

test.describe("Forum Topic Navigation", () => {
  test("should navigate to topic detail page", async ({ page }) => {
    await page.goto("/forum/news");
    await page.waitForSelector(".data-table", { timeout: 10000 });

    // Find first topic link
    const topicLink = page.locator(".topic-link").first();

    if (await topicLink.isVisible()) {
      await topicLink.click();

      // Should navigate to topic page (URL pattern: /forum/{alias}/{number})
      await expect(page).toHaveURL(/\/forum\/[^/]+\/\d+/);
    }
  });
});
