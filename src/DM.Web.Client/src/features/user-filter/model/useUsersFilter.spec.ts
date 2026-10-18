/**
 * @vitest-environment jsdom
 */

/**
 * The users filter read through the address: what an address means, what each
 * action writes into it, and that an address the filter wrote reads back as
 * itself.
 */
import { describe, it, expect, afterEach } from "vitest";
import {
  filterOnListPage,
  plain,
  type Page,
} from "@/shared/lib/testing/urlFilter";
import { UserRole } from "@/shared/api/models/common/user";
import { useUsersFilter } from "./useUsersFilter";

type Filter = ReturnType<typeof useUsersFilter>;
let page: (Page & { filter: Filter }) | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
});

async function on(query: Record<string, string>) {
  page = await filterOnListPage(useUsersFilter, query);
  return page;
}

const DEFAULT_STATE = {
  search: "",
  activity: "all",
  onlineFilter: "all",
  role: "all",
  experience: "all",
  ratingMin: null,
  ratingMax: null,
  gamesHostingMin: null,
  gamesHostingMax: null,
  gamesPlayingMin: null,
  gamesPlayingMax: null,
  blogsHostingMin: null,
  blogsHostingMax: null,
  registeredFromUtc: null,
  registeredToUtc: null,
  sortBy: "lastActivity",
  sortOrder: "desc",
};

const FULL = {
  search: "анна",
  activity: "active",
  online: "online",
  role: "Mentor",
  experience: "newbie",
  ratingMin: "10",
  ratingMax: "90",
  gamesHostingMin: "1",
  gamesHostingMax: "5",
  gamesPlayingMin: "2",
  gamesPlayingMax: "6",
  blogsHostingMin: "0",
  blogsHostingMax: "3",
  registeredFromUtc: "2020-01-01",
  registeredToUtc: "2021-01-01",
  sortBy: "username",
  sortOrder: "asc",
};

describe("useUsersFilter: what an address means", () => {
  it("reads an empty address as everyone, the latest active first", async () => {
    const { filter } = await on({});

    expect(plain(filter.filterState.value)).toEqual(DEFAULT_STATE);
    expect(filter.searchParams.value).toEqual({
      activity: "all",
      sortBy: "lastActivity",
      sortOrder: "desc",
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(false);
  });

  it("reads every part of a full address", async () => {
    const { filter } = await on({ ...FULL, number: "5" });

    expect(plain(filter.filterState.value)).toEqual({
      search: "анна",
      activity: "active",
      onlineFilter: "online",
      role: "Mentor",
      experience: "newbie",
      ratingMin: 10,
      ratingMax: 90,
      gamesHostingMin: 1,
      gamesHostingMax: 5,
      gamesPlayingMin: 2,
      gamesPlayingMax: 6,
      blogsHostingMin: 0,
      blogsHostingMax: 3,
      registeredFromUtc: "2020-01-01",
      registeredToUtc: "2021-01-01",
      sortBy: "username",
      sortOrder: "asc",
    });
    expect(filter.searchParams.value).toEqual({
      search: "анна",
      activity: "active",
      isOnline: true,
      role: UserRole.Mentor,
      isNewbie: true,
      minRating: 10,
      maxRating: 90,
      minGamesHosting: 1,
      maxGamesHosting: 5,
      minGamesPlaying: 2,
      maxGamesPlaying: 6,
      minBlogsHosting: 0,
      maxBlogsHosting: 3,
      registeredFromUtc: "2020-01-01T00:00:00Z",
      registeredToUtc: "2021-01-01T23:59:59Z",
      sortBy: "username",
      sortOrder: "asc",
      number: 5,
      size: 20,
    });
    expect(filter.hasActiveFilters.value).toBe(true);
  });

  it("asks for the experienced as not newbies", async () => {
    const { filter } = await on({ experience: "experienced" });

    expect(filter.searchParams.value.isNewbie).toBe(false);
  });

  it("asks for the online only among the active", async () => {
    const { filter } = await on({ activity: "inactive", online: "online" });

    expect(filter.filterState.value.onlineFilter).toBe("online");
    expect(filter.searchParams.value).not.toHaveProperty("isOnline");
  });

  it("drops what it cannot read and keeps the rest", async () => {
    const { filter } = await on({
      activity: "sleeping",
      online: "maybe",
      role: "Overlord",
      experience: "old",
      ratingMin: "abc",
      ratingMax: "12.7",
      registeredFromUtc: "yesterday",
      sortBy: "bogus",
      sortOrder: "up",
    });

    expect(plain(filter.filterState.value)).toEqual({
      ...DEFAULT_STATE,
      ratingMax: 12,
    });
  });

  it.each<[string, Record<string, string>, boolean]>([
    ["a role alone", { role: "Admin" }, true],
    ["the online alone", { online: "online" }, true],
    ["a rating floor alone", { ratingMin: "0" }, true],
    ["a sort alone", { sortBy: "username", sortOrder: "asc" }, false],
  ])("counts %s as a filter: %s", async (_what, query, counted) => {
    const { filter } = await on(query);

    expect(filter.hasActiveFilters.value).toBe(counted);
  });
});

describe("useUsersFilter: what an action writes", () => {
  it.each<[string, Record<string, string>, (f: Filter) => void, object]>([
    ["a search", {}, (f) => f.setSearch("борис"), { search: "борис" }],
    ["the active", {}, (f) => f.setActivity("active"), { activity: "active" }],
    [
      "the online among the active",
      { activity: "active" },
      (f) => f.setOnlineFilter("online"),
      { activity: "active", online: "online" },
    ],
    [
      "another activity, dropping the online",
      { activity: "active", online: "online" },
      (f) => f.setActivity("inactive"),
      { activity: "inactive" },
    ],
    [
      "another activity and back in one go, the online gone",
      { activity: "active", online: "online" },
      (f) => {
        f.setActivity("inactive");
        f.setActivity("active");
      },
      { activity: "active" },
    ],
    [
      "the online without the active, which the address does not keep",
      {},
      (f) => f.setOnlineFilter("online"),
      {},
    ],
    ["a role", {}, (f) => f.setRole(UserRole.Moderator), { role: "Moderator" }],
    ["every role again", { role: "Admin" }, (f) => f.setRole("all"), {}],
    [
      "an experience",
      {},
      (f) => f.setExperience("experienced"),
      { experience: "experienced" },
    ],
    [
      "each range of numbers",
      {},
      (f) => {
        f.setRatingRange(0, null);
        f.setGamesHostingRange(null, 4);
        f.setGamesPlayingRange(1, 2);
        f.setBlogsHostingRange(3, 3);
      },
      {
        ratingMin: "0",
        gamesHostingMax: "4",
        gamesPlayingMin: "1",
        gamesPlayingMax: "2",
        blogsHostingMin: "3",
        blogsHostingMax: "3",
      },
    ],
    [
      "a range of registration",
      {},
      (f) => f.setRegisteredRange("2020-01-01", null),
      { registeredFromUtc: "2020-01-01" },
    ],
    [
      "a sort in the direction of its own",
      {},
      (f) => f.setSort("rating"),
      { sortBy: "rating" },
    ],
    [
      "a sort in a direction given",
      {},
      (f) => f.setSort("rating", "asc"),
      { sortBy: "rating", sortOrder: "asc" },
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

describe("useUsersFilter: an address written reads back as itself", () => {
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
      { role: "Overlord", ratingMax: "12.7", sortOrder: "desc" },
      { ratingMax: "12" },
    ],
    [
      "one with the online and no activity",
      { online: "online", activity: "all" },
      {},
    ],
  ])("%s", async (_name, query, canonical) => {
    const first = await rewritten(query);
    expect(first).toEqual(canonical);

    expect(await rewritten(first)).toEqual(canonical);
  });
});
