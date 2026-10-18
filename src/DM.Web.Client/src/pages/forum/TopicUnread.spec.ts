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
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { forumApi } from "@/entities/forum";
import TopicUnread from "./TopicUnread.vue";

const replace = vi.hoisted(() => vi.fn());

enableAutoUnmount(afterEach);

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { alias: "general", num: "42" }, query: {} }),
  useRouter: () => ({ replace }),
}));

/** Where the screen sends the reader for this answer of the server. */
async function landing(answer: unknown, commentsPerPage?: number) {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (commentsPerPage)
    useAuthStore().user = {
      settings: { paging: { commentsPerPage } },
    } as never;
  const asked = vi
    .spyOn(forumApi, "getFirstUnreadComment")
    .mockResolvedValue(answer as never);
  mount(TopicUnread, { global: { plugins: [pinia] } });
  await flushPromises();
  expect(asked).toHaveBeenCalledWith("general", 42);
  return replace.mock.calls[0][0];
}

const at = (commentNumber: number) => ({
  data: { resource: { commentId: "c-7", commentNumber } },
  error: null,
});

const TOPIC = { name: "topic", params: { alias: "general", num: 42 } };

describe("TopicUnread", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    replace.mockReset();
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
