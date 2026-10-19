/**
 * @vitest-environment jsdom
 */

// Where the counter of a game's comments leads.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { discussionApi } from "@/entities/game";
import GameFirstUnreadComment from "./GameFirstUnreadComment.vue";

const replace = vi.hoisted(() => vi.fn());

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "g-1" }, query: {} }),
  useRouter: () => ({ replace }),
}));

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
});
