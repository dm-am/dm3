import { test, expect } from "@playwright/test";

/** The forum index is a DataTable whose title cell is a `.board-link`. */
test.describe("Forum Index", () => {
  test("should display forum index page", async ({ page }) => {
    await page.goto("/forum");
    // exact, because the sidebar block is also titled "Форум" and a substring
    // match resolves to two headings at once.
    await expect(
      page.getByRole("heading", { name: "Форум", exact: true }),
    ).toBeVisible();
    await expect(page.locator("table.data-table")).toBeVisible({
      timeout: 10000,
    });
  });

  test("should list the boards", async ({ page }) => {
    await page.goto("/forum");
    // The seed writes boards, so an empty table is a defect, not a state.
    // Scoped to the table: the sidebar carries the same class on list items.
    await expect(
      page.locator("table.data-table a.board-link").first(),
    ).toBeVisible({
      timeout: 10000,
    });
  });

  test("should navigate to board", async ({ page }) => {
    await page.goto("/forum");

    const firstBoard = page.locator("table.data-table a.board-link").first();
    await expect(firstBoard).toBeVisible({ timeout: 10000 });
    await firstBoard.click();

    await expect(page).toHaveURL(/\/forum\//);
    await expect(page.locator(".topics-page")).toBeVisible({ timeout: 10000 });
  });
});
