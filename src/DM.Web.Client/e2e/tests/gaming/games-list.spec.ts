import { test, expect, type Page } from "@playwright/test";
import { API_BASE_URL } from "../../fixtures/auth";
import {
  anyGameTitle,
  onlyOccurrence,
  rowOfGame,
  rows,
  rowsRendered,
  table,
} from "../../fixtures/gamesPage";
import { readTables } from "../../fixtures/tables";

/**
 * /games, its table: the columns, paging, the empty result, the slots and
 * readers cells, the loading state. The filter control is in
 * games-filter.spec.ts and the sort in games-sort.spec.ts; the locators of
 * the page, and the two habits all three keep, are in fixtures/gamesPage.ts.
 *
 * Written against the viewport of each project. At every width the table is
 * the full table: no column is left out, and its frame
 * scrolls sideways only where even the least width of the table, every word
 * whole, is wider than the column (fixtures/tables.ts).
 */

test.describe("Games List Page", () => {
  test.describe("Page Structure", () => {
    test("should display games table with required columns", async ({
      page,
      browser,
    }) => {
      await page.goto("/games");

      await expect(page.locator(".games-data-table")).toBeVisible();

      // The exact set, in order: an exact list states which columns there are.
      const all = [
        "#",
        "Название",
        "Ведущие",
        "Теги",
        "Статус игры",
        "Рецензии",
        "Читатели",
      ];
      await rowsRendered(page);
      const shown = async () =>
        (await table(page).getByRole("columnheader").allTextContents()).map(
          (one) => one.trim(),
        );
      // No column is left out, and the frame scrolls only a table wider than
      // the column at its least (fixtures/tables.ts).
      const tableProblems = async (on: Page) =>
        (
          await readTables(on, {
            within: ".games-data-table",
            words: true,
          })
        ).problems;
      expect(await shown()).toEqual(all);
      expect(await tableProblems(page)).toEqual([]);

      // A window of 1920 has the room for the whole table, so there the
      // frame does not scroll at all, in a window of its own.
      const wide = await browser.newContext({
        viewport: { width: 1920, height: 900 },
      });
      const desktop = await wide.newPage();
      await desktop.goto("/games");
      await rowsRendered(desktop);
      expect(await tableProblems(desktop)).toEqual([]);
      expect(
        await desktop
          .locator(".games-data-table .data-table-scroll")
          .evaluate((frame) => frame.scrollWidth - frame.clientWidth),
      ).toBeLessThanOrEqual(1);
      await wide.close();
    });

    test("shows pagination under the table", async ({ page }) => {
      await page.goto("/games");

      // The seed holds more games than fit on one page, so the pager is there.
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

  // The slots indicator next to the status badge, and the readers count.
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
      // instead of saying there are none.
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
      // Only the table's own search is delayed: the sidebar's game lists ask
      // with a projection.
      await page.route(
        (url) =>
          url.origin === API_BASE_URL &&
          url.pathname === "/v1/games" &&
          !url.searchParams.has("projection"),
        async (route) => {
          // Four seconds: this test catches a state on its way past, so the
          // window has to outlast a slow boot and still fit the 5s expect
          // timeout.
          await new Promise((resolve) => setTimeout(resolve, 4000));
          await route.continue();
        },
      );

      await page.goto("/games");

      // aria-busy is the loading contract: the table is there from the start,
      // marked busy while the request is in flight and unmarked once rows
      // arrive.
      await expect(table(page)).toHaveAttribute("aria-busy", "true");
      await expect(rows(page)).not.toHaveCount(0);
      await expect(table(page)).not.toHaveAttribute("aria-busy", "true");
    });
  });
});
