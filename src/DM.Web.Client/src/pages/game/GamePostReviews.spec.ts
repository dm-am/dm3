/**
 * @vitest-environment jsdom
 */

import { describe, expect, it } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { RatedPostsList } from "@/widgets/rated-posts";
import GamePostReviews from "./GamePostReviews.vue";

async function render() {
  const pinia = createPinia();
  setActivePinia(pinia);
  useGameDetailsStore().game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
  };

  const { wrapper } = await mountPage(
    GamePostReviews,
    "/game/the-game/post-reviews",
    {
      routes: [["game-post-reviews", "/game/:id/post-reviews"]],
      pinia,
      stubs: { RatedPostsList: true },
    },
  );
  return wrapper;
}

describe("GamePostReviews", () => {
  it("draws the shared rated-posts list, scoped to this game", async () => {
    const list = (await render()).findComponent(RatedPostsList);

    expect(list.exists()).toBe(true);
    // The guid, not the public id the route carries.
    expect(list.props("scope")).toEqual({
      kind: "game",
      gameId: "the-game-guid",
    });
    // Its pages go by the address the reader came by.
    expect(list.props("pagingTo")).toEqual({
      name: "game-post-reviews",
      params: { id: "the-game" },
    });
  });
});
