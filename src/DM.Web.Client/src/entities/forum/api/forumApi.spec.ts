/**
 * @vitest-environment node
 */

// News go by publication and at most two at a time; both rules live in the query string.
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
