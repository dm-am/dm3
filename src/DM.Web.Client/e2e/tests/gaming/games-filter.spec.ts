import { test, expect } from "@playwright/test";
import { primaryUser } from "../../fixtures/auth";
import {
  anyGameTitle,
  bubbles,
  filters,
  onlyOccurrence,
  openFilterDropdown,
  rowsRendered,
  searchField,
  sortTrigger,
  table,
  unambiguousTag,
} from "../../fixtures/gamesPage";

/**
 * /games, its filter control: the search, the status, tags, hosts and dates
 * of the "Фильтры" menu, the menu's own navigation, the bubbles, and the
 * state the address keeps. The locators of the page, and the habits these
 * specs keep, are in fixtures/gamesPage.ts.
 */

test.describe("Games List Page", () => {
  test.describe("Page Structure", () => {
    test("should display filter bar", async ({ page }) => {
      await page.goto("/games");

      await expect(searchField(page)).toBeVisible();
      await expect(
        filters(page).getByRole("button", { name: "Фильтры" }),
      ).toBeVisible();
      // The sort trigger carries the active sort as its label, and the default
      // sort is "created desc".
      await expect(sortTrigger(page)).toHaveText("Дата создания");
    });
  });

  test.describe("Search", () => {
    test("should filter games by text search", async ({ page }) => {
      await page.goto("/games");

      // Applied on a 300ms debounce (and on blur); there is no Enter handler.
      await searchField(page).fill("тест");

      // Read the decoded parameter — the browser reports the Cyrillic value
      // percent-encoded, so a raw-URL match on "search=тест" never fires.
      await expect(page).toHaveURL(
        (url) => url.searchParams.get("search") === "тест",
      );
    });

    test("should clear search input", async ({ page }) => {
      await page.goto("/games?search=тест");

      const input = searchField(page);
      await expect(input).toHaveValue("тест");

      await filters(page)
        .getByRole("button", { name: "Очистить поиск" })
        .click();

      await expect(input).toHaveValue("");
      await expect(page).toHaveURL((url) => !url.searchParams.has("search"));
    });

    test("should preserve search when navigating back", async ({ page }) => {
      await page.goto("/games");

      // Search for a title that is on the page, so the game to open is one the
      // test named rather than whichever row happens to be first.
      const title = await anyGameTitle(page);
      await searchField(page).fill(title);
      await expect(page).toHaveURL(
        (url) => url.searchParams.get("search") === title,
      );

      await table(page).getByRole("link", { name: title, exact: true }).click();
      await expect(page).toHaveURL(/\/game\/[^/?]+/);

      await page.goBack();
      await expect(searchField(page)).toHaveValue(title);
    });
  });

  test.describe("Filters - Status", () => {
    test("should open filter dropdown", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);

      await expect(
        region.getByRole("button", { name: "Фильтры" }),
      ).toHaveAttribute("aria-expanded", "true");
      // Each item is a button whose accessible name is its label plus its
      // hint, so the names are anchored. Plain "Тег" would also match
      // "Без тега".
      await expect(
        region.getByRole("button", { name: /^Статус игры / }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Ведущие / }),
      ).toBeVisible();
      await expect(region.getByRole("button", { name: /^Тег / })).toBeVisible();
    });

    test("should filter by Draft status", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Статус игры / }).click();
      await region.getByRole("button", { name: /^Оформляется / }).click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("status") === "Draft",
      );
      await expect(bubbles(page)).toContainText("Статус игры: Оформляется");
    });

    test("should filter by Active status with recruitment", async ({
      page,
    }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Статус игры / }).click();
      await region.getByRole("button", { name: /^Идет игра / }).click();
      await region.getByRole("button", { name: /^Набор игроков / }).click();
      await region.getByRole("button", { name: /^Первый набор / }).click();

      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("status") === "Active" &&
          url.searchParams.get("recruitmentFilter") === "initial",
      );
      await expect(bubbles(page)).toContainText(
        "Статус игры: Идет игра (первый набор)",
      );
    });

    test("should filter by Closed status with reason", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Статус игры / }).click();
      await region.getByRole("button", { name: /^Закрыта / }).click();
      await region.getByRole("button", { name: /^Заморожена / }).click();

      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("status") === "Closed" &&
          url.searchParams.get("closedReasonFilter") === "Frozen",
      );
      await expect(bubbles(page)).toContainText(
        "Статус игры: Закрыта (заморожена)",
      );
    });

    test("should remove status filter via bubble", async ({ page }) => {
      await page.goto("/games?status=Active");

      // The chip's remove control carries the filter value in its accessible
      // name, which is a sharper handle than the chip's own text.
      const remove = bubbles(page).getByRole("button", {
        name: "Убрать фильтр: Идет игра",
      });
      await expect(remove).toBeVisible();

      await remove.click();

      await expect(page).toHaveURL((url) => !url.searchParams.has("status"));
      await expect(bubbles(page)).toBeHidden();
    });
  });

  test.describe("Filters - Tags", () => {
    test("should navigate to tag groups", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Тег / }).click();

      // Level two of the tag branch: a search field over the whole catalog
      // plus the groups it is divided into.
      await expect(
        region.getByPlaceholder("Поиск тега", { exact: true }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Система / }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Жанр / }),
      ).toBeVisible();
    });

    test("should add required tag filter", async ({ page, request }) => {
      const tag = await unambiguousTag(request);
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Тег / }).click();
      // Searching gives a flat list, so the tag that gets clicked is the one
      // the test named — walking into a group would only offer "whichever
      // group came first".
      await region
        .getByPlaceholder("Поиск тега", { exact: true })
        .fill(tag.title);
      await region.getByRole("button", { name: tag.title }).click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("requiredTags") === String(tag.id),
      );
      await expect(bubbles(page)).toContainText(`Тег: ${tag.title}`);
    });

    test("should add excluded tag filter", async ({ page, request }) => {
      const tag = await unambiguousTag(request);
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Без тега / }).click();
      await region
        .getByPlaceholder("Поиск тега", { exact: true })
        .fill(tag.title);
      await region.getByRole("button", { name: tag.title }).click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("excludedTags") === String(tag.id),
      );
      await expect(bubbles(page)).toContainText(`Без тега: ${tag.title}`);
    });

    test("should click tag in game row to filter by it", async ({ page }) => {
      // "Теги" stands at every width; where the table is wider than the
      // column, the click scrolls its frame to the tag first.
      await page.goto("/games");

      await rowsRendered(page);
      const tagTitles = await table(page)
        .locator("a.tag-link")
        .allTextContents();
      const title = onlyOccurrence(
        tagTitles.map((tagTitle) => tagTitle.trim()),
        "tag title",
      );

      await table(page).getByRole("link", { name: title, exact: true }).click();

      await expect(page).toHaveURL((url) =>
        /^\d+$/.test(url.searchParams.get("requiredTags") ?? ""),
      );
      await expect(bubbles(page)).toContainText(`Тег: ${title}`);
    });
  });

  test.describe("Filters - Owners", () => {
    test("should open owner search", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Ведущие / }).click();

      await expect(region.getByPlaceholder("Поиск ведущего")).toBeVisible();
    });

    test("should add owner filter by search", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Ведущие / }).click();
      // Suggestions are a search result: with an empty query the API is never
      // called.
      await region
        .getByPlaceholder("Поиск ведущего")
        .fill(primaryUser.username);
      await region
        .getByRole("button", { name: primaryUser.username, exact: true })
        .click();

      // The URL parameter is "hosts" (the API field is hostUsernames).
      await expect(page).toHaveURL(
        (url) => url.searchParams.get("hosts") === primaryUser.username,
      );
      // Singular prefix for a single host; "Ведущие:" appears from the second.
      await expect(bubbles(page)).toContainText(
        `Ведущий: ${primaryUser.username}`,
      );
    });
  });

  test.describe("Filters - Date Ranges", () => {
    test("should open date filter options", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Даты / }).click();

      await expect(
        region.getByRole("button", { name: /^Создание игры / }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Начало игры / }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Начало последнего набора / }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Закрытие игры / }),
      ).toBeVisible();
    });

    test("should show date range inputs", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Даты / }).click();
      await region.getByRole("button", { name: /^Создание игры / }).click();

      // Each field is a DateInput: the text field sits inside its wrapper.
      await expect(
        region.getByRole("textbox", { name: "Дата: От" }),
      ).toBeVisible();
      await expect(
        region.getByRole("textbox", { name: "Дата: До" }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: "Применить" }),
      ).toBeVisible();
    });

    test("should apply date range filter", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Даты / }).click();
      await region.getByRole("button", { name: /^Создание игры / }).click();

      await region
        .getByRole("textbox", { name: "Дата: От" })
        .fill("2024-01-01");
      await region
        .getByRole("textbox", { name: "Дата: До" })
        .fill("2024-12-31");
      await region.getByRole("button", { name: "Применить" }).click();

      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("createdFromUtc") === "2024-01-01" &&
          url.searchParams.get("createdToUtc") === "2024-12-31",
      );
      // The chip is prefixed "Создание:" and shows the dates as typed by a
      // reader, not in the ISO form the URL carries.
      await expect(bubbles(page)).toContainText(
        "Создание: 01.01.2024 — 31.12.2024",
      );
    });
  });

  test.describe("Filter Interactions", () => {
    test("should clear all filters", async ({ page }) => {
      await page.goto("/games?status=Active&sortBy=title");

      await expect(bubbles(page)).toContainText("Статус игры: Идет игра");

      await bubbles(page).getByRole("button", { name: "Сбросить" }).click();

      // "Сбросить" resets sorting along with the filters, so the canonical URL
      // it lands on carries no parameters at all.
      await expect(bubbles(page)).toBeHidden();
      await expect(page).toHaveURL(
        (url) => [...url.searchParams.keys()].length === 0,
      );
    });

    test("should navigate back in filter dropdown", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Статус игры / }).click();

      const header = region.locator(".dropdown-nav-header");
      await expect(header).toBeVisible();
      await expect(header).toContainText("Статус игры");

      // Addressed by class: the icon-only button has no accessible name.
      await header.locator(".nav-back-btn").click();

      await expect(
        region.getByRole("button", { name: /^Статус игры / }),
      ).toBeVisible();
      await expect(
        region.getByRole("button", { name: /^Ведущие / }),
      ).toBeVisible();
    });

    test("should close dropdown on click outside", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      const dropdown = region.locator(".filter-dropdown-container");
      await expect(dropdown).toBeVisible();

      await page.getByRole("heading", { level: 1 }).click();

      await expect(dropdown).toBeHidden();
      await expect(
        region.getByRole("button", { name: "Фильтры" }),
      ).toHaveAttribute("aria-expanded", "false");
    });

    test("should combine multiple filters", async ({ page }) => {
      await page.goto("/games?status=Active");

      await searchField(page).fill("тест");

      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("status") === "Active" &&
          url.searchParams.get("search") === "тест",
      );
    });
  });

  test.describe("URL State Persistence", () => {
    test("should restore filters from URL", async ({ page }) => {
      await page.goto(
        "/games?status=Active&search=тест&sortBy=title&sortOrder=asc",
      );

      await expect(searchField(page)).toHaveValue("тест");
      await expect(bubbles(page)).toContainText("Статус игры: Идет игра");
      await expect(sortTrigger(page)).toHaveText("Название");
    });

    test("should restore tag filters from URL", async ({ page, request }) => {
      const tag = await unambiguousTag(request);

      await page.goto(`/games?requiredTags=${tag.id}`);

      await expect(bubbles(page)).toContainText(`Тег: ${tag.title}`);
      // A tag id the catalog does not know is dropped from the URL on load
      // (validateTagFilters); a known one must survive.
      await expect(page).toHaveURL(
        (url) => url.searchParams.get("requiredTags") === String(tag.id),
      );
    });
  });
});
