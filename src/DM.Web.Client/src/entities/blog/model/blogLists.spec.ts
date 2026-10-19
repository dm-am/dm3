/**
 * @vitest-environment jsdom
 */

/**
 * "Активные блоги" reads the rows of its block. Widened, the list asks the
 * server for the rows named, though the copy it holds is fresh for a minute.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { Api } from "@/shared/api";
import { createBlogLists } from "./blogLists";
import { SIDEBAR_BLOG_ROWS } from "./sidebarRows";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("the active blogs of the left column", () => {
  it("ask for the rows they are widened to, past their fresh copy", async () => {
    const answer = { data: { resources: [] }, error: null };
    const get = vi.spyOn(Api, "get").mockResolvedValue(answer as never);
    const { lists } = createBlogLists();

    await lists.fetchActiveBlogs();
    await lists.widenActiveBlogs(9);

    expect(get.mock.calls.map(([url, query]) => [url, query?.take])).toEqual([
      ["blogs", SIDEBAR_BLOG_ROWS],
      ["blogs", 9],
    ]);
    expect(lists.activeBlogsTake.value).toBe(9);
  });
});
