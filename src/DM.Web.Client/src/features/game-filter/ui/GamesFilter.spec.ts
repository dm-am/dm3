/**
 * @vitest-environment jsdom
 */

/**
 * Backspace in the empty search of the games filter takes the filters off one
 * at a time, the last bubble of the row first, and the address follows each
 * press.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { gameApi } from "@/entities/game";
import { onListPage, type Page } from "@/shared/lib/testing/urlFilter";
import GamesFilter from "./GamesFilter.vue";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
  vi.restoreAllMocks();
});

/** The tags the store knows, so the filter keeps the ones in the address. */
const tag = (id: number) => ({
  id,
  title: `Тег ${id}`,
  groupTitle: "Жанр",
  groupSortOrder: 1,
  sortOrder: id,
});

async function filterOn(query: Record<string, string>) {
  vi.spyOn(gameApi, "getTags").mockResolvedValue({
    data: { resources: [3, 5, 7, 8].map(tag) },
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
