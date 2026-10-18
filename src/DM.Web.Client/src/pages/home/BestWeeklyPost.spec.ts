/**
 * @vitest-environment jsdom
 */

/**
 * The two rated posts of the home page ("Лучший пост недели", "Последний
 * оцененный пост") ask no game anything about the post, as the feed does not.
 * They used to ask whether the game's blacklist held the viewer; the list
 * closes no rating any more, and this pins that both blocks hand the post as
 * it came and ask nothing more.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h, type Component } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  gameApi,
  GameParticipation,
  useRatedPostsStore,
} from "@/entities/game";
import type { Post } from "@/entities/game";
import { useAuthStore } from "@/shared/stores";
import BestWeeklyPost from "./BestWeeklyPost.vue";
import LatestRatedPost from "./LatestRatedPost.vue";

/** The post down to what the block hands it. */
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

describe("главная: оцененные посты", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setActivePinia(createPinia());
    useAuthStore().updateUser({ id: "u-me", username: "Читатель" } as never);
  });

  it.each<[string, Component, "bestOfWeek" | "latestRated"]>([
    ["лучший пост недели", BestWeeklyPost, "bestOfWeek"],
    ["последний оцененный пост", LatestRatedPost, "latestRated"],
  ])(
    "%s отдает пост как пришел и не спрашивает игру сам",
    async (_block, block, field) => {
      const getGame = vi.spyOn(gameApi, "getGame");
      const store = useRatedPostsStore();
      // Both posts are already loaded: nothing is fetched on mount.
      store.$patch({
        bestLoaded: true,
        latestLoaded: true,
        [field]: aPost("p-1", "g-listed"),
      });
      vi.spyOn(store, "fetchBestOfWeek").mockResolvedValue();
      vi.spyOn(store, "fetchLatestRated").mockResolvedValue();

      const wrapper = mount(block, {
        global: { stubs: { GamePost: GamePostStub } },
      });
      await flushPromises();

      expect(wrapper.get(".post-stub").attributes("data-id")).toBe("p-1");
      expect(getGame).not.toHaveBeenCalled();
    },
  );
});
