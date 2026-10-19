/**
 * @vitest-environment jsdom
 */

/** "Мои игры" stars the game that waits for the viewer (GameRef.awaitsViewerTurn), and no other. */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import OwnedGames from "./OwnedGames.vue";
import {
  GameParticipation,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";

const stubs = {
  RouterLink: RouterLinkStub,
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
  // The block is the signed-in viewer's.
  useAuthStore().user = { username: "viewer" } as never;
  // Straight into the store, the read answered by nothing.
  vi.spyOn(store, "fetchParticipatingGames").mockResolvedValue(
    undefined as never,
  );
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
