import { test, expect } from "@playwright/test";

/**
 * Board and filter of a forum board.
 *
 * Two things were wrong here at once. "news" is not a board alias — the seed
 * writes "general" and nine more — and the filter selectors named a control
 * set the client does not have: ".search-input", ".sort-select",
 * ".sort-order-button", ".clear-button", ".clear-filters-button". The search
 * box is `.search-container .the-input` with a `.clear-input-btn`, the sort
 * is a SortButton (`.sort-btn` opening `.sort-dropdown` of `.sort-option`),
 * and there is no "clear filters" control for a search at all: the bubbles
 * row with its `.clear-all-link` only appears for an author or date filter.
 * The two tests written against it asserted a button that cannot exist.
 */
const BOARD = "/forum/general";

test.describe("Forum Topics", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(BOARD);
    await page.waitForSelector(".topics-page", { timeout: 10000 });
  });

  test("should display topics list with DataTable", async ({ page }) => {
    await expect(page.locator(".data-table")).toBeVisible();
  });

  test("should display board navigation", async ({ page }) => {
    await expect(page.locator(".board-navigation")).toBeVisible();
    await expect(page.locator(".board-link").first()).toBeVisible();
  });

  test("should highlight current board in navigation", async ({ page }) => {
    await expect(page.locator(".board-link.active")).toHaveText("Общий");
  });

  test("should navigate between boards", async ({ page }) => {
    // The seed writes ten boards, so there is always another one.
    const targetBoard = page.locator(".board-link:not(.active)").first();
    const boardText = await targetBoard.textContent();
    await targetBoard.click();

    await page.waitForURL(/\/forum\//);
    await expect(page.locator(".board-link.active")).toContainText(
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

  // Two things this test used to get wrong, and both made it red on correct
  // code. The direction control lives inside the dropdown, which is a v-if and
  // which toggleSortOrder closes (SortButton.vue), so the second click had no
  // element to land on; and the URL carries the sort order only while it is not
  // the default, so "back to desc" is an absent parameter and never
  // `sortOrder=desc`.
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
    // Both topics the seed writes into this board are pinned, so the guard
    // this test used to hide behind was never doing anything but hiding.
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
