import { test, expect } from "@playwright/test";

test.describe("Browse Games", () => {
  test("should display active games page", async ({ page }) => {
    await page.goto("/games");
    await expect(page.locator(".games-data-table")).toBeVisible();
  });

  test("should display games table with data", async ({ page }) => {
    await page.goto("/games");
    await expect(page.locator("#results")).toBeVisible();
  });

  test("should show game status badge in list", async ({ page }) => {
    await page.goto("/games");
    // Status badges are shown in the table
    await expect(page.locator(".status-wrapper").first()).toBeVisible();
  });
});
