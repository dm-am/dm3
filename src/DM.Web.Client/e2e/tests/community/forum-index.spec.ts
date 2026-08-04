import { test, expect } from "@playwright/test";

/**
 * ".forum-index", ".boards-list", ".forum-section", ".forum-group",
 * ".forum-link", ".forum-title" and ".topics-table" are in no template of the
 * client. The index is a DataTable whose title cell is a `.board-link`, the
 * heading comes from the forum shell, and a board's topics render inside
 * `.topics-page`. The old file asked for all seven and clicked behind an
 * `if`, so it reported green on an empty page.
 */
test.describe("Forum Index", () => {
  test("should display forum index page", async ({ page }) => {
    await page.goto("/forum");
    await expect(page.getByRole("heading", { name: "Форум" })).toBeVisible();
    await expect(page.locator("table.data-table")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should list the boards", async ({ page }) => {
    await page.goto("/forum");
    // The seed writes boards, so an empty table is a defect, not a state.
    await expect(page.locator(".board-link").first()).toBeVisible({
      timeout: 10000,
    });
  });

  test("should navigate to board", async ({ page }) => {
    await page.goto("/forum");

    const firstBoard = page.locator(".board-link").first();
    await expect(firstBoard).toBeVisible({ timeout: 10000 });
    await firstBoard.click();

    await expect(page).toHaveURL(/\/forum\//);
    await expect(page.locator(".topics-page")).toBeVisible({ timeout: 10000 });
  });
});
