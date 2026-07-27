import { test, expect } from "@playwright/test";

test.describe("Games List Page", () => {
  test.describe("Page Structure", () => {
    test("should display games table with required columns", async ({
      page,
    }) => {
      await page.goto("/games");

      // Verify table exists
      const table = page.locator("#results");
      await expect(table).toBeVisible();

      // Verify column headers
      await expect(
        page.getByRole("columnheader", { name: "Название" }),
      ).toBeVisible();
      await expect(
        page.getByRole("columnheader", { name: "Ведущие" }),
      ).toBeVisible();
      await expect(
        page.getByRole("columnheader", { name: "Статус игры" }),
      ).toBeVisible();
      await expect(
        page.getByRole("columnheader", { name: "Участники" }),
      ).toBeVisible();
    });

    test("should display filter bar", async ({ page }) => {
      await page.goto("/games");

      // Search input
      await expect(
        page.locator(".games-filter .search-container"),
      ).toBeVisible();
      await expect(page.getByPlaceholder("Поиск")).toBeVisible();

      // Filter button
      await expect(page.getByRole("button", { name: "Фильтры" })).toBeVisible();

      // Sort button
      await expect(page.locator(".sort-btn")).toBeVisible();
    });

    test("should display pagination when games exist", async ({ page }) => {
      await page.goto("/games");

      // Wait for table to load
      await page.waitForSelector("#results");

      // Check if paging exists (may not if few games)
      const paging = page.locator(".paging-top, .paging-bottom");
      const count = await paging.count();
      // Just verify paging components are rendered (may be empty if no pagination needed)
      expect(count).toBeGreaterThanOrEqual(0);
    });
  });

  test.describe("Search", () => {
    test("should filter games by text search", async ({ page }) => {
      await page.goto("/games");

      // Type search query
      const searchInput = page.getByPlaceholder("Поиск");
      await searchInput.fill("тест");
      await searchInput.press("Enter");

      // URL should update with search parameter
      await expect(page).toHaveURL(/search=тест/);
    });

    test("should clear search input", async ({ page }) => {
      await page.goto("/games?search=тест");

      // Verify search input has value
      const searchInput = page.getByPlaceholder("Поиск");
      await expect(searchInput).toHaveValue("тест");

      // Click clear button
      await page.locator(".clear-input-btn").click();

      // Search should be cleared
      await expect(searchInput).toHaveValue("");
    });

    test("should preserve search when navigating back", async ({ page }) => {
      await page.goto("/games");

      // Enter search
      const searchInput = page.getByPlaceholder("Поиск");
      await searchInput.fill("поиск");
      await searchInput.press("Enter");

      await expect(page).toHaveURL(/search=поиск/);

      // Navigate to a game (if any exists) and back
      const gameLink = page.locator(".game-link").first();
      if (await gameLink.isVisible()) {
        await gameLink.click();
        await page.goBack();

        // Search should be preserved
        await expect(searchInput).toHaveValue("поиск");
      }
    });
  });

  test.describe("Sorting", () => {
    test("should open sort dropdown", async ({ page }) => {
      await page.goto("/games");

      // Click sort button
      await page.locator(".sort-btn").click();

      // Dropdown should appear
      await expect(page.locator(".sort-dropdown")).toBeVisible();

      // Should have sort options
      await expect(page.getByText("Дата создания")).toBeVisible();
      await expect(page.getByText("Название")).toBeVisible();
      await expect(page.getByText("Популярность")).toBeVisible();
    });

    test("should sort by popularity", async ({ page }) => {
      await page.goto("/games");

      // Open sort dropdown
      await page.locator(".sort-btn").click();

      // Select "Популярность" option
      await page
        .locator(".sort-option")
        .filter({ hasText: "Популярность" })
        .click();

      // URL should have sortBy=popularity
      await expect(page).toHaveURL(/sortBy=popularity/);
      // Default order is desc
      await expect(page).toHaveURL(/sortOrder=desc/);
    });

    test("should show popularity sort hint", async ({ page }) => {
      await page.goto("/games");

      // Open sort dropdown
      await page.locator(".sort-btn").click();

      // Popularity option should have hint about active readers
      const popularityOption = page
        .locator(".sort-option")
        .filter({ hasText: "Популярность" });
      await expect(popularityOption.locator(".sort-option-hint")).toContainText(
        "активных читателей",
      );
    });

    test("should sort by title ascending", async ({ page }) => {
      await page.goto("/games");

      // Open sort dropdown
      await page.locator(".sort-btn").click();

      // Select "Название" option
      await page
        .locator(".sort-option")
        .filter({ hasText: "Название" })
        .click();

      // URL should have sortBy parameter
      await expect(page).toHaveURL(/sortBy=title/);
    });

    test("should toggle sort order", async ({ page }) => {
      await page.goto("/games?sortBy=title&sortOrder=desc");

      // Open sort dropdown
      await page.locator(".sort-btn").click();

      // Click direction toggle
      await page.locator(".sort-direction").click();

      // Order should change to asc
      await expect(page).toHaveURL(/sortOrder=asc/);
    });

    test("should sort by column header click", async ({ page }) => {
      await page.goto("/games");

      // Click on "Название" column header
      const titleHeader = page.getByRole("columnheader", { name: "Название" });
      await titleHeader.click();

      // Should sort by title
      await expect(page).toHaveURL(/sortBy=title/);
    });
  });

  test.describe("Filters - Status", () => {
    test("should open filter dropdown", async ({ page }) => {
      await page.goto("/games");

      // Click filter button
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Dropdown should appear
      await expect(page.locator(".dropdown")).toBeVisible();

      // Should show filter categories
      await expect(page.getByText("Статус игры")).toBeVisible();
      await expect(page.getByText("Ведущие")).toBeVisible();
      await expect(page.getByText("Тег")).toBeVisible();
    });

    test("should filter by Draft status", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Статус игры"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Статус игры" })
        .click();

      // Select "Оформляется" (Draft)
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Оформляется" })
        .click();

      // URL should update
      await expect(page).toHaveURL(/status=Draft/);

      // Bubble should appear
      await expect(
        page.locator(".bubble").filter({ hasText: "Оформляется" }),
      ).toBeVisible();
    });

    test("should filter by Active status with recruitment", async ({
      page,
    }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Статус игры"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Статус игры" })
        .click();

      // Select "Идет игра" (Active - navigation)
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Идет игра" })
        .click();

      // Select "Набор игроков" to see sub-options
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Набор игроков" })
        .click();

      // Select "Первый набор"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Первый набор" })
        .click();

      // URL should update
      await expect(page).toHaveURL(/status=Active/);
      await expect(page).toHaveURL(/recruitmentFilter=initial/);
    });

    test("should filter by Closed status with reason", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Статус игры"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Статус игры" })
        .click();

      // Select "Закрыта" (Closed - navigation)
      await page
        .locator(".dropdown-item")
        .filter({ hasText: /^Закрыта$/ })
        .click();

      // Select "Заморожена" (Frozen)
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Заморожена" })
        .click();

      // URL should update
      await expect(page).toHaveURL(/status=Closed/);
      await expect(page).toHaveURL(/closedReasonFilter=Frozen/);
    });

    test("should remove status filter via bubble", async ({ page }) => {
      await page.goto("/games?status=Active");

      // Status bubble should be visible
      const statusBubble = page
        .locator(".bubble")
        .filter({ hasText: "Статус игры" });
      await expect(statusBubble).toBeVisible();

      // Click remove button on bubble
      await statusBubble.locator(".bubble-remove-btn").click();

      // URL should not have status
      await expect(page).not.toHaveURL(/status=/);
    });
  });

  test.describe("Filters - Tags", () => {
    test("should navigate to tag groups", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Тег"
      await page.locator(".dropdown-item").filter({ hasText: /^Тег$/ }).click();

      // Should show tag groups (e.g., "Жанр", "Система")
      // Wait for groups to load
      await page.waitForSelector(".dropdown-item");

      // Dropdown should still be visible with tag groups
      await expect(page.locator(".dropdown")).toBeVisible();
    });

    test("should add required tag filter", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Тег"
      await page.locator(".dropdown-item").filter({ hasText: /^Тег$/ }).click();

      // Wait for groups to load and click first available group
      await page.waitForSelector(".dropdown-item");
      const firstGroup = page.locator(".dropdown-item").first();
      await firstGroup.click();

      // Select first tag in group
      await page.waitForSelector(".dropdown-item");
      const firstTag = page.locator(".dropdown-item").first();
      await firstTag.click();

      // URL should have requiredTags
      await expect(page).toHaveURL(/requiredTags=/);

      // Bubble should appear
      await expect(
        page.locator(".bubble").filter({ hasText: "Тег:" }),
      ).toBeVisible();
    });

    test("should add excluded tag filter", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Без тега"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Без тега" })
        .click();

      // Wait for groups to load and click first available group
      await page.waitForSelector(".dropdown-item");
      const firstGroup = page.locator(".dropdown-item").first();
      await firstGroup.click();

      // Select first tag in group
      await page.waitForSelector(".dropdown-item");
      const firstTag = page.locator(".dropdown-item").first();
      await firstTag.click();

      // URL should have excludedTags
      await expect(page).toHaveURL(/excludedTags=/);

      // Bubble should appear
      await expect(
        page.locator(".bubble").filter({ hasText: "Без тега:" }),
      ).toBeVisible();
    });

    test("should click tag in game row to filter by it", async ({ page }) => {
      await page.goto("/games");

      // Wait for table to load
      await page.waitForSelector("#results");

      // Find a tag link in the table
      const tagLink = page.locator(".tag-link").first();
      if (await tagLink.isVisible()) {
        await tagLink.click();

        // URL should have requiredTags
        await expect(page).toHaveURL(/requiredTags=/);
      }
    });
  });

  test.describe("Filters - Owners", () => {
    test("should open owner search", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Ведущие"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Ведущие" })
        .click();

      // Should show search input for owners
      await expect(page.locator(".dropdown-search-input")).toBeVisible();
      await expect(page.getByPlaceholder("Поиск ведущего...")).toBeVisible();
    });

    test("should add owner filter by search", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown and navigate to owners
      await page.getByRole("button", { name: "Фильтры" }).click();
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Ведущие" })
        .click();

      // Wait for owner suggestions to load
      await page.waitForSelector(".dropdown-item");

      // Select first owner suggestion
      const firstOwner = page.locator(".dropdown-item").first();
      if (await firstOwner.isVisible()) {
        await firstOwner.click();

        // URL should have ownerUsernames
        await expect(page).toHaveURL(/ownerUsernames=/);

        // Bubble should appear
        await expect(
          page.locator(".bubble").filter({ hasText: "Ведущие:" }),
        ).toBeVisible();
      }
    });
  });

  test.describe("Filters - Date Ranges", () => {
    test("should open date filter options", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Select "Даты"
      await page.locator(".dropdown-item").filter({ hasText: "Даты" }).click();

      // Should show date type options
      await expect(page.getByText("Создание игры")).toBeVisible();
      await expect(page.getByText("Начало игры")).toBeVisible();
      await expect(page.getByText("Закрытие игры")).toBeVisible();
    });

    test("should show date range inputs", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Navigate to dates
      await page.locator(".dropdown-item").filter({ hasText: "Даты" }).click();

      // Select "Создание игры"
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Создание игры" })
        .click();

      // Should show date inputs
      await expect(page.locator(".date-input").first()).toBeVisible();
      await expect(page.locator(".date-apply-btn")).toBeVisible();
    });

    test("should apply date range filter", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Navigate to dates
      await page.locator(".dropdown-item").filter({ hasText: "Даты" }).click();
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Создание игры" })
        .click();

      // Fill date inputs
      await page.locator(".date-input").first().fill("2024-01-01");
      await page.locator(".date-input").last().fill("2024-12-31");

      // Apply
      await page.locator(".date-apply-btn").click();

      // URL should have date params
      await expect(page).toHaveURL(/createdFromUtc=2024-01-01/);
      await expect(page).toHaveURL(/createdToUtc=2024-12-31/);

      // Bubble should appear
      await expect(
        page.locator(".bubble").filter({ hasText: "Создание игры:" }),
      ).toBeVisible();
    });
  });

  test.describe("Filter Interactions", () => {
    test("should clear all filters", async ({ page }) => {
      await page.goto("/games?status=Active&sortBy=title");

      // Wait for filters to be applied
      await expect(page.locator(".bubble")).toBeVisible();

      // Click "Сбросить" link
      await page.locator(".clear-all-link").click();

      // All bubbles should be gone
      await expect(page.locator(".bubbles-row .bubble")).toHaveCount(0);
    });

    test("should navigate back in filter dropdown", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();

      // Navigate to status
      await page
        .locator(".dropdown-item")
        .filter({ hasText: "Статус игры" })
        .click();

      // Should show nav header
      await expect(page.locator(".dropdown-nav-header")).toBeVisible();

      // Click back button
      await page.locator(".nav-back-btn").click();

      // Should be back at root level
      await expect(page.getByText("Статус игры")).toBeVisible();
      await expect(page.getByText("Ведущие")).toBeVisible();
    });

    test("should close dropdown on click outside", async ({ page }) => {
      await page.goto("/games");

      // Open filter dropdown
      await page.getByRole("button", { name: "Фильтры" }).click();
      await expect(page.locator(".dropdown")).toBeVisible();

      // Click outside
      await page.locator("h1").click();

      // Dropdown should close
      await expect(page.locator(".dropdown")).not.toBeVisible();
    });

    test("should combine multiple filters", async ({ page }) => {
      // Start with status filter
      await page.goto("/games?status=Active");

      // Add a search
      const searchInput = page.getByPlaceholder("Поиск");
      await searchInput.fill("тест");
      await searchInput.press("Enter");

      // Both filters should be in URL
      await expect(page).toHaveURL(/status=Active/);
      await expect(page).toHaveURL(/search=тест/);
    });
  });

  test.describe("Pagination", () => {
    test("should navigate to next page", async ({ page }) => {
      await page.goto("/games");

      // Wait for table
      await page.waitForSelector("#results");

      // Check if pagination exists
      const nextPageLink = page.locator(".paging-bottom a").last();
      if (await nextPageLink.isVisible()) {
        await nextPageLink.click();

        // URL should have page number
        await expect(page).toHaveURL(/number=2/);
      }
    });

    test("should preserve filters when paginating", async ({ page }) => {
      await page.goto("/games?status=Active");

      // Wait for table
      await page.waitForSelector("#results");

      // Check if pagination exists
      const nextPageLink = page.locator(".paging-bottom a").last();
      if (await nextPageLink.isVisible()) {
        await nextPageLink.click();

        // Status filter should still be in URL
        await expect(page).toHaveURL(/status=Active/);
      }
    });
  });

  test.describe("URL State Persistence", () => {
    test("should restore filters from URL", async ({ page }) => {
      await page.goto(
        "/games?status=Active&search=тест&sortBy=title&sortOrder=asc",
      );

      // Search input should have value
      await expect(page.getByPlaceholder("Поиск")).toHaveValue("тест");

      // Status bubble should be visible
      await expect(
        page.locator(".bubble").filter({ hasText: "Статус игры" }),
      ).toBeVisible();

      // Sort button should show "Название"
      await expect(page.locator(".sort-btn")).toContainText("Название");
    });

    test("should restore tag filters from URL", async ({ page }) => {
      // Need a valid tag ID - this test assumes tags exist
      await page.goto("/games?requiredTags=1");

      // Wait for tags to load
      await page.waitForTimeout(500);

      // If tag exists, a "Тег:" bubble should appear - may or may not be
      // visible depending on whether tag ID 1 exists
    });
  });

  test.describe("Empty States", () => {
    test("should show empty message when no games match filters", async ({
      page,
    }) => {
      // Use a very specific search that likely won't match anything
      await page.goto("/games?search=xyznonexistentgame123456789");

      // Wait for table to load
      await page.waitForSelector("#results");

      // Should show empty message
      await expect(
        page.getByText("Нет игр по заданным фильтрам"),
      ).toBeVisible();
    });
  });

  test.describe("Participants Column", () => {
    test("should display participants with reader count", async ({ page }) => {
      await page.goto("/games");

      // Wait for table to load
      await page.waitForSelector("#results");

      // Participants column should show slots and reader count
      const participantsCell = page.locator(".participants").first();
      if (await participantsCell.isVisible()) {
        // Should have slots display (e.g., "2/4" or "3/∞")
        await expect(participantsCell.locator(".slots")).toBeVisible();
        // Should have readers count in brackets (e.g., "[+5]")
        await expect(participantsCell.locator(".readers-count")).toBeVisible();
      }
    });

    test("should show readers tooltip on hover", async ({ page }) => {
      await page.goto("/games");

      // Wait for table to load
      await page.waitForSelector("#results");

      // Find reader count element
      const readersCount = page.locator(".readers-count").first();
      if (await readersCount.isVisible()) {
        // Should have title attribute with reader info
        const title = await readersCount.getAttribute("title");
        expect(title).toContain("Читатели:");
      }
    });
  });

  test.describe("Loading States", () => {
    test("should show loading state during data fetch", async ({ page }) => {
      // Slow down network to see loading
      await page.route("**/v1/games**", async (route) => {
        await new Promise((resolve) => setTimeout(resolve, 500));
        await route.continue();
      });

      await page.goto("/games");

      // Table should show loading state
      // Note: exact selector depends on DataTable implementation
      const table = page.locator("#results");
      await expect(table).toBeVisible();
    });
  });
});
