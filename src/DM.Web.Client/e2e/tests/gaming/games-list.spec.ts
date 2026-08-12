import { test, expect, type APIRequestContext } from "@playwright/test";
import type { Locator, Page } from "@playwright/test";
import { API_BASE_URL, primaryUser } from "../../fixtures/auth";

/**
 * /games — table, filter control, sorting, paging.
 *
 * Two habits this file keeps, both of them reactions to how it read before:
 *
 * - Locators are scoped to a region instead of disambiguated with .first().
 *   The filter control repeats the table's own words ("Статус игры" and
 *   "Ведущие" each label both a filter and a column), so a page-wide
 *   getByText matched two elements and died of strict mode. Scoping says
 *   which of the two the test means; .first() only says "either".
 * - No assertion is guarded by an `if`. Half the tests here used to run their
 *   assertions inside `if (await x.isVisible())`, so they reported green on a
 *   page that rendered nothing at all.
 *
 * Written against a desktop viewport. Two columns ("Теги", "Рецензии") are hidden
 * below 768px by design (Column.hideOnMobile), which the column test reads
 * from the same media query the CSS uses.
 */

/** The filter control: search field, "Фильтры" dropdown, sort, bubbles. */
const filters = (page: Page): Locator => page.locator(".games-filter");

/** The games table. One table on the page, addressed by role. */
const table = (page: Page): Locator => page.getByRole("table");

/** The row of active-filter chips. Absent when no filter is applied. */
const bubbles = (page: Page): Locator => page.locator(".bubbles-row");

/** A data row. The loading skeleton uses .skeleton-row, so this counts none. */
const rows = (page: Page): Locator => table(page).locator("tr.table-row");

/**
 * Hold until the table has rendered its data rows.
 *
 * Anything that reads text out of the table in bulk — allTextContents,
 * evaluateAll — resolves against whatever the DOM holds at that instant and
 * does not retry. While the request is in flight DataTable renders skeleton
 * rows under a different class, so those reads come back empty, and the caller
 * then fails on its own emptiness check rather than on a locator timeout: the
 * message says no value occurs exactly once, which reads like a data problem
 * and is really a timing one. Only for reads that expect rows; a test asserting
 * an empty result uses toHaveCount(0), which retries on its own.
 */
async function rowsRendered(page: Page): Promise<void> {
  await expect(rows(page).first()).toBeVisible();
}

/** The sort trigger. Its label is the active sort, so it has no fixed name. */
const sortTrigger = (page: Page): Locator =>
  filters(page).locator(".sort-section").getByRole("button");

const searchField = (page: Page): Locator =>
  filters(page).getByRole("textbox", { name: "Поиск по названию" });

async function openFilterDropdown(page: Page): Promise<Locator> {
  const region = filters(page);
  await region.getByRole("button", { name: "Фильтры" }).click();
  return region;
}

async function openSortMenu(page: Page): Promise<Locator> {
  await sortTrigger(page).click();
  return filters(page).locator(".sort-section").getByRole("menu");
}

/**
 * The one value that occurs exactly once, so a text locator built from it
 * resolves to a single element. Rows and tag links are otherwise
 * indistinguishable, and a positional locator would quietly follow whatever
 * the current sort order puts first.
 */
function onlyOccurrence(values: string[], what: string): string {
  const seen = values.filter((value) => value.length > 0);
  const unique = seen.find(
    (value) => seen.filter((other) => other === value).length === 1,
  );
  if (!unique) {
    throw new Error(
      `No ${what} occurs exactly once on the page, so no test can address one.`,
    );
  }
  return unique;
}

/** A table row addressed by the title of the game in it. */
function rowOfGame(page: Page, title: string): Locator {
  return table(page)
    .getByRole("row")
    .filter({ has: page.getByRole("link", { name: title, exact: true }) });
}

async function anyGameTitle(page: Page): Promise<string> {
  await rowsRendered(page);
  const titles = await table(page).locator("a.game-link").allTextContents();
  return onlyOccurrence(
    titles.map((title) => title.trim()),
    "game title",
  );
}

interface CatalogTag {
  id: number;
  title: string;
}

/**
 * A tag whose title no other tag title contains. Typing it into the tag
 * search leaves exactly one item in the list, which is what makes the item
 * locator unambiguous. Read from the catalog rather than hardcoded so the
 * test states "some tag" and not "tag #1".
 */
async function unambiguousTag(request: APIRequestContext): Promise<CatalogTag> {
  const response = await request.get(`${API_BASE_URL}/v1/games/tags`);
  await expect(response, "the tag catalog must load").toBeOK();
  const body = (await response.json()) as { resources?: CatalogTag[] };
  const tags = body.resources ?? [];
  const tag = tags.find(
    (candidate) =>
      tags.filter((other) =>
        other.title.toLowerCase().includes(candidate.title.toLowerCase()),
      ).length === 1,
  );
  if (!tag) {
    throw new Error("Every tag title is a substring of another one.");
  }
  return tag;
}

/** The requests this page's table and filter make. Everything else is shell. */
function isGamesListRequest(url: URL): boolean {
  if (url.pathname === "/v1/games") return !url.searchParams.has("projection");
  return url.pathname === "/v1/games/tags" || url.pathname === "/v1/users";
}

/**
 * Keep the run inside the API's request budget.
 *
 * One load of /games costs seventeen API requests, of which three belong to
 * the table, its tag catalog and its host lookup; the rest is the site shell —
 * sidebar lists, stats, chats, notifications, SignalR negotiate. The
 * API allows 100 requests per address per minute (GlobalPermitLimit), so a
 * 38-test file burns the whole budget in its first three tests and everything
 * after that measures 429s. That failure is indistinguishable from the one
 * this file was accused of: the games request fails, the table is not rendered
 * at all (DataTable is hidden when a request failed and no stale rows exist),
 * and every selector "goes missing".
 *
 * So the shell's requests never leave the browser. Nothing under test is
 * stubbed — the three requests above reach the real API untouched — and the
 * widgets that lose their data show their own error state, which no assertion
 * here reads.
 *
 * The tier-wide fix is not in this file: the API already takes
 * RateLimiting:Enabled=false, and an e2e stack should be started with it.
 */
test.beforeEach(async ({ page }) => {
  await page.route(
    (url) => url.origin === API_BASE_URL,
    async (route) => {
      if (isGamesListRequest(new URL(route.request().url()))) {
        await route.continue();
        return;
      }
      await route.abort();
    },
  );
});

test.describe("Games List Page", () => {
  test.describe("Page Structure", () => {
    test("should display games table with required columns", async ({
      page,
    }) => {
      await page.goto("/games");

      await expect(page.locator(".games-data-table")).toBeVisible();

      // The exact set, in order. There is no "Участники" column: the columns
      // are an owner decision and this spec predates it, so it demanded one.
      // An exact list states the decision instead of merely omitting it.
      const compact = await page.evaluate(
        () => window.matchMedia("(max-width: 768px)").matches,
      );
      await expect(table(page).getByRole("columnheader")).toHaveText(
        compact
          ? ["#", "Название", "Ведущие", "Статус игры", "Читатели"]
          : [
              "#",
              "Название",
              "Ведущие",
              "Теги",
              "Статус игры",
              "Рецензии",
              "Читатели",
            ],
      );
    });

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

    test("shows pagination under the table", async ({ page }) => {
      await page.goto("/games");

      // The seed holds more games than fit on one page, so the pager is there.
      // The previous version asserted count >= 0, which is true of every
      // possible outcome including a page that failed to render.
      const pager = page.getByRole("navigation", { name: "Пагинация" });
      await expect(pager).toBeVisible();
      // The accessible name is the aria-label Paging.vue puts on the link,
      // "Страница N", not the digit it prints. A link named by its text alone
      // is one a screen reader announces as a bare number.
      await expect(
        pager.getByRole("link", { name: "Страница 2", exact: true }),
      ).toBeVisible();
    });
  });

  test.describe("Search", () => {
    test("should filter games by text search", async ({ page }) => {
      await page.goto("/games");

      // Applied on a 300ms debounce (and on blur). There is no Enter handler:
      // the old test pressed Enter and passed on the debounce behind its back.
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

  test.describe("Sorting", () => {
    test("should open sort dropdown", async ({ page }) => {
      await page.goto("/games");

      const menu = await openSortMenu(page);
      await expect(menu).toBeVisible();

      // Scoped to the menu: "Дата создания" is also the trigger's own label
      // and "Название" is a column header, so the page-wide lookups this test
      // used to make resolved to two elements each.
      await expect(
        menu.getByRole("menuitem", { name: "Дата создания" }),
      ).toBeVisible();
      await expect(
        menu.getByRole("menuitem", { name: "Название" }),
      ).toBeVisible();
      await expect(
        menu.getByRole("menuitem", { name: "Популярность" }),
      ).toBeVisible();
    });

    test("should sort by popularity", async ({ page }) => {
      await page.goto("/games");

      const menu = await openSortMenu(page);
      await menu.getByRole("menuitem", { name: "Популярность" }).click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("sortBy") === "popularity",
      );
      // The URL carries non-default state only. Popularity's default direction
      // is desc, so sortOrder stays out of it; the old test demanded
      // sortOrder=desc and failed on a canonical URL.
      await expect(page).toHaveURL((url) => !url.searchParams.has("sortOrder"));
      await expect(sortTrigger(page)).toHaveText("Популярность");
    });

    test("should show popularity sort hint", async ({ page }) => {
      await page.goto("/games");

      const menu = await openSortMenu(page);
      const popularity = menu.getByRole("menuitem", { name: "Популярность" });

      // The hint names players and readers, not "активных читателей" as the
      // old expectation had it: the sort counts active users among both.
      await expect(popularity.locator(".sort-option-hint")).toHaveText(
        "По количеству активных пользователей среди текущих игроков и читателей",
      );
    });

    test("should sort by title ascending", async ({ page }) => {
      await page.goto("/games");

      const menu = await openSortMenu(page);
      await menu.getByRole("menuitem", { name: "Название" }).click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("sortBy") === "title",
      );

      // SORT_OPTIONS gives "title" defaultDirection: "asc", so picking
      // "Название" must sort from "А" to "Я". This once read desc: SortButton
      // reported the field and the direction as two events, and the filter
      // handled the second one while its own state still held the pre-click
      // direction, so it flipped the asc it had just been given. One event now
      // carries both.
      await expect(page).toHaveURL(
        (url) => url.searchParams.get("sortOrder") === "asc",
      );
    });

    test("should toggle sort order", async ({ page }) => {
      await page.goto("/games?sortBy=title&sortOrder=desc");

      const menu = await openSortMenu(page);
      // The direction row is labelled with the order in effect.
      await menu.getByRole("menuitem", { name: "По убыванию" }).click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("sortOrder") === "asc",
      );
    });

    test("should sort by column header click", async ({ page }) => {
      await page.goto("/games?sortBy=title&sortOrder=asc");

      const header = table(page).getByRole("columnheader", {
        name: "Название",
        exact: true,
      });

      // Headers are presentational labels; sorting is driven solely by the
      // sort control (product decision, recorded in DataTable.vue). What a
      // header owes the user is the announcement of the active sort, and a
      // click on it must change nothing — the old expectation of
      // click-to-sort was written against a table that never had it.
      await expect(header).toHaveAttribute("aria-sort", "ascending");
      await header.click();
      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("sortBy") === "title" &&
          url.searchParams.get("sortOrder") === "asc",
      );
      await expect(header.getByRole("button")).toHaveCount(0);
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

      // The placeholder has no trailing ellipsis; the old expectation did.
      await expect(region.getByPlaceholder("Поиск ведущего")).toBeVisible();
    });

    test("should add owner filter by search", async ({ page }) => {
      await page.goto("/games");

      const region = await openFilterDropdown(page);
      await region.getByRole("button", { name: /^Ведущие / }).click();
      // Suggestions are a search result, not a preloaded list: with an empty
      // query the API is never called, which is why waiting for an item
      // without typing timed out.
      await region
        .getByPlaceholder("Поиск ведущего")
        .fill(primaryUser.username);
      await region
        .getByRole("button", { name: primaryUser.username, exact: true })
        .click();

      // The URL parameter is "hosts" (the API field is hostUsernames). The old
      // "ownerUsernames" predates owner→host.
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

      // Each field is a DateInput: the ".date-input" class the old test filled
      // sits on the component's wrapper div, not on the text field inside it.
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

      // Addressed by class because the control has no accessible name: an
      // icon-only button with an aria-hidden icon and no aria-label. Reported
      // separately; a role locator becomes possible once it is labelled.
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

      // The old test watched ".dropdown", which matches nothing in this app —
      // so its closing assertion held for a dropdown that stayed open.
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

  test.describe("Pagination", () => {
    test("should navigate to next page", async ({ page }) => {
      await page.goto("/games");

      await page
        .getByRole("navigation", { name: "Пагинация" })
        .getByRole("link", { name: "Страница 2", exact: true })
        .click();

      await expect(page).toHaveURL(
        (url) => url.searchParams.get("number") === "2",
      );
    });

    test("should preserve filters when paginating", async ({ page }) => {
      // Active games span more than one page in the seed.
      await page.goto("/games?status=Active");

      await page
        .getByRole("navigation", { name: "Пагинация" })
        .getByRole("link", { name: "Страница 2", exact: true })
        .click();

      await expect(page).toHaveURL(
        (url) =>
          url.searchParams.get("status") === "Active" &&
          url.searchParams.get("number") === "2",
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
      // The old version of this test asserted nothing at all: it waited 500ms
      // and left a comment about what might have happened.
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

  test.describe("Empty States", () => {
    test("should show empty message when no games match filters", async ({
      page,
    }) => {
      await page.goto("/games?search=xyznonexistentgame123456789");

      await expect(rows(page)).toHaveCount(0);
      await expect(table(page)).toContainText(
        "Игр по заданным фильтрам не найдено",
      );
    });
  });

  // Was "Participants Column". There is no participants column — the columns
  // are an owner decision — so these cover the two cells that do carry the
  // numbers the old tests were reaching for: the slots indicator next to the
  // status badge, and the readers count.
  test.describe("Status and Readers Cells", () => {
    test("shows taken and total player slots next to the status", async ({
      page,
    }) => {
      await page.goto("/games");

      const title = await anyGameTitle(page);

      // "[2/4]" with a limit, "[3/∞]" without one.
      await expect(
        rowOfGame(page, title).locator(".slots-indicator"),
      ).toHaveText(/^\[\d+\/(\d+|∞)\]$/);
    });

    test("shows readers tooltip on hover", async ({ page }) => {
      await page.goto("/games");

      // A row whose readers count is not zero, so the tooltip lists readers
      // instead of saying there are none. The tooltip is the site's own
      // component, not a title attribute as the old test assumed.
      await rowsRendered(page);
      const titles = await rows(page).evaluateAll((rendered) =>
        rendered
          .filter(
            (row) =>
              (
                row.querySelector(".readers-count")?.textContent ?? "0"
              ).trim() !== "0",
          )
          .map((row) =>
            (row.querySelector("a.game-link")?.textContent ?? "").trim(),
          ),
      );
      const title = onlyOccurrence(titles, "game with readers");

      await rowOfGame(page, title).locator(".readers-count").hover();

      const tooltip = page.getByRole("tooltip");
      await expect(tooltip).toBeVisible();
      await expect(tooltip).toContainText("Читатели:");
    });
  });

  test.describe("Loading States", () => {
    test("should show loading state during data fetch", async ({ page }) => {
      // Only the table's own search is delayed. A "**/v1/games**" pattern
      // would also take over the sidebar's game lists, which the run above
      // deliberately keeps off the API.
      await page.route(
        (url) =>
          url.origin === API_BASE_URL &&
          url.pathname === "/v1/games" &&
          !url.searchParams.has("projection"),
        async (route) => {
          // Four seconds, not one and a half. This is the one test here that has
          // to catch a state on its way past instead of waiting for a settled
          // one, so the window has to outlast a slow boot: on a loaded machine
          // the first assertion has been seen starting after a 1500ms window had
          // already closed, and it then polls for its whole timeout against an
          // attribute that is never coming back. Four seconds still fits inside
          // the 5s expect timeout.
          await new Promise((resolve) => setTimeout(resolve, 4000));
          await route.continue();
        },
      );

      await page.goto("/games");

      // aria-busy is the loading contract: the table is there from the start,
      // marked busy while the request is in flight and unmarked once rows
      // arrive. The old test only checked that the wrapper existed.
      await expect(table(page)).toHaveAttribute("aria-busy", "true");
      await expect(rows(page)).not.toHaveCount(0);
      await expect(table(page)).not.toHaveAttribute("aria-busy", "true");
    });
  });
});
