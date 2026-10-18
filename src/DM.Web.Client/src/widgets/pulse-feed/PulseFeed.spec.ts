/**
 * @vitest-environment jsdom
 */

/**
 * The feed asks no game anything about its posts.
 *
 * The posts of the feed come from many games. The feed used to ask the
 * details of every game on the page whether the game's blacklist held the
 * viewer, a request per game out of the budget every reader behind an address
 * shares. The list closes no rating any more, and this pins that the feed
 * hands each post as it came and asks nothing more.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { gameApi, GameParticipation } from "@/entities/game";
import type { Post } from "@/entities/game";
import { useAuthStore } from "@/shared/stores";
import PulseFeed from "./PulseFeed.vue";

vi.mock("@/features/pulse-filter", async () => {
  const { ref, defineComponent, h } = await import("vue");
  return {
    usePulseFilter: () => ({
      filterState: ref({ search: "" }),
      searchParams: ref({}),
      hasActiveFilters: ref(false),
    }),
    PulseFilter: defineComponent({
      name: "PulseFilter",
      setup: () => () => h("div"),
    }),
  };
});

/** The post down to what the feed hands it. */
const GamePostStub = defineComponent({
  name: "GamePost",
  props: {
    post: { type: Object, required: true },
  },
  setup(props) {
    const post = props.post as Post;
    return () =>
      h("article", {
        class: "post-stub",
        "data-id": post.id,
      });
  },
});

/** A post of a list, naming its room and game as the server does. */
const aPost = (id: string, gameId: string): Post =>
  ({
    id,
    author: { id: "u-author", username: "Автор" },
    createdUtc: "2026-09-01T10:00:00Z",
    gameText: "Текст",
    room: {
      id: `r-${gameId}`,
      roomNumber: 1,
      title: "Комната",
      unreadPostsCount: 0,
      game: {
        id: gameId,
        publicId: gameId,
        title: gameId,
        participation: [GameParticipation.Reader],
      },
    },
  }) as unknown as Post;

describe("PulseFeed", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setActivePinia(createPinia());
    useAuthStore().updateUser({ id: "u-me", username: "Читатель" } as never);
  });

  it("отдает посты как пришли и не спрашивает игры сама", async () => {
    vi.spyOn(gameApi, "getRatedPosts").mockResolvedValue({
      data: {
        resources: [aPost("p-1", "g-listed"), aPost("p-2", "g-open")],
        paging: null,
      },
      error: null,
    } as never);
    const getGame = vi.spyOn(gameApi, "getGame");

    const wrapper = mount(PulseFeed, {
      global: { stubs: { GamePost: GamePostStub } },
    });
    await flushPromises();

    const ids = wrapper
      .findAll(".post-stub")
      .map((p) => p.attributes("data-id"));
    expect(ids).toEqual(["p-1", "p-2"]);
    expect(getGame).not.toHaveBeenCalled();
  });
});
