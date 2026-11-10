/**
 * @vitest-environment jsdom
 */

// Where the counter of a game's comments leads.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { discussionApi } from "@/entities/game";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GameFirstUnreadComment from "./GameFirstUnreadComment.vue";

/** The router of the screen last opened. */
let router: Router;

/** Where the screen sends the reader for this answer of the server. */
async function landing(answer: () => Promise<unknown>, pageSize?: number) {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (pageSize)
    useAuthStore().user = {
      settings: { pageSize },
    } as never;
  const asked = vi
    .spyOn(discussionApi, "getFirstUnreadComment")
    .mockImplementation(answer as never);
  ({ router } = await mountPage(
    GameFirstUnreadComment,
    "/game/g-1/comments/unread",
    {
      routes: [
        ["game-first-unread-comment", "/game/:id/comments/unread"],
        ["game-comments", "/game/:id/comments"],
      ],
      pinia,
    },
  ));
  await flushPromises();
  expect(asked).toHaveBeenCalledWith("g-1");
  const { name, params, query, hash } = router.currentRoute.value;
  return { name, params, query, hash };
}

const unreadAt = (commentNumber: number) => async () => ({
  data: { resource: { hasUnread: true, commentId: "c-9", commentNumber } },
  error: null,
});

const DISCUSSION = {
  name: "game-comments",
  params: { id: "g-1" },
  query: {},
  hash: "",
};

describe("GameFirstUnreadComment", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it.each<[number, number | undefined, object]>([
    [25, 10, { number: "3" }],
    [20, 10, { number: "2" }],
    [10, 10, {}],
    [41, undefined, { number: "3" }],
  ])(
    "lands on comment %i, %s to a page, with the page %j",
    async (commentNumber, perPage, query) => {
      const target = await landing(unreadAt(commentNumber), perPage);

      expect(target).toEqual({ ...DISCUSSION, query, hash: "#comment-c-9" });
    },
  );

  // With the jump in the history, Back would land on it and it would jump again.
  it("takes the place of the jump, so Back does not land on it", async () => {
    await landing(unreadAt(25), 10);

    router.back();
    await flushPromises();

    expect(router.currentRoute.value.name).toBe("game-comments");
  });

  it("opens the discussion itself where nothing is unread", async () => {
    const target = await landing(async () => ({
      data: { resource: { hasUnread: false } },
      error: null,
    }));

    expect(target).toEqual(DISCUSSION);
  });

  it("opens the discussion itself when the request failed", async () => {
    const target = await landing(async () => ({
      data: null,
      error: { status: 500, title: "" },
    }));

    expect(target).toEqual(DISCUSSION);
  });
});
