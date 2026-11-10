/**
 * @vitest-environment jsdom
 */

/**
 * Where the counter of a topic's comments leads: the page of the topic that
 * holds the first comment the reader has not read, counted in the reader's
 * own page size, with the comment's permalink; the topic itself where there is
 * nothing to land on.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { forumApi } from "@/entities/forum";
import { mountPage } from "@/shared/lib/testing/mountPage";
import TopicUnread from "./TopicUnread.vue";

enableAutoUnmount(afterEach);

/** The router of the screen last opened. */
let router: Router;

/** Where the screen sends the reader for this answer of the server. */
async function landing(answer: unknown, pageSize?: number) {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (pageSize)
    useAuthStore().user = {
      settings: { pageSize },
    } as never;
  const asked = vi
    .spyOn(forumApi, "getFirstUnreadComment")
    .mockResolvedValue(answer as never);
  ({ router } = await mountPage(TopicUnread, "/forum/general/42/unread", {
    routes: [
      ["topic-unread", "/forum/:alias/:num/unread"],
      ["topic", "/forum/:alias/:num"],
    ],
    pinia,
  }));
  await flushPromises();
  expect(asked).toHaveBeenCalledWith("general", 42);
  const { name, params, query, hash } = router.currentRoute.value;
  return { name, params, query, hash };
}

const at = (commentNumber: number) => ({
  data: { resource: { commentId: "c-7", commentNumber } },
  error: null,
});

// Params come back from the address as text.
const TOPIC = {
  name: "topic",
  params: { alias: "general", num: "42" },
  query: {},
  hash: "",
};

describe("TopicUnread", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it.each<[number, number | undefined, object]>([
    [25, 10, { number: "3" }],
    [20, 10, { number: "2" }],
    [10, 10, {}],
    [21, undefined, { number: "2" }],
    [1, undefined, {}],
  ])(
    "lands on comment %i, %s to a page, with the page %j",
    async (commentNumber, perPage, query) => {
      const target = await landing(at(commentNumber), perPage);

      expect(target).toEqual({ ...TOPIC, query, hash: "#comment-c-7" });
    },
  );

  // With the jump in the history, Back would land on it and it would jump again.
  it("takes the place of the jump, so Back does not land on it", async () => {
    await landing(at(25), 10);

    router.back();
    await flushPromises();

    expect(router.currentRoute.value.name).toBe("topic");
  });

  it("opens the topic itself where there is no comment to land on", async () => {
    const target = await landing({ data: { resource: {} }, error: null });

    expect(target).toEqual(TOPIC);
  });

  it("opens the topic itself when the request failed", async () => {
    const target = await landing({
      data: null,
      error: { status: 500, title: "" },
    });

    expect(target).toEqual(TOPIC);
  });
});
