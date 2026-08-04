import { test, expect } from "@playwright/test";

/**
 * The community list, addressed the way the filter addresses itself.
 *
 * What this file was: `q`, `sort` and `filter` in every URL. The filter reads
 * none of them — useUsersFilter binds `search`, `activity`, `role`, `sortBy` and
 * `sortOrder` (buildQueryFromState / parseQueryToState) — so every one of those
 * navigations asked for the plain unfiltered list, and the tests that then
 * asserted "no .error-message" passed on a page that had never been filtered.
 * Their titles named a feature; their bodies checked that the page rendered.
 *
 * Two more rules the URL follows and the assertions follow with it: a parameter
 * is written only while it differs from the default (sortBy is lastActivity,
 * activity is all), and the direction comes with the field — choosing "Имя"
 * sets asc because that option declares it.
 */
test.describe("Users Search", () => {
  test.describe("Search Functionality", () => {
    test("should filter users by text search", async ({ page }) => {
      await page.goto("/community");

      const searchInput = page.getByPlaceholder("Поиск");
      await searchInput.fill("тест");
      await searchInput.press("Enter");

      await expect(page).toHaveURL(/search=/);
    });

    test("should clear search input", async ({ page }) => {
      await page.goto("/community?search=тест");

      const searchInput = page.getByPlaceholder("Поиск");
      await expect(searchInput).toHaveValue("тест");

      await searchInput.clear();
      await searchInput.press("Enter");

      await expect(page).not.toHaveURL(/search=/);
    });

    test("should display search results", async ({ page }) => {
      await page.goto("/community?search=a");

      // ".users-table", ".user-card" and ".search-results" name nothing: the
      // widget renders .users-data-table.
      await expect(page.locator(".users-data-table")).toBeVisible();
      await expect(page.locator(".error-message")).not.toBeVisible();
    });
  });

  test.describe("Sorting", () => {
    test("should sort by username", async ({ page }) => {
      await page.goto("/community");

      // The control is the SortButton: a .sort-btn opening a .sort-dropdown
      // of .sort-option items. ".sort-select" and "[data-sort]" are not in
      // the client, and the option reads "Имя", not "По имени" — so the
      // guard was false and the body never ran.
      await page.locator(".sort-btn").click();
      await page
        .locator(".sort-option")
        .filter({ hasText: "Имя" })
        .first()
        .click();

      await expect(page).toHaveURL(/sortBy=username/);
      // The field carries its own direction: username declares asc.
      await expect(page).toHaveURL(/sortOrder=asc/);
    });

    test("should sort by rating", async ({ page }) => {
      await page.goto("/community?sortBy=rating");

      await expect(page.locator(".users-data-table")).toBeVisible();
      await expect(page).toHaveURL(/sortBy=rating/);
    });

    test("should keep the default sort out of the address", async ({
      page,
    }) => {
      await page.goto("/community");

      await expect(page.locator(".users-data-table")).toBeVisible();
      // lastActivity is the default, and a default is not written down.
      await expect(page).not.toHaveURL(/sortBy=/);
    });

    test("should sort by registration date", async ({ page }) => {
      await page.goto("/community?sortBy=registered");

      await expect(page.locator(".users-data-table")).toBeVisible();
      await expect(page).toHaveURL(/sortBy=registered/);
    });

    test("should combine search with explicit sort", async ({ page }) => {
      await page.goto("/community?search=тест&sortBy=rating");

      await expect(page.getByPlaceholder("Поиск")).toHaveValue("тест");
      await expect(page).toHaveURL(/sortBy=rating/);
      await expect(page.locator(".error-message")).not.toBeVisible();
    });
  });

  test.describe("Activity Filter", () => {
    test("should filter by active users", async ({ page }) => {
      await page.goto("/community?activity=active");

      await expect(page.locator(".users-data-table")).toBeVisible();
      await expect(page).toHaveURL(/activity=active/);
    });

    test("should filter by all users", async ({ page }) => {
      await page.goto("/community?activity=all");

      await expect(page.locator(".users-data-table")).toBeVisible();
      // "all" is the default and is dropped from the address.
      await expect(page).not.toHaveURL(/activity=/);
    });

    test("should combine search with activity filter", async ({ page }) => {
      await page.goto("/community?search=тест&activity=active");

      await expect(page.getByPlaceholder("Поиск")).toHaveValue("тест");
      await expect(page).toHaveURL(/activity=active/);
    });
  });

  test.describe("URL State Persistence", () => {
    test("should restore filters from URL", async ({ page }) => {
      await page.goto("/community?search=Alice&sortBy=rating&activity=active");

      await expect(page.getByPlaceholder("Поиск")).toHaveValue("Alice");
      await expect(page).toHaveURL(/sortBy=rating/);
      await expect(page).toHaveURL(/activity=active/);
      await expect(page.locator(".error-message")).not.toBeVisible();
    });

    test("should preserve the query when paginating", async ({ page }) => {
      // A sort, not a search: the seed writes more accounts than fit on one
      // page, so the paging strip is there whatever the search matches. The
      // old version paginated on "q", which the filter does not read, and
      // clicked behind an `if` — nothing was asserted either way.
      await page.goto("/community?sortBy=username");

      const paging = page.locator(".paging").first();
      await expect(paging).toBeVisible();
      await paging.getByRole("link", { name: "2", exact: true }).click();

      await expect(page).toHaveURL(/sortBy=username/);
      await expect(page).toHaveURL(/number=2/);
    });
  });

  test.describe("Empty States", () => {
    test("should show empty message when no users match search", async ({
      page,
    }) => {
      // The filter binds "search"; "q" is not read at all, so the old URL asked
      // for the unfiltered list and no empty state could ever appear.
      await page.goto("/community?search=xyznonexistentuser123456789");

      await expect(page.locator(".table-empty")).toHaveText(
        "Пользователей по заданным фильтрам не найдено",
      );
    });
  });
});
