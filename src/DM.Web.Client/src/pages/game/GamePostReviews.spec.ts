/**
 * @vitest-environment jsdom
 */

import { describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore, type Game } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import GamePostReviews from "./GamePostReviews.vue";

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

  return mount(GamePostReviews, {
    global: { plugins: [pinia] },
    shallow: true,
  });
}

describe("GamePostReviews", () => {
  it("draws the shared rated-posts list, scoped to this game", () => {
    const list = render().findComponent(RatedPostsList);

    expect(list.exists()).toBe(true);
    // The guid, not the public id the route carries.
    expect(list.props("scope")).toEqual({
      kind: "game",
      gameId: "the-game-guid",
    });
  });
});
