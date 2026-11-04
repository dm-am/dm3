/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { gameApi, type Tag } from "@/entities/game";
import { onListPage, type Page } from "@/shared/lib/testing/urlFilter";
import GamesFilter from "./GamesFilter.vue";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
  vi.restoreAllMocks();
});

const tag = (id: number) => ({
  id,
  title: `Тег ${id}`,
  groupTitle: "Жанр",
  groupSortOrder: 1,
  sortOrder: id,
});

// An order of the catalog that is neither the order of the ids nor of the address.
const CATALOG = [
  { ...tag(3), sortOrder: 2 },
  { ...tag(5), sortOrder: 1 },
  { ...tag(7), groupTitle: "Система", groupSortOrder: 2, sortOrder: 1 },
  { ...tag(8), sortOrder: 9 },
];

async function filterOn(
  query: Record<string, string>,
  catalog: Partial<Tag>[] = [3, 5, 7, 8].map(tag),
) {
  vi.spyOn(gameApi, "getTags").mockResolvedValue({
    data: { resources: catalog },
    error: null,
  } as never);
  page = await onListPage(GamesFilter, query, {
    global: { stubs: { UserMultiSelect: true } },
  });
  return page;
}

async function backspace(on: Page) {
  await on.wrapper.get(".the-input").trigger("keydown", { key: "Backspace" });
  return on.settled();
}

const bubbles = (on: Page) =>
  on.wrapper
    .findAll(".bubble")
    .map(
      (bubble) =>
        `${bubble.find(".bubble-prefix").text()} ${bubble.get(".bubble-value-text").text()}`,
    );

const menuOpen = (on: Page) =>
  on.wrapper.find(".filter-dropdown-container").exists();

const items = (on: Page) =>
  on.wrapper.findAll("button.dropdown-item .item-label").map((l) => l.text());

async function click(on: Page, selector: string) {
  await on.wrapper.get(selector).trigger("click");
  await flushPromises();
}

async function walk(on: Page, path: string) {
  await click(on, ".filter-btn");
  for (const label of path.split(" > ")) {
    const item = on.wrapper
      .findAll("button.dropdown-item")
      .find((button) => button.get(".item-label").text() === label);
    if (!item) throw new Error(`no item "${label}" in ${items(on).join(", ")}`);
    await item.trigger("click");
    await flushPromises();
  }
}

const DATES = {
  createdFromUtc: "2026-01-01",
  activatedFromUtc: "2026-02-01",
  recruitmentStartedFromUtc: "2026-03-01",
  closedToUtc: "2026-04-01",
};

describe("GamesFilter: Backspace in the empty search", () => {
  it("takes the filters off from the last bubble back to the status", async () => {
    const on = await filterOn({
      status: "Active",
      recruitmentFilter: "open",
      hosts: "anna,boris",
      ...DATES,
      requiredTags: "3,5",
      excludedTags: "7,8",
    });
    const base = { status: "Active", recruitmentFilter: "open" };
    const { createdFromUtc, activatedFromUtc, recruitmentStartedFromUtc } =
      DATES;

    const steps = [];
    for (let press = 0; press < 12; press++) steps.push(await backspace(on));

    expect(steps).toEqual([
      {
        ...base,
        hosts: "anna,boris",
        ...DATES,
        requiredTags: "3,5",
        excludedTags: "7",
      },
      { ...base, hosts: "anna,boris", ...DATES, requiredTags: "3,5" },
      { ...base, hosts: "anna,boris", ...DATES, requiredTags: "3" },
      { ...base, hosts: "anna,boris", ...DATES },
      {
        ...base,
        hosts: "anna,boris",
        createdFromUtc,
        activatedFromUtc,
        recruitmentStartedFromUtc,
      },
      { ...base, hosts: "anna,boris", createdFromUtc, activatedFromUtc },
      { ...base, hosts: "anna,boris", createdFromUtc },
      { ...base, hosts: "anna,boris" },
      { ...base, hosts: "anna" },
      base,
      {},
      {},
    ]);
  });

  it("leaves the filters alone while the search holds a word", async () => {
    const on = await filterOn({ search: "эльф", status: "Closed" });

    expect(await backspace(on)).toEqual({ search: "эльф", status: "Closed" });
  });
});

describe("GamesFilter: the row of bubbles", () => {
  it("lays out the status, the ranges of dates in the order of the table and the tags in the order of the catalog", async () => {
    const on = await filterOn(
      {
        status: "Active",
        recruitmentFilter: "initial",
        closedToUtc: "2026-04-30",
        activatedFromUtc: "2026-02-01",
        activatedToUtc: "2026-02-28",
        createdFromUtc: "2026-01-01",
        requiredTags: "3,5",
        excludedTags: "7,8",
      },
      CATALOG,
    );

    expect(bubbles(on)).toEqual([
      "Статус игры: Идет игра (первый набор)",
      "Создание: с 01.01.2026",
      "Начало игры: 01.02.2026 — 28.02.2026",
      "Закрытие: до 30.04.2026",
      "Тег: Тег 5",
      "Тег: Тег 3",
      "Без тега: Тег 8",
      "Без тега: Тег 7",
    ]);
  });

  it("shows the row for a range of dates alone", async () => {
    const on = await filterOn({ recruitmentStartedFromUtc: "2026-03-01" });

    expect(bubbles(on)).toEqual(["Начало набора: с 01.03.2026"]);
  });
});

describe("GamesFilter: the levels of the menu", () => {
  it.each<[string, Record<string, string>]>([
    ["Статус игры > Оформляется", { status: "Draft" }],
    [
      "Статус игры > Идет игра > Набор игроков > Первый набор",
      { status: "Active", recruitmentFilter: "initial" },
    ],
    [
      "Статус игры > Закрыта > Заморожена",
      { status: "Closed", closedReasonFilter: "Frozen" },
    ],
  ])("sets the status of %s and closes", async (path, address) => {
    const on = await filterOn({});

    await walk(on, path);

    expect(await on.settled()).toEqual(address);
    expect(menuOpen(on)).toBe(false);
  });

  it("clears the range of dates its picker was opened for and closes", async () => {
    const on = await filterOn({
      createdFromUtc: "2026-01-01",
      recruitmentStartedFromUtc: "2026-03-01",
    });

    await walk(on, "Даты > Начало последнего набора");
    const clear = on.wrapper
      .findAll(".dropdown-date-range button")
      .find((button) => button.text() === "Сбросить");
    await clear!.trigger("click");

    expect(await on.settled()).toEqual({ createdFromUtc: "2026-01-01" });
    expect(menuOpen(on)).toBe(false);
  });

  it("empties the tag search when the group is left", async () => {
    const on = await filterOn({}, CATALOG);
    await walk(on, "Тег > Жанр");
    await on.wrapper.get(".dropdown-search-input").setValue("3");
    expect(items(on)).toEqual(["Тег 3"]);

    await click(on, ".nav-back-btn");

    expect(items(on)).toEqual(["Жанр", "Система"]);
    expect(
      (on.wrapper.get(".dropdown-search-input").element as HTMLInputElement)
        .value,
    ).toBe("");
  });
});

// "Nothing found" answers the catalog, so it waits for the catalog to come.
describe("GamesFilter: the tag search before the tags come", () => {
  async function tagSearchWith(answer: Promise<unknown>) {
    vi.spyOn(gameApi, "getTags").mockReturnValue(answer as never);
    page = await onListPage(
      GamesFilter,
      {},
      {
        global: { stubs: { UserMultiSelect: true } },
      },
    );
    await walk(page, "Тег");
    await page.wrapper.get(".dropdown-search-input").setValue("нет такого");
    return page;
  }

  it("says nothing while the tags are on their way", async () => {
    const on = await tagSearchWith(new Promise(() => {}));

    expect(on.wrapper.text()).not.toContain("Ничего не найдено");
  });

  it("says nothing found once the tags have come", async () => {
    const on = await tagSearchWith(
      Promise.resolve({ data: { resources: CATALOG }, error: null }),
    );

    expect(on.wrapper.text()).toContain("Ничего не найдено");
  });

  it("says the tags did not come and offers a retry", async () => {
    const on = await tagSearchWith(
      Promise.resolve({ data: null, error: { status: 500, title: "x" } }),
    );

    expect(on.wrapper.text()).not.toContain("Ничего не найдено");
    expect(on.wrapper.text()).toContain("Не удалось загрузить теги");
    expect(on.wrapper.find(".retry").text()).toBe("Повторить");
  });
});
