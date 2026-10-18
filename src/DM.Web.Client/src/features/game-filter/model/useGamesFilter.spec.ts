/**
 * @vitest-environment jsdom
 */

/**
 * The games filter read through the address, the one contract it has with a
 * reader: a bookmarked list opens on the same games. What an address means,
 * what each action writes into it, and that an address the filter wrote reads
 * back as itself.
 */
import { describe, it, expect, afterEach } from "vitest";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { useGamesFilter } from "./useGamesFilter";

type Filter = ReturnType<typeof useGamesFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(useGamesFilter, query);
  return page;
}

const DEFAULT_STATE = {
  search: "",
  status: null,
  recruitmentFilter: "any",
  closedReasonFilter: "any",
  requiredTags: [],
  excludedTags: [],
  hostUsernames: [],
  createdFromUtc: null,
  createdToUtc: null,
  activatedFromUtc: null,
  activatedToUtc: null,
  closedFromUtc: null,
  closedToUtc: null,
  recruitmentStartedFromUtc: null,
  recruitmentStartedToUtc: null,
  sortBy: "created",
  sortOrder: "desc",
};

const FULL = {
  search: "дракон",
  status: "Active",
  recruitmentFilter: "open",
  requiredTags: "3,5",
  excludedTags: "7",
  hosts: "anna,boris",
  createdFromUtc: "2026-01-01",
  createdToUtc: "2026-02-01",
  activatedFromUtc: "2026-03-01",
  activatedToUtc: "2026-04-01",
  closedFromUtc: "2026-05-01",
  closedToUtc: "2026-06-01",
  recruitmentStartedFromUtc: "2026-07-01",
  recruitmentStartedToUtc: "2026-08-01",
  sortBy: "title",
  sortOrder: "asc",
};

describe("useGamesFilter: what an address means", () => {
  it("reads an empty address as every game, newest first", async () => {
    const { filter } = await on({});

    expect(plain(filter.filterState.value)).toEqual(DEFAULT_STATE);
    expect(filter.searchParams.value).toEqual({
      sortBy: "created",
      sortOrder: "desc",
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(false);
    expect(filter.activeFilters.value).toEqual([]);
  });

  it("reads every part of a full address, and asks for whole days", async () => {
    const { filter } = await on({ ...FULL, number: "3" });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      search: "дракон",
      status: "Active",
      recruitmentFilter: "open",
      requiredTags: [3, 5],
      excludedTags: [7],
      hostUsernames: ["anna", "boris"],
      createdFromUtc: "2026-01-01",
      createdToUtc: "2026-02-01",
      activatedFromUtc: "2026-03-01",
      activatedToUtc: "2026-04-01",
      closedFromUtc: "2026-05-01",
      closedToUtc: "2026-06-01",
      recruitmentStartedFromUtc: "2026-07-01",
      recruitmentStartedToUtc: "2026-08-01",
      sortBy: "title",
      sortOrder: "asc",
    });
    expect(filter.searchParams.value).toEqual({
      search: "дракон",
      status: "Active",
      recruitmentFilter: "open",
      requiredTags: [3, 5],
      excludedTags: [7],
      hostUsernames: ["anna", "boris"],
      createdFromUtc: "2026-01-01T00:00:00Z",
      createdToUtc: "2026-02-01T23:59:59Z",
      activatedFromUtc: "2026-03-01T00:00:00Z",
      activatedToUtc: "2026-04-01T23:59:59Z",
      closedFromUtc: "2026-05-01T00:00:00Z",
      closedToUtc: "2026-06-01T23:59:59Z",
      recruitmentStartedFromUtc: "2026-07-01T00:00:00Z",
      recruitmentStartedToUtc: "2026-08-01T23:59:59Z",
      sortBy: "title",
      sortOrder: "asc",
      number: 3,
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(true);
    expect(filter.activeFilters.value.map((chip) => chip.label)).toEqual([
      "Идет игра",
      "Набор: Набор игроков",
      "+Тег #3",
      "+Тег #5",
      "-Тег #7",
      "ведущий: anna",
      "ведущий: boris",
    ]);
  });

  it("drops what it cannot read and keeps the rest", async () => {
    const { filter } = await on({
      status: "Bogus",
      recruitmentFilter: "nope",
      requiredTags: "x,0,-2,4",
      excludedTags: "",
      hosts: " anna ,,averyveryverylongusername",
      createdFromUtc: "not-a-date",
      sortBy: "bogus",
      sortOrder: "up",
      number: "abc",
    });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      requiredTags: [4],
      // A name is cut at 20 characters.
      hostUsernames: ["anna", "averyveryverylonguse"],
    });
    expect(filter.searchParams.value).toEqual({
      requiredTags: [4],
      hostUsernames: ["anna", "averyveryverylonguse"],
      sortBy: "created",
      sortOrder: "desc",
      size: 20,
    });
  });

  it("asks for a sub-filter only under the status it belongs to", async () => {
    const { filter } = await on({
      status: "Closed",
      recruitmentFilter: "open",
      closedReasonFilter: "Frozen",
    });

    expect(filter.filterState.value.recruitmentFilter).toBe("open");
    expect(filter.searchParams.value).toEqual({
      status: "Closed",
      closedReasonFilter: "Frozen",
      sortBy: "created",
      sortOrder: "desc",
      size: 20,
    });
    expect(filter.activeFilters.value.map((chip) => chip.label)).toEqual([
      "Закрыта",
      "Причина: Заморожена",
    ]);
  });

  it.each<[string, Record<string, string>, boolean]>([
    ["a search alone", { search: "x" }, true],
    ["a date alone", { closedToUtc: "2026-01-01" }, true],
    ["a sort alone", { sortBy: "title", sortOrder: "asc" }, false],
  ])("counts %s as a filter: %s", async (_what, query, counted) => {
    const { filter } = await on(query);

    expect(filter.hasActiveFilters.value).toBe(counted);
  });
});

describe("useGamesFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    ["a search", {}, (f) => f.setSearch("эльф"), { search: "эльф" }],
    ["a status", {}, (f) => f.setStatus("Draft"), { status: "Draft" }],
    [
      "another status, dropping the sub-filter of the old one",
      { status: "Active", recruitmentFilter: "open" },
      (f) => f.setStatus("Closed"),
      { status: "Closed" },
    ],
    [
      "another status and back in one go, the sub-filter gone",
      { status: "Active", recruitmentFilter: "open" },
      (f) => {
        f.setStatus("Closed");
        f.setStatus("Active");
      },
      { status: "Active" },
    ],
    [
      "the same status, keeping its sub-filter",
      { status: "Active", recruitmentFilter: "open" },
      (f) => f.setStatus("Active"),
      { status: "Active", recruitmentFilter: "open" },
    ],
    [
      "no status",
      { status: "Active", recruitmentFilter: "open" },
      (f) => f.clearStatus(),
      {},
    ],
    [
      "a recruitment filter under Active",
      { status: "Active" },
      (f) => f.setRecruitmentFilter("subsequent"),
      { status: "Active", recruitmentFilter: "subsequent" },
    ],
    [
      "a recruitment filter without Active, which the address does not keep",
      {},
      (f) => f.setRecruitmentFilter("subsequent"),
      {},
    ],
    [
      "a closed reason under Closed",
      { status: "Closed" },
      (f) => f.setClosedReasonFilter("Finished"),
      { status: "Closed", closedReasonFilter: "Finished" },
    ],
    [
      "a required tag, taking it off the excluded",
      { excludedTags: "5,6" },
      (f) => f.addRequiredTag(5),
      { requiredTags: "5", excludedTags: "6" },
    ],
    [
      "an excluded tag, taking it off the required",
      { requiredTags: "3,4" },
      (f) => f.addExcludedTag(3),
      { requiredTags: "4", excludedTags: "3" },
    ],
    [
      "a tag removed from either list",
      { requiredTags: "3,4", excludedTags: "4" },
      (f) => f.removeTag(4),
      { requiredTags: "3" },
    ],
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
    [
      "no hosts",
      { hosts: "boris,anna", search: "x" },
      (f) => f.clearHosts(),
      { search: "x" },
    ],
    [
      "each range of dates",
      {},
      (f) => {
        f.setCreatedRange("2026-01-01", null);
        f.setActivatedRange(null, "2026-02-01");
        f.setClosedRange("2026-03-01", "2026-03-02");
        f.setRecruitmentStartedRange("2026-04-01", null);
      },
      {
        createdFromUtc: "2026-01-01",
        activatedToUtc: "2026-02-01",
        closedFromUtc: "2026-03-01",
        closedToUtc: "2026-03-02",
        recruitmentStartedFromUtc: "2026-04-01",
      },
    ],
    [
      "no dates, the rest kept",
      {
        createdFromUtc: "2026-01-01",
        closedToUtc: "2026-02-01",
        hosts: "anna",
      },
      (f) => f.clearDateRanges(),
      { hosts: "anna" },
    ],
    [
      "a sort in the direction of its own",
      {},
      (f) => f.setSort("title"),
      { sortBy: "title", sortOrder: "asc" },
    ],
    [
      "a sort whose own direction is not the filter's fallback",
      { sortOrder: "asc" },
      (f) => f.setSort("popularity"),
      { sortBy: "popularity" },
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
      "the tags that no longer exist taken off",
      { requiredTags: "3,9", excludedTags: "8" },
      (f) => f.validateTagFilters(new Set([3])),
      { requiredTags: "3" },
    ],
    [
      "two quick presses, as one",
      {},
      (f) => {
        f.addRequiredTag(1);
        f.addRequiredTag(2);
      },
      { requiredTags: "1,2" },
    ],
    [
      "any change, leaving the page number behind",
      { search: "x", number: "3" },
      (f) => f.setSearch("y"),
      { search: "y" },
    ],
    [
      "the same search again, which still leaves the page number behind",
      { search: "x", number: "3" },
      (f) => f.setSearch("x"),
      { search: "x" },
    ],
  ])("writes %s", async (_what, query, act, written) => {
    const { filter, settled } = await on(query);

    act(filter);

    expect(await settled()).toEqual(written);
  });

  it("leaves the address alone when every tag still exists", async () => {
    const { filter, settled } = await on({ requiredTags: "3", number: "2" });

    filter.validateTagFilters(new Set([3, 4]));

    expect(await settled()).toEqual({ requiredTags: "3", number: "2" });
  });

  it("drops a change when the reader has left the page before it was written", async () => {
    const { filter, router, settled } = await on({});

    filter.setSearch("эльф");
    await router.push({ path: "/elsewhere", query: { tab: "1" } });

    expect(await settled()).toEqual({ tab: "1" });
  });
});

describe("useGamesFilter: an address written reads back as itself", () => {
  /**
   * The address the filter writes for what it read off this one: a search
   * set to the search it already has is an action that changes nothing.
   */
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
      {
        status: "Bogus",
        requiredTags: "x,4",
        sortBy: "bogus",
        sortOrder: "desc",
      },
      { requiredTags: "4" },
    ],
    [
      "one with a sub-filter under the wrong status",
      { status: "Closed", recruitmentFilter: "open" },
      { status: "Closed" },
    ],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
