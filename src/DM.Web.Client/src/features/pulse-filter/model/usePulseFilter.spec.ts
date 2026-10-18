/**
 * @vitest-environment jsdom
 */

/**
 * The pulse filter read through the address: what an address means, what each
 * action writes into it, and that an address the filter wrote reads back as
 * itself. Unlike the filters of the catalogue it keeps the address as it is for
 * an action that changes nothing, page number included.
 */
import { describe, it, expect, afterEach } from "vitest";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { usePulseFilter } from "./usePulseFilter";

type Filter = ReturnType<typeof usePulseFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(usePulseFilter, query);
  return page;
}

const DEFAULT_STATE = {
  search: "",
  sortBy: "lastreview",
  sortOrder: "desc",
  minRating: null,
  maxRating: null,
  authorUsernames: [],
  createdFrom: null,
  createdTo: null,
  gameId: null,
};

const FULL = {
  q: "космос",
  sort: "rating",
  order: "asc",
  minRating: "2",
  maxRating: "5",
  authors: "anna,boris",
  createdFrom: "2026-01-01",
  createdTo: "2026-02-01",
  game: "42",
};

describe("usePulseFilter: what an address means", () => {
  it("reads an empty address as every review, the latest first", async () => {
    const { filter } = await on({});

    expect(plain(filter.filterState.value)).toEqual(DEFAULT_STATE);
    expect(filter.searchParams.value).toEqual({
      sortBy: "lastreview",
      sortOrder: "desc",
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(false);
  });

  it("reads every part of a full address, and sends the days as they are", async () => {
    const { filter } = await on({ ...FULL, number: "2" });

    expect(plain(filter.filterState.value)).toEqual({
      search: "космос",
      sortBy: "rating",
      sortOrder: "asc",
      minRating: 2,
      maxRating: 5,
      authorUsernames: ["anna", "boris"],
      createdFrom: "2026-01-01",
      createdTo: "2026-02-01",
      gameId: "42",
    });
    expect(filter.searchParams.value).toEqual({
      search: "космос",
      sortBy: "rating",
      sortOrder: "asc",
      minRating: 2,
      maxRating: 5,
      authorUsernames: ["anna", "boris"],
      createdFrom: "2026-01-01",
      createdTo: "2026-02-01",
      gameId: "42",
      number: 2,
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(true);
  });

  it("drops what it cannot read and keeps the rest", async () => {
    const { filter } = await on({
      sort: "title",
      order: "up",
      minRating: "x",
      maxRating: "4.5",
      authors: " anna , ,boris ",
      createdFrom: "2026-02-30",
      createdTo: "2026-01-01T10:00:00Z",
    });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      maxRating: 4,
      authorUsernames: ["anna", "boris"],
      // The day check reads the shape of a date and what Date makes of it,
      // and Date rolls the 30th of February over into March.
      createdFrom: "2026-02-30",
    });
  });

  it.each<[string, Record<string, string>, boolean]>([
    ["a game alone", { game: "7" }, true],
    ["a rating floor alone", { minRating: "0" }, true],
    ["a sort alone", { sort: "rating", order: "asc" }, false],
  ])("counts %s as a filter: %s", async (_what, query, counted) => {
    const { filter } = await on(query);

    expect(filter.hasActiveFilters.value).toBe(counted);
  });
});

describe("usePulseFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    ["a search", {}, (f) => f.setSearch("звезды"), { q: "звезды" }],
    [
      "a sort, downward unless told",
      { order: "asc" },
      (f) => f.setSort("rating"),
      { sort: "rating" },
    ],
    [
      "a sort in a direction given",
      {},
      (f) => f.setSort("reviewcount", "asc"),
      { sort: "reviewcount", order: "asc" },
    ],
    [
      "a range of ratings",
      {},
      (f) => f.setRatingRange(0, 3),
      { minRating: "0", maxRating: "3" },
    ],
    [
      "an author, after the others",
      { authors: "boris" },
      (f) => f.addAuthor("anna"),
      { authors: "boris,anna" },
    ],
    [
      "an author removed",
      { authors: "boris,anna" },
      (f) => f.removeAuthor("boris"),
      { authors: "anna" },
    ],
    [
      "a range of dates",
      {},
      (f) => f.setCreatedRange("2026-01-01", null),
      { createdFrom: "2026-01-01" },
    ],
    ["a game", {}, (f) => f.setGameId("9"), { game: "9" }],
    ["no game", { game: "9" }, (f) => f.setGameId(null), {}],
    ["everything cleared, the sort as well", FULL, (f) => f.clearFilters(), {}],
    [
      "any change, leaving the page number behind",
      { q: "x", number: "3" },
      (f) => f.setSearch("y"),
      { q: "y" },
    ],
    [
      "nothing for the same search, the page number kept",
      { q: "x", number: "3" },
      (f) => f.setSearch("x"),
      { q: "x", number: "3" },
    ],
    [
      "nothing for an author already there",
      { authors: "anna", number: "3" },
      (f) => f.addAuthor("anna"),
      { authors: "anna", number: "3" },
    ],
    [
      "nothing for a clear of what is clear already",
      { number: "3" },
      (f) => f.clearFilters(),
      { number: "3" },
    ],
  ])("writes %s", async (_what, query, act, written) => {
    const { filter, settled } = await on(query);

    act(filter);

    expect(await settled()).toEqual(written);
  });
});

describe("usePulseFilter: an address written reads back as itself", () => {
  /**
   * The address the filter writes for what it read off this one. A search
   * set to the one it has is no change here, so the search is moved away and
   * back in one go.
   */
  async function rewritten(query: Record<string, string>) {
    const { filter, settled, wrapper } = await on(query);
    const search = filter.filterState.value.search;
    filter.setSearch(`${search}~`);
    filter.setSearch(search);
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
      { sort: "title", maxRating: "4.5", authors: " anna ", order: "desc" },
      { maxRating: "4", authors: "anna" },
    ],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
