import {
  expect,
  type APIRequestContext,
  type Locator,
  type Page,
} from "@playwright/test";
import { API_BASE_URL } from "./auth";

/**
 * How the specs of /games address the page: its table (games-list.spec.ts),
 * its filter control (games-filter.spec.ts) and its sort (games-sort.spec.ts).
 *
 * Locators are scoped to a region instead of disambiguated with .first(): the
 * filter control repeats the table's own words ("Статус игры" and "Ведущие"
 * each label both a filter and a column).
 */

/** The filter control: search field, "Фильтры" dropdown, sort, bubbles. */
export const filters = (page: Page): Locator => page.locator(".games-filter");

/** The games table. One table on the page, addressed by role. */
export const table = (page: Page): Locator => page.getByRole("table");

/** The row of active-filter chips. Absent when no filter is applied. */
export const bubbles = (page: Page): Locator => page.locator(".bubbles-row");

/** A data row. The loading skeleton uses .skeleton-row, so this counts none. */
export const rows = (page: Page): Locator =>
  table(page).locator("tr.table-row");

/**
 * Hold until the table has rendered its data rows: bulk reads (allTextContents,
 * evaluateAll) do not retry, and while the request is in flight DataTable
 * renders skeleton rows under a different class.
 */
export async function rowsRendered(page: Page): Promise<void> {
  await expect(rows(page).first()).toBeVisible();
}

/** The sort trigger. Its label is the active sort, so it has no fixed name. */
export const sortTrigger = (page: Page): Locator =>
  filters(page).locator(".sort-section").getByRole("button");

export const searchField = (page: Page): Locator =>
  filters(page).getByRole("textbox", { name: "Поиск по названию" });

export async function openFilterDropdown(page: Page): Promise<Locator> {
  const region = filters(page);
  await region.getByRole("button", { name: "Фильтры" }).click();
  return region;
}

export async function openSortMenu(page: Page): Promise<Locator> {
  await sortTrigger(page).click();
  return filters(page).locator(".sort-section").getByRole("menu");
}

/**
 * The one value that occurs exactly once, so a text locator built from it
 * resolves to a single element. Rows and tag links are otherwise
 * indistinguishable, and a positional locator would quietly follow whatever
 * the current sort order puts first.
 */
export function onlyOccurrence(values: string[], what: string): string {
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
export function rowOfGame(page: Page, title: string): Locator {
  return table(page)
    .getByRole("row")
    .filter({ has: page.getByRole("link", { name: title, exact: true }) });
}

export async function anyGameTitle(page: Page): Promise<string> {
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
export async function unambiguousTag(
  request: APIRequestContext,
): Promise<CatalogTag> {
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
