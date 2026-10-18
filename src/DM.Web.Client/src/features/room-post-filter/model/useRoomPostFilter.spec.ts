/**
 * @vitest-environment jsdom
 */

/**
 * The room filter lives in the address, and the room list is asked what the
 * address says. These pin the translation both ways: every filter and sort of
 * the bar reaches the request under the name the API takes, the room itself
 * asks with nothing at all (so its request, and the cheap numbering the server
 * gives it, stay what they were), and "Сбросить" brings the address back to
 * the bare room.
 */
import { describe, it, expect } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { createRouter, createMemoryHistory } from "vue-router";
import {
  buildApiFilter,
  buildQueryFromState,
  parseQueryToState,
  useRoomPostFilter,
  type RoomPostFilterComposable,
} from "./useRoomPostFilter";

const apiFor = (query: Record<string, string>) =>
  buildApiFilter(parseQueryToState(query));

describe("room filter request", () => {
  it("asks the bare room with nothing", () => {
    expect(apiFor({})).toEqual({});
  });

  it("sends the search, the authors and the rating range", () => {
    expect(
      apiFor({
        q: "при",
        authors: "SolohinLex,TestMentor",
        minRating: "-2",
        maxRating: "5",
      }),
    ).toEqual({
      search: "при",
      authorUsernames: ["SolohinLex", "TestMentor"],
      minRating: -2,
      maxRating: 5,
    });
  });

  it("sends the characters as ids and the posts without one as a flag", () => {
    expect(apiFor({ characters: "c-1,none,c-2" })).toEqual({
      characterIds: ["c-1", "c-2"],
      withoutCharacter: true,
    });
    expect(apiFor({ characters: "none" })).toEqual({ withoutCharacter: true });
  });

  it("sends the reviews choice both ways", () => {
    expect(apiFor({ reviews: "with" })).toEqual({ hasReviews: true });
    expect(apiFor({ reviews: "without" })).toEqual({ hasReviews: false });
  });

  it("widens the creation dates to the whole days", () => {
    expect(
      apiFor({ createdFrom: "2026-02-01", createdTo: "2026-02-28" }),
    ).toEqual({
      createdFromUtc: "2026-02-01T00:00:00Z",
      createdToUtc: "2026-02-28T23:59:59Z",
    });
  });

  it("sends a sort only when it is not the room's own order", () => {
    expect(apiFor({ sort: "created" })).toEqual({});
    expect(apiFor({ sort: "rating", order: "desc" })).toEqual({
      sortBy: "rating",
      sortOrder: "desc",
    });
    expect(apiFor({ order: "desc" })).toEqual({
      sortBy: "created",
      sortOrder: "desc",
    });
    expect(apiFor({ sort: "lastreview", order: "asc" })).toEqual({
      sortBy: "lastreview",
      sortOrder: "asc",
    });
  });

  it("ignores what the address cannot mean", () => {
    expect(
      apiFor({
        sort: "likes",
        order: "sideways",
        reviews: "maybe",
        minRating: "много",
        createdFrom: "вчера",
      }),
    ).toEqual({});
  });

  it("writes back only what differs from the room itself", () => {
    const query = {
      q: "при",
      sort: "reviewcount",
      order: "desc",
      authors: "SolohinLex",
      characters: "c-1,none",
      minRating: "-1",
      reviews: "without",
      createdTo: "2026-02-28",
    };
    expect(buildQueryFromState(parseQueryToState(query))).toEqual(query);
    expect(buildQueryFromState(parseQueryToState({ sort: "created" }))).toEqual(
      {},
    );
  });
});

describe("room filter in the address", () => {
  async function mountFilter(query: Record<string, string>) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: "/room", name: "room", component: { render: () => null } },
      ],
    });
    await router.push({ path: "/room", query });
    await router.isReady();

    let filter!: RoomPostFilterComposable;
    const Host = defineComponent({
      setup() {
        filter = useRoomPostFilter();
        return () => h("div");
      },
    });
    mount(Host, { global: { plugins: [router] } });
    return { router, filter };
  }

  // The dispatcher batches rapid changes into one navigation after a short
  // debounce.
  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve, 80));
    await flushPromises();
  };

  it("picks a sort in its own direction", async () => {
    const { router, filter } = await mountFilter({});
    filter.setSort("rating");
    await settle();
    expect(router.currentRoute.value.query).toEqual({
      sort: "rating",
      order: "desc",
    });

    filter.setSort("created");
    await settle();
    expect(router.currentRoute.value.query).toEqual({});
  });

  it("tells a filtered view from a re-sorted one and from the room", async () => {
    const sorted = await mountFilter({ sort: "rating", order: "desc" });
    expect(sorted.filter.isFilteredView.value).toBe(true);
    expect(sorted.filter.hasActiveFilters.value).toBe(false);

    const filtered = await mountFilter({ reviews: "with" });
    expect(filtered.filter.isFilteredView.value).toBe(true);
    expect(filtered.filter.hasActiveFilters.value).toBe(true);

    const bare = await mountFilter({ number: "3" });
    expect(bare.filter.isFilteredView.value).toBe(false);
  });

  it("drops every filter, the sort and the page on reset", async () => {
    const { router, filter } = await mountFilter({
      q: "при",
      characters: "c-1",
      sort: "lastreview",
      order: "desc",
      number: "2",
    });
    filter.clearFilters();
    await settle();
    expect(router.currentRoute.value.query).toEqual({});
  });

  it("starts a changed filter from its first page", async () => {
    const { router, filter } = await mountFilter({ q: "при", number: "3" });
    filter.addCharacter("none");
    await settle();
    expect(router.currentRoute.value.query).toEqual({
      q: "при",
      characters: "none",
    });
  });
});
