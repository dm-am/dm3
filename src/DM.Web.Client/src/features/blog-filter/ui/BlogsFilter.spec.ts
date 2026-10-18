/**
 * @vitest-environment jsdom
 */

/**
 * Backspace in the empty search of the blogs filter takes the filters off one
 * at a time, the last bubble of the row first, and the address follows each
 * press.
 */
import { describe, it, expect, afterEach } from "vitest";
import { onListPage, type Page } from "@/shared/lib/testing/urlFilter";
import BlogsFilter from "./BlogsFilter.vue";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function filterOn(query: Record<string, string>) {
  page = await onListPage(BlogsFilter, query, {
    global: { stubs: { UserMultiSelect: true } },
  });
  return page;
}

async function backspace(on: Page) {
  await on.wrapper.get(".the-input").trigger("keydown", { key: "Backspace" });
  return on.settled();
}

describe("BlogsFilter: Backspace in the empty search", () => {
  it("takes the filters off from the last bubble back to the status", async () => {
    const on = await filterOn({
      status: "Active",
      hosts: "anna,boris",
      createdFromUtc: "2026-01-01",
      activatedToUtc: "2026-02-01",
      closedFromUtc: "2026-03-01",
    });

    const steps = [];
    for (let press = 0; press < 7; press++) steps.push(await backspace(on));

    expect(steps).toEqual([
      {
        status: "Active",
        hosts: "anna,boris",
        createdFromUtc: "2026-01-01",
        activatedToUtc: "2026-02-01",
      },
      { status: "Active", hosts: "anna,boris", createdFromUtc: "2026-01-01" },
      { status: "Active", hosts: "anna,boris" },
      { status: "Active", hosts: "anna" },
      { status: "Active" },
      {},
      {},
    ]);
  });

  it("leaves the filters alone while the search holds a word", async () => {
    const on = await filterOn({ search: "сага", status: "Closed" });

    expect(await backspace(on)).toEqual({ search: "сага", status: "Closed" });
  });
});
