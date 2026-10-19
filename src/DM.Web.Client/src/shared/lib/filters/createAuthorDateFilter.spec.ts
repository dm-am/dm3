/**
 * @vitest-environment jsdom
 */

/**
 * The scaffold of a URL-synced author/date filter has one home.
 *
 * useTopicsFilter and useCommentsFilter were byte-for-byte twins of 280 lines:
 * the same eight actions, the same reducer, the same query parser and builder,
 * differing in their default sort. A
 * change to the common half had to be made twice, and nothing pointed at the
 * second place.
 *
 * The check is over the sources rather than over a mounted filter, because what
 * it guards is the shape of the file the next list will be copied from: a
 * module that declares the whole action set again is that copy starting. Lists
 * with a shape of their own (the pulse filter and its rating range, the games
 * filter and its hosts) declare a different set and are none of this rule.
 *
 * The filter itself is read through the address, on a list of its own built
 * here: what an address means, what each action writes into it, and that an
 * address the filter wrote reads back as itself. The topics and the comments
 * filters only name their sorts, so what holds for this
 * list holds for both.
 */
import { describe, it, expect, afterEach } from "vitest";
import { existsSync, readdirSync, readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { createAuthorDateFilter } from "./createAuthorDateFilter";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";

const HERE = dirname(fileURLToPath(import.meta.url));
// shared/lib/filters -> src
const FEATURES = join(resolve(HERE, "../../.."), "features");

/** The action set the shared filter owns, in full. */
const SHARED_ACTIONS = [
  "SET_SEARCH",
  "ADD_AUTHOR",
  "REMOVE_AUTHOR",
  "CLEAR_AUTHORS",
  "SET_DATE_RANGE",
  "SET_SORT",
  "TOGGLE_SORT_ORDER",
  "CLEAR_FILTERS",
];

/** Every model source of every filter feature. */
function filterModules(): { name: string; source: string }[] {
  const modules: { name: string; source: string }[] = [];
  for (const feature of readdirSync(FEATURES)) {
    const dir = join(FEATURES, feature, "model");
    if (!existsSync(dir)) continue;
    for (const file of readdirSync(dir)) {
      if (!file.endsWith(".ts") || file.endsWith(".spec.ts")) continue;
      modules.push({
        name: `${feature}/model/${file}`,
        source: readFileSync(join(dir, file), "utf8"),
      });
    }
  }
  return modules;
}

describe("the author/date filter scaffold", () => {
  const modules = filterModules();

  it("has filter modules to read at all", () => {
    // Without this the two checks below would pass by seeing nothing.
    expect(modules.length).toBeGreaterThan(0);
  });

  it("is what every list of that shape is built from", () => {
    const offenders = modules
      .filter(({ source }) =>
        SHARED_ACTIONS.every((action) => source.includes(`"${action}"`)),
      )
      .filter(({ source }) => !source.includes("createAuthorDateFilter"))
      .map(({ name }) => name);

    expect(
      offenders,
      "a second copy of the shared reducer and query parser: declare the sort options and the defaults and call createAuthorDateFilter",
    ).toEqual([]);
  });

  it("is used by more than one list", () => {
    // The check above passes for a module that declares no actions at all, so
    // the factory has to be seen in use, or the rule is measuring silence.
    const built = modules
      .filter(({ source }) => source.includes("createAuthorDateFilter"))
      .map(({ name }) => name);

    expect(built.length).toBeGreaterThan(1);
  });
});

/** A list of that shape, with sorts and defaults of its own. */
const useProbeFilter = createAuthorDateFilter<"lastActivity" | "title">({
  name: "useProbeFilter",
  sortOptions: [
    { value: "lastActivity", label: "Активность", defaultDirection: "desc" },
    { value: "title", label: "Заголовок", defaultDirection: "asc" },
  ],
  defaultSortBy: "lastActivity",
  defaultSortOrder: "desc",
});

type Filter = ReturnType<typeof useProbeFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(useProbeFilter, query);
  return page;
}

const DEFAULT_STATE = {
  search: "",
  authors: [],
  createdFromUtc: null,
  createdToUtc: null,
  sortBy: "lastActivity",
  sortOrder: "desc",
};

const FULL = {
  search: "анна",
  authors: "анна,борис",
  createdFromUtc: "2024-01-01",
  createdToUtc: "2024-01-31",
  sortBy: "title",
  sortOrder: "asc",
};

describe("createAuthorDateFilter: what an address means", () => {
  it("reads an empty address as no filter and the default sort", async () => {
    const { filter } = await on({});

    expect(plain(filter.filterState.value)).toEqual(DEFAULT_STATE);
    expect(filter.searchParams.value).toEqual({
      sortBy: "lastActivity",
      sortOrder: "desc",
      size: DEFAULT_PAGE_SIZE,
    });
    expect(filter.hasActiveFilters.value).toBe(false);
  });

  it("reads every part of a full address", async () => {
    const { filter } = await on({ ...FULL, number: "3" });

    expect(plain(filter.filterState.value)).toEqual({
      search: "анна",
      authors: ["анна", "борис"],
      createdFromUtc: "2024-01-01",
      createdToUtc: "2024-01-31",
      sortBy: "title",
      sortOrder: "asc",
    });
    expect(filter.searchParams.value).toEqual({
      search: "анна",
      authors: ["анна", "борис"],
      createdFromUtc: "2024-01-01T00:00:00Z",
      createdToUtc: "2024-01-31T23:59:59Z",
      sortBy: "title",
      sortOrder: "asc",
      number: 3,
      size: DEFAULT_PAGE_SIZE,
    });
    expect(filter.hasActiveFilters.value).toBe(true);
  });

  it("asks for the created range as whole days and the page size of its list", async () => {
    const { filter } = await on({
      createdFromUtc: "2024-03-01",
      createdToUtc: "2024-03-01",
    });

    expect(filter.searchParams.value).toEqual({
      createdFromUtc: "2024-03-01T00:00:00Z",
      createdToUtc: "2024-03-01T23:59:59Z",
      sortBy: "lastActivity",
      sortOrder: "desc",
      size: DEFAULT_PAGE_SIZE,
    });
  });

  it("drops what it cannot read and keeps the rest", async () => {
    const { filter } = await on({
      search: "я".repeat(250),
      authors: " анна , ,ВесьмаДлинноеИмяАвтора",
      createdFromUtc: "yesterday",
      createdToUtc: "2024-13-45",
      sortBy: "bogus",
      sortOrder: "up",
    });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      search: "я".repeat(200),
      authors: ["анна", "ВесьмаДлинноеИмяАвто"],
    });
  });

  it.each<[string, Record<string, string>, boolean]>([
    ["an author alone", { authors: "анна" }, true],
    ["a created range alone", { createdFromUtc: "2024-01-01" }, true],
    ["a sort alone", { sortBy: "title", sortOrder: "asc" }, false],
  ])("counts %s as a filter: %s", async (_what, query, counted) => {
    const { filter } = await on(query);

    expect(filter.hasActiveFilters.value).toBe(counted);
  });
});

describe("createAuthorDateFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    ["a search", {}, (f) => f.setSearch("борис"), { search: "борис" }],
    [
      "an author added",
      { authors: "анна" },
      (f) => f.addAuthor("борис"),
      { authors: "анна,борис" },
    ],
    [
      "an author removed",
      { authors: "анна,борис" },
      (f) => f.removeAuthor("анна"),
      { authors: "борис" },
    ],
    [
      "the authors cleared",
      { authors: "анна,борис", search: "x" },
      (f) => f.clearAuthors(),
      { search: "x" },
    ],
    [
      "a created range",
      {},
      (f) => f.setDateRange("2024-01-01", null),
      { createdFromUtc: "2024-01-01" },
    ],
    [
      "a sort in the direction of its own",
      {},
      (f) => f.setSort("title"),
      { sortBy: "title", sortOrder: "asc" },
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

describe("createAuthorDateFilter: an address written reads back as itself", () => {
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
      { authors: " анна ,", sortBy: "bogus", sortOrder: "desc" },
      { authors: "анна" },
    ],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
