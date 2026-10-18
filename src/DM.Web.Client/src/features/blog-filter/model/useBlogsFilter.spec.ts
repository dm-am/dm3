/**
 * @vitest-environment jsdom
 */

/**
 * The blogs filter read through the address: what an address means, what each
 * action writes into it, and that an address the filter wrote reads back as
 * itself.
 */
import { describe, it, expect, afterEach } from "vitest";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { useBlogsFilter } from "./useBlogsFilter";

type Filter = ReturnType<typeof useBlogsFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(useBlogsFilter, query);
  return page;
}

const DEFAULT_STATE = {
  search: "",
  status: "any",
  hostUsernames: [],
  createdFromUtc: null,
  createdToUtc: null,
  activatedFromUtc: null,
  activatedToUtc: null,
  closedFromUtc: null,
  closedToUtc: null,
  sortBy: "created",
  sortOrder: "desc",
};

const FULL = {
  search: "хроники",
  status: "Closed",
  hosts: "anna,boris",
  createdFromUtc: "2026-01-01",
  createdToUtc: "2026-02-01",
  activatedFromUtc: "2026-03-01",
  activatedToUtc: "2026-04-01",
  closedFromUtc: "2026-05-01",
  closedToUtc: "2026-06-01",
  sortBy: "title",
  sortOrder: "asc",
};

describe("useBlogsFilter: what an address means", () => {
  it("reads an empty address as every blog, newest first", async () => {
    const { filter } = await on({});

    expect(plain(filter.filterState.value)).toEqual(DEFAULT_STATE);
    expect(filter.searchParams.value).toEqual({
      sortBy: "created",
      sortOrder: "desc",
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(false);
  });

  it("reads every part of a full address, and asks for whole days", async () => {
    const { filter } = await on({ ...FULL, number: "2" });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      search: "хроники",
      status: "Closed",
      hostUsernames: ["anna", "boris"],
      createdFromUtc: "2026-01-01",
      createdToUtc: "2026-02-01",
      activatedFromUtc: "2026-03-01",
      activatedToUtc: "2026-04-01",
      closedFromUtc: "2026-05-01",
      closedToUtc: "2026-06-01",
      sortBy: "title",
      sortOrder: "asc",
    });
    expect(filter.searchParams.value).toEqual({
      search: "хроники",
      status: "Closed",
      hostUsernames: ["anna", "boris"],
      createdFromUtc: "2026-01-01T00:00:00Z",
      createdToUtc: "2026-02-01T23:59:59Z",
      activatedFromUtc: "2026-03-01T00:00:00Z",
      activatedToUtc: "2026-04-01T23:59:59Z",
      closedFromUtc: "2026-05-01T00:00:00Z",
      closedToUtc: "2026-06-01T23:59:59Z",
      sortBy: "title",
      sortOrder: "asc",
      number: 2,
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(true);
  });

  it("drops what it cannot read and keeps the rest", async () => {
    const { filter } = await on({
      status: "Bogus",
      hosts: " anna ,,averyveryverylongusername",
      activatedToUtc: "31.02.2026",
      sortBy: "bogus",
      sortOrder: "up",
      number: "0",
    });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      // A name is cut at 20 characters.
      hostUsernames: ["anna", "averyveryverylonguse"],
    });
    expect(filter.searchParams.value).toEqual({
      hostUsernames: ["anna", "averyveryverylonguse"],
      sortBy: "created",
      sortOrder: "desc",
      size: 20,
    });
  });

  it.each<[string, Record<string, string>, boolean]>([
    ["a status alone", { status: "Draft" }, true],
    ["a date alone", { closedToUtc: "2026-01-01" }, true],
    ["a sort alone", { sortBy: "title", sortOrder: "asc" }, false],
  ])("counts %s as a filter: %s", async (_what, query, counted) => {
    const { filter } = await on(query);

    expect(filter.hasActiveFilters.value).toBe(counted);
  });
});

describe("useBlogsFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    ["a search", {}, (f) => f.setSearch("сага"), { search: "сага" }],
    ["a status", {}, (f) => f.setStatus("Active"), { status: "Active" }],
    ["every status again", { status: "Active" }, (f) => f.setStatus("any"), {}],
    ["no status", { status: "Draft" }, (f) => f.clearStatus(), {}],
    [
      "a host, after the others",
      { hosts: "boris" },
      (f) => f.addHost("anna"),
      { hosts: "boris,anna" },
    ],
    [
      "a host removed",
      { hosts: "boris,anna" },
      (f) => f.removeHost("boris"),
      { hosts: "anna" },
    ],
    ["no hosts", { hosts: "boris,anna" }, (f) => f.clearHosts(), {}],
    [
      "each range of dates",
      {},
      (f) => {
        f.setCreatedRange("2026-01-01", null);
        f.setActivatedRange(null, "2026-02-01");
        f.setClosedRange("2026-03-01", "2026-03-02");
      },
      {
        createdFromUtc: "2026-01-01",
        activatedToUtc: "2026-02-01",
        closedFromUtc: "2026-03-01",
        closedToUtc: "2026-03-02",
      },
    ],
    [
      "a sort in the direction of its own",
      {},
      (f) => f.setSort("title"),
      { sortBy: "title", sortOrder: "asc" },
    ],
    [
      "a sort in a direction given",
      {},
      (f) => f.setSort("title", "desc"),
      { sortBy: "title" },
    ],
    [
      "the direction turned",
      {},
      (f) => f.toggleSortOrder(),
      { sortOrder: "asc" },
    ],
    ["everything cleared, the sort as well", FULL, (f) => f.clearFilters(), {}],
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

describe("useBlogsFilter: an address written reads back as itself", () => {
  /** The address the filter writes for what it read off this one. */
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
    ["a full one", FULL, FULL],
    [
      "one with what it cannot read",
      { status: "Bogus", hosts: "anna", sortOrder: "desc", number: "4" },
      { hosts: "anna" },
    ],
    ["one asking for every status", { status: "any" }, {}],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
