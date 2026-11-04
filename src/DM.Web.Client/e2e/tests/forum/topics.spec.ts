import { test, expect } from "@playwright/test";

/**
 * Board and filter of a forum board. The search box is `.search-container
 * .the-input` with a `.clear-input-btn`, the sort is a SortButton (`.sort-btn`
 * opening `.sort-dropdown` of `.sort-option`).
 */
const BOARD = "/forum/general";

test.describe("Forum Topics", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(BOARD);
    await page.waitForSelector(".topics-page", { timeout: 10000 });
  });

  test("should highlight current board in navigation", async ({ page }) => {
    await expect(page.locator(".board-link.active")).toHaveText("Общий");
  });

  test("should navigate between boards", async ({ page }) => {
    // The seed writes ten boards, so there is always another one. Scoped to the
    // page navigation: the sidebar carries the same class on list items, which
    // are not links, and those come first in the document.
    const navigation = page.locator(".board-navigation");
    const targetBoard = navigation.locator(".board-link:not(.active)").first();
    const boardText = await targetBoard.textContent();
    await targetBoard.click();

    await page.waitForURL(/\/forum\//);
    await expect(navigation.locator(".board-link.active")).toContainText(
      boardText ?? "",
    );
  });
});

test.describe("Forum Topics Filters", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(BOARD);
    await page.waitForSelector(".topics-filter", { timeout: 10000 });
  });

  test("should display filter controls", async ({ page }) => {
    await expect(page.locator(".topics-filter .the-input")).toBeVisible();
    await expect(page.locator(".topics-filter .sort-btn")).toBeVisible();
  });

  test("should filter topics by search", async ({ page }) => {
    await page.locator(".topics-filter .the-input").fill("test");

    await expect(page).toHaveURL(/search=test/, { timeout: 5000 });
  });

  test("should clear search when clicking clear button", async ({ page }) => {
    const searchInput = page.locator(".topics-filter .the-input");
    await searchInput.fill("test");
    await expect(page).toHaveURL(/search=test/, { timeout: 5000 });

    await page.locator(".topics-filter .clear-input-btn").click();

    await expect(searchInput).toHaveValue("");
    await expect(page).not.toHaveURL(/search=/);
  });

  test("should change sort field", async ({ page }) => {
    await page.locator(".topics-filter .sort-btn").click();
    await page
      .locator(".topics-filter .sort-option")
      .filter({ hasText: "Дата создания" })
      .click();

    await expect(page).toHaveURL(/sortBy=created/);
  });

  // The direction control lives inside the dropdown, which toggleSortOrder
  // closes (SortButton.vue), so each click reopens it; and the URL carries the
  // sort order only while it is not the default.
  test("should toggle sort order", async ({ page }) => {
    const sortButton = page.locator(".topics-filter .sort-btn");
    const direction = page.locator(".topics-filter .sort-direction");

    await sortButton.click();
    await direction.click();
    await expect(page).toHaveURL(/sortOrder=asc/);

    await sortButton.click();
    await direction.click();
    await expect(page).not.toHaveURL(/sortOrder=/);
  });
});

test.describe("Forum Topics Pinned", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(BOARD);
    await page.waitForSelector(".data-table", { timeout: 10000 });
  });

  test("should display pinned topics with bold style", async ({ page }) => {
    // Both topics the seed writes into this board are pinned.
    const firstPinned = page.locator(".topic-link.pinned").first();

    await expect(firstPinned).toBeVisible();
    await expect(firstPinned).toHaveCSS("font-weight", "700");
  });

  test("should show pin icon for pinned topics", async ({ page }) => {
    const firstPinned = page.locator(".topic-link.pinned").first();

    await expect(firstPinned.locator(".topic-icon")).toBeVisible();
  });
});

test.describe("Forum Topic Navigation", () => {
  test("should navigate to topic detail page", async ({ page }) => {
    await page.goto(BOARD);
    await page.waitForSelector(".data-table", { timeout: 10000 });

    const topicLink = page.locator(".topic-link").first();
    await expect(topicLink).toBeVisible();
    await topicLink.click();

    // URL pattern: /forum/{alias}/{number}
    await expect(page).toHaveURL(/\/forum\/[^/]+\/\d+/);
  });
});
