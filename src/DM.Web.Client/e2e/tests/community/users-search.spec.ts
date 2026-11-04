import { test, expect } from "@playwright/test";

/**
 * The community list, addressed the way the filter addresses itself:
 * useUsersFilter binds `search`, `activity`, `role`, `sortBy` and `sortOrder`.
 * A parameter is written only while it differs from the default (sortBy is
 * lastActivity, activity is all), and the direction comes with the field:
 * choosing "Имя" sets asc because that option declares it.
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

      // The widget renders .users-data-table.
      await expect(page.locator(".users-data-table")).toBeVisible();
      await expect(page.locator(".error-message")).not.toBeVisible();
    });
  });

  test.describe("Sorting", () => {
    test("should sort by username", async ({ page }) => {
      await page.goto("/community");

      // The control is the SortButton: a .sort-btn opening .sort-option items.
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

      // The page does not write a default into the address, and it does not
      // rewrite an address somebody arrived with either: the default is
      // understood, and the list is the unfiltered one.
      await expect(page).toHaveURL(/activity=all/);
      await expect(page.locator(".bubbles-row")).toHaveCount(0);
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
      // page, so the paging strip is there whatever the search matches.
      await page.goto("/community?sortBy=username");

      const paging = page.locator(".paging").first();
      await expect(paging).toBeVisible();
      // The accessible name is the aria-label Paging.vue puts on the link,
      // "Страница N", not the digit it prints.
      await paging
        .getByRole("link", { name: "Страница 2", exact: true })
        .click();

      await expect(page).toHaveURL(/sortBy=username/);
      await expect(page).toHaveURL(/number=2/);
    });
  });

  test.describe("Empty States", () => {
    test("should show empty message when no users match search", async ({
      page,
    }) => {
      await page.goto("/community?search=xyznonexistentuser123456789");

      await expect(page.locator(".table-empty")).toHaveText(
        "Пользователей по заданным фильтрам не найдено",
      );
    });
  });
});
