/**
 * @vitest-environment jsdom
 */

/**
 * The per-game "Оцененные посты" used to be a list of its own: one fixed page
 * of a hundred posts, no paging, and a room filter computed on the client over
 * that truncated hundred — an answer about the posts that happened to be
 * fetched rather than about the game. The same list is drawn on /pulse and on
 * both profile subpages with the shared filter and server-side paging, so the
 * page draws that one now and says only which posts it is about.
 *
 * The breadcrumb over a post names the room alone here: the game is the page,
 * and repeating its title above every post of its own sub-page says nothing.
 */
import { describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore, type Game } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import GamePostReviews from "./GamePostReviews.vue";

// Only the route param is needed; the rest of the router stays real so the
// components that import it from the same module keep working.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" }, query: {} }),
}));

function render() {
  const pinia = createPinia();
  setActivePinia(pinia);
  useGameDetailsStore().game = {
    id: "the-game-guid",
    publicId: "the-game",
  } as unknown as Game;

  // Shallow: the list is a unit of its own with its own spec surface; what
  // this page owes it is the scope, the paging target and the breadcrumb.
  return mount(GamePostReviews, {
    global: { plugins: [pinia] },
    shallow: true,
  });
}

describe("GamePostReviews", () => {
  it("draws the shared rated-posts list, scoped to this game", () => {
    const list = render().findComponent(RatedPostsList);

    expect(list.exists()).toBe(true);
    // The guid, not the five-letter public id the route carries: the posts
    // endpoint takes the game's identifier.
    expect(list.props("scope")).toEqual({
      kind: "game",
      gameId: "the-game-guid",
    });
  });

  it("points the paging back at the page it stands on", () => {
    expect(render().findComponent(RatedPostsList).props("pagingTo")).toEqual({
      name: "game-post-reviews",
      params: { id: "the-game" },
    });
  });

  it("keeps the game out of the breadcrumb of its own posts", () => {
    expect(
      render().findComponent(RatedPostsList).props("navigationLevel"),
    ).toBe("room");
  });
});
