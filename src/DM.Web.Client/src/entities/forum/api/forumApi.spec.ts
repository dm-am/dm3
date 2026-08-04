/**
 * @vitest-environment node
 */

/**
 * The homepage news block is the only caller of this request, and it had lost
 * both of its rules to server defaults.
 *
 * Order: TopicRepository sorts by last activity unless a key is given, so a
 * topic published on 29.07 that collected a comment stood above one published
 * on 01.08. News are ordered by publication, not by discussion.
 *
 * Count: `take` was a page size (5), and the number of cards was decided by the
 * seven-day freshness filter alone — three today, two tomorrow, five after a
 * busy week. The owner's rule is at most two at a time, and the request is
 * where the cap starts.
 *
 * Both rules live in the query string, so this asserts the query string. Swap
 * the sort key back to the default, or raise the cap, and it goes red.
 */
import { describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: { get: vi.fn().mockResolvedValue({ data: null, error: null }) },
  RENDER_AUDIENCE: { AuthorEdit: "AuthorEdit" },
}));

import { Api } from "@/shared/api";
import forumApi, { NEWS_WIDGET_LIMIT } from "./forumApi";

describe("forumApi.getNews", () => {
  it("orders the homepage news by publication time, newest first", async () => {
    await forumApi.getNews();

    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      "boards/news/topics",
      expect.objectContaining({ sortBy: "created", sortOrder: "desc" }),
    );
  });

  it("asks for no more news than the block is allowed to show", async () => {
    await forumApi.getNews();

    expect(NEWS_WIDGET_LIMIT).toBe(2);
    expect(vi.mocked(Api.get)).toHaveBeenCalledWith(
      "boards/news/topics",
      expect.objectContaining({ take: NEWS_WIDGET_LIMIT }),
    );
  });
});
