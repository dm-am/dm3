import { test, expect } from "@playwright/test";
import { openSortMenu, sortTrigger, table } from "../../fixtures/gamesPage";

/**
 * /games, its sort: the menu of the sort control, the sorts it offers and the
 * direction each one takes, and the column headers, which announce the sort
 * and do not change it. The locators of the page, and the habits these specs
 * keep, are in fixtures/gamesPage.ts.
 */

test.describe("Games List Page", () => {
  test.describe("Sorting", () => {
    test("should open sort dropdown", async ({ page }) => {
      await page.goto("/games");

      const menu = await openSortMenu(page);
      await expect(menu).toBeVisible();

      // Scoped to the menu: "Дата создания" is also the trigger's own label
      // and "Название" is a column header.
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
      // The URL carries non-default state only: popularity's default direction
      // is desc, so sortOrder stays out of it.
      await expect(page).toHaveURL((url) => !url.searchParams.has("sortOrder"));
      await expect(sortTrigger(page)).toHaveText("Популярность");
    });

    test("should show popularity sort hint", async ({ page }) => {
      await page.goto("/games");

      const menu = await openSortMenu(page);
      const popularity = menu.getByRole("menuitem", { name: "Популярность" });

      // The hint names players and readers: the sort counts active users
      // among both.
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
      // "Название" sorts from "А" to "Я".
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
      // sort control (DataTable.vue), and a click on a header changes nothing.
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
});
