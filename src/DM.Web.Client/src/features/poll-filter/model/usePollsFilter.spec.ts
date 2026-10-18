/**
 * @vitest-environment jsdom
 */

/**
 * The request of the polls list: a day of the address reaches the server as
 * the whole day. Sent as the bare day, the end of a period was the first
 * instant of its last day, and a poll that started on that day was not found.
 * An address that holds a moment and not a day is read as its day, and one
 * that holds no date at all as no filter.
 */
import { describe, it, expect, afterEach } from "vitest";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { PollStatus } from "@/shared/api/models/community/polls";
import { usePollsFilter } from "./usePollsFilter";

type Filter = ReturnType<typeof usePollsFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(usePollsFilter, query);
  return page;
}

/** The filter as the list reads it, on an address with this query. */
async function filterOn(query: Record<string, string>) {
  return (await on(query)).filter;
}

describe("the request of the polls list", () => {
  it("asks for a period of the start from the start of its first day to the end of its last", async () => {
    const { searchParams } = await filterOn({
      startsFromUtc: "2026-09-01",
      startsToUtc: "2026-09-30",
    });

    expect(searchParams.value).toMatchObject({
      startsFromUtc: "2026-09-01T00:00:00Z",
      startsToUtc: "2026-09-30T23:59:59Z",
    });
  });

  it("asks for a period of the end the same way", async () => {
    const { searchParams } = await filterOn({
      endsFromUtc: "2026-10-01",
      endsToUtc: "2026-10-31",
    });

    expect(searchParams.value).toMatchObject({
      endsFromUtc: "2026-10-01T00:00:00Z",
      endsToUtc: "2026-10-31T23:59:59Z",
    });
  });

  it("reads a moment of the address as its day, and no date as no filter", async () => {
    const { filterState, searchParams, hasActiveFilters } = await filterOn({
      startsFromUtc: "2026-09-01T15:30:00Z",
      endsToUtc: "not-a-date",
    });

    expect(filterState.value.startsFromUtc).toBe("2026-09-01");
    expect(filterState.value.endsToUtc).toBe("");
    expect(searchParams.value.startsFromUtc).toBe("2026-09-01T00:00:00Z");
    expect(searchParams.value).not.toHaveProperty("endsToUtc");
    expect(hasActiveFilters.value).toBe(true);
  });
});

const FULL = {
  status: "Active",
  pollType: "anonymous",
  search: "выборы",
  startsFromUtc: "2026-09-01",
  startsToUtc: "2026-09-30",
  endsFromUtc: "2026-10-01",
  endsToUtc: "2026-10-31",
  sortBy: "starts",
  sortOrder: "desc",
};

describe("usePollsFilter: what an address means", () => {
  it("reads an empty address as every poll by status, fifteen to a page", async () => {
    const { filter } = await on({});

    expect(plain(filter.filterState.value)).toEqual({
      status: "",
      search: "",
      startsFromUtc: "",
      startsToUtc: "",
      endsFromUtc: "",
      endsToUtc: "",
      pollType: "",
      sortBy: "status",
      sortOrder: "asc",
    });
    expect(filter.searchParams.value).toEqual({
      sortBy: "status",
      sortOrder: "asc",
      size: 15,
    });
    expect(filter.hasActiveFilters.value).toBe(false);
  });

  it("reads every part of a full address", async () => {
    const { filter } = await on({ ...FULL, number: "2" });

    expect(filter.searchParams.value).toEqual({
      status: "Active",
      isAnonymous: true,
      search: "выборы",
      startsFromUtc: "2026-09-01T00:00:00Z",
      startsToUtc: "2026-09-30T23:59:59Z",
      endsFromUtc: "2026-10-01T00:00:00Z",
      endsToUtc: "2026-10-31T23:59:59Z",
      sortBy: "starts",
      sortOrder: "desc",
      number: 2,
      size: 15,
    });
    expect(filter.hasActiveFilters.value).toBe(true);
  });

  it("asks for the public ones as not anonymous", async () => {
    const { filter } = await on({ pollType: "public" });

    expect(filter.searchParams.value.isAnonymous).toBe(false);
  });

  it("drops what it cannot read", async () => {
    const { filter } = await on({
      status: "Archived",
      pollType: "secret",
      sortBy: "votes",
      sortOrder: "up",
    });

    expect(filter.searchParams.value).toEqual({
      sortBy: "status",
      sortOrder: "asc",
      size: 15,
    });
  });
});

describe("usePollsFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    [
      "a status",
      {},
      (f) => f.setStatus(PollStatus.Pending),
      { status: "Pending" },
    ],
    ["every status", { status: "Closed" }, (f) => f.setStatus(""), {}],
    [
      "a kind of poll",
      {},
      (f) => f.setPollType("public"),
      { pollType: "public" },
    ],
    ["a search", {}, (f) => f.setSearch("бюджет"), { search: "бюджет" }],
    [
      "each end of each period",
      {},
      (f) => {
        f.setStartsFromUtc("2026-09-01");
        f.setStartsToUtc("2026-09-02");
        f.setEndsFromUtc("2026-10-01");
        f.setEndsToUtc("2026-10-02");
      },
      {
        startsFromUtc: "2026-09-01",
        startsToUtc: "2026-09-02",
        endsFromUtc: "2026-10-01",
        endsToUtc: "2026-10-02",
      },
    ],
    [
      "a sort in the direction of its own",
      {},
      (f) => f.setSort("starts"),
      { sortBy: "starts", sortOrder: "desc" },
    ],
    [
      "a sort in a direction given",
      {},
      (f) => f.setSort("ends", "asc"),
      { sortBy: "ends" },
    ],
    [
      "the direction turned",
      {},
      (f) => f.toggleSortOrder(),
      { sortOrder: "desc" },
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

describe("usePollsFilter: an address written reads back as itself", () => {
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
      "one with a moment for a day and a word for a date",
      { startsFromUtc: "2026-09-01T15:30:00Z", endsToUtc: "not-a-date" },
      { startsFromUtc: "2026-09-01" },
    ],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
