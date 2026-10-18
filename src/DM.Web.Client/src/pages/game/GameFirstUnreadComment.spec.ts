/**
 * @vitest-environment jsdom
 */

/**
 * Where the counter of a game's comments leads: the page of the discussion
 * that holds the first comment the reader has not read, counted in the
 * reader's own page size, with the comment's permalink; the discussion itself
 * where nothing is unread or the request fails.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { gameApi } from "@/entities/game";
import GameFirstUnreadComment from "./GameFirstUnreadComment.vue";

const replace = vi.hoisted(() => vi.fn());

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "g-1" }, query: {} }),
  useRouter: () => ({ replace }),
}));

/** Where the screen sends the reader for this answer of the server. */
async function landing(
  answer: () => Promise<unknown>,
  commentsPerPage?: number,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (commentsPerPage)
    useAuthStore().user = {
      settings: { paging: { commentsPerPage } },
    } as never;
  const asked = vi
    .spyOn(gameApi, "getFirstUnreadComment")
    .mockImplementation(answer as never);
  mount(GameFirstUnreadComment, { global: { plugins: [pinia] } });
  await flushPromises();
  expect(asked).toHaveBeenCalledWith("g-1");
  return replace.mock.calls[0][0];
}

const unreadAt = (commentNumber: number) => async () => ({
  data: { resource: { hasUnread: true, commentId: "c-9", commentNumber } },
  error: null,
});

const DISCUSSION = { name: "game-comments", params: { id: "g-1" } };

describe("GameFirstUnreadComment", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    replace.mockReset();
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

  it("opens the discussion itself when the request threw", async () => {
    const target = await landing(async () => {
      throw new Error("Network Error");
    });

    expect(target).toEqual(DISCUSSION);
  });
});
