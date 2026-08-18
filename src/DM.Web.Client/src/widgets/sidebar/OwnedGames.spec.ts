/**
 * @vitest-environment jsdom
 */

/**
 * "Мои игры" is the panel a player watches, and it was the one panel that never
 * said which of his games was waiting for him: the mentor's list of curated
 * games has carried the star from the start, the room rows carry it, and the
 * player's own list carried nothing. The marker is the server's answer now
 * (GameRef.awaitsViewerTurn), so the panel's whole job is to draw it on the
 * right row and on no other.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import OwnedGames from "./OwnedGames.vue";
import {
  GameParticipation,
  useGamesStore,
  type GameRef,
} from "@/entities/game";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: {
    template: "<span class='tooltip'><slot /></span>",
    props: ["text"],
  },
};

const game = (id: string, title: string, awaiting: string[] | null): GameRef =>
  ({
    id,
    publicId: id,
    title,
    status: "Active",
    master: { id: "u-1", username: "master" },
    assistants: [],
    participation: [GameParticipation.Player],
    subscribersCount: 0,
    recruitment: { isOpen: false, pcCount: 0, isSubsequent: false },
    unreadPostsCount: 0,
    unreadCommentsCount: 0,
    gameReviewsCount: 0,
    postReviewsCount: 0,
    awaitsViewerTurn: awaiting !== null,
    awaitedCharacterNames: awaiting ?? [],
  }) as unknown as GameRef;

function mountPanel(games: GameRef[]) {
  const store = useGamesStore();
  // Written straight into the store: the panel fetches only for a signed-in
  // viewer, and this test is about what it draws, not about how it loads.
  store.participatingGames = games;
  return mount(OwnedGames, { global: { stubs } });
}

describe("OwnedGames", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("stars only the game that awaits the viewer", () => {
    const wrapper = mountPanel([
      game("g-1", "Ждущая игра", ["Гром"]),
      game("g-2", "Спокойная игра", null),
    ]);

    const starred = wrapper
      .findAll("li.link")
      .filter((row) => row.find(".star").exists());

    expect(starred).toHaveLength(1);
    expect(starred[0].text()).toContain("Ждущая игра");
  });

  it("draws no star when nothing awaits the viewer", () => {
    const wrapper = mountPanel([game("g-2", "Спокойная игра", null)]);

    expect(wrapper.find(".star").exists()).toBe(false);
  });
});
