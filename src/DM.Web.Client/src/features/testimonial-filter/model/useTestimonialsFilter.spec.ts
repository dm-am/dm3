/**
 * @vitest-environment jsdom
 */

/**
 * The testimonials filter read through the address: what an address means,
 * what each action writes into it, and that an address the filter wrote reads
 * back as itself.
 */
import { describe, it, expect, afterEach } from "vitest";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { useTestimonialsFilter } from "./useTestimonialsFilter";

type Filter = ReturnType<typeof useTestimonialsFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(useTestimonialsFilter, query);
  return page;
}

describe("useTestimonialsFilter: what an address means", () => {
  it.each<[string, Record<string, string>, object, object, boolean]>([
    [
      "an empty one as every testimonial, newest first, ten to a page",
      {},
      { search: "", sortBy: "created", sortOrder: "desc" },
      { sortBy: "created", sortOrder: "desc", size: 10 },
      false,
    ],
    [
      "a full one",
      { search: "мастер", sortBy: "author", sortOrder: "asc", number: "4" },
      { search: "мастер", sortBy: "author", sortOrder: "asc" },
      {
        search: "мастер",
        sortBy: "author",
        sortOrder: "asc",
        number: 4,
        size: 10,
      },
      true,
    ],
    [
      "one it cannot read as the default",
      { sortBy: "rating", sortOrder: "up", number: "-1" },
      { search: "", sortBy: "created", sortOrder: "desc" },
      { sortBy: "created", sortOrder: "desc", size: 10 },
      false,
    ],
    [
      "a sort alone as no filter",
      { sortBy: "author" },
      { search: "", sortBy: "author", sortOrder: "desc" },
      { sortBy: "author", sortOrder: "desc", size: 10 },
      false,
    ],
  ])("reads %s", async (_what, query, state, params, active) => {
    const { filter } = await on(query);

    expect(plain(filter.filterState.value)).toEqual(state);
    expect(filter.searchParams.value).toEqual(params);
    expect(filter.hasActiveFilters.value).toBe(active);
  });

  it("cuts a search at 200 characters", async () => {
    const { filter } = await on({ search: "а".repeat(250) });

    expect(filter.filterState.value.search).toHaveLength(200);
  });
});

describe("useTestimonialsFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    ["a search", {}, (f) => f.setSearch("игрок"), { search: "игрок" }],
    [
      "a sort in the direction of its own",
      {},
      (f) => f.setSort("author"),
      { sortBy: "author", sortOrder: "asc" },
    ],
    [
      "a sort in a direction given",
      {},
      (f) => f.setSort("author", "desc"),
      { sortBy: "author" },
    ],
    [
      "the direction turned",
      {},
      (f) => f.toggleSortOrder(),
      { sortOrder: "asc" },
    ],
    [
      "everything cleared, the sort as well",
      { search: "x", sortBy: "author", sortOrder: "asc" },
      (f) => f.clearFilters(),
      {},
    ],
    [
      "any change, leaving the page number behind",
      { search: "x", number: "3" },
      (f) => f.setSearch("y"),
      { search: "y" },
    ],
  ])("writes %s", async (_what, query, act, written) => {
    const { filter, settled } = await on(query);

    act(filter);

    expect(await settled()).toEqual(written);
  });
});

describe("useTestimonialsFilter: an address written reads back as itself", () => {
  async function rewritten(query: Record<string, string>) {
    const { filter, settled, wrapper } = await on(query);
    filter.setSearch(filter.filterState.value.search);
    const written = await settled();
    wrapper.unmount();
    page = null;
    return written;
  }

  it.each<[string, Record<string, string>, Record<string, string>]>([
    ["an empty one", {}, {}],
    [
      "a full one",
      { search: "мастер", sortBy: "author", sortOrder: "asc" },
      { search: "мастер", sortBy: "author", sortOrder: "asc" },
    ],
    [
      "one with the defaults spelled out",
      { sortBy: "created", sortOrder: "desc", number: "2" },
      {},
    ],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
