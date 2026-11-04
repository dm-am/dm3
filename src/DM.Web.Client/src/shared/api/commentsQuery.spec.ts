/**
 * @vitest-environment node
 */

/** The wire form of a comments query: skip/take for the page, authors as authorUsernames. */
import { describe, expect, it } from "vitest";
import { toCommentsQueryParams } from "./commentsQuery";

describe("toCommentsQueryParams", () => {
  it("asks for the default page without an offset", () => {
    expect(toCommentsQueryParams()).toEqual({ take: 20 });
  });

  it("does not offset the first page", () => {
    expect(toCommentsQueryParams({ number: 1, size: 10 })).toEqual({
      take: 10,
    });
  });

  it("converts a page number into the offset the wire takes", () => {
    expect(toCommentsQueryParams({ number: 3, size: 25 })).toEqual({
      take: 25,
      skip: 50,
    });
  });

  it("names the filters as the API does and leaves the empty ones out", () => {
    expect(
      toCommentsQueryParams({
        search: "dice",
        authors: ["ann", "bob"],
        createdFromUtc: "2026-01-01T00:00:00Z",
        sortBy: "likes",
        sortOrder: "desc",
      }),
    ).toEqual({
      take: 20,
      search: "dice",
      authorUsernames: ["ann", "bob"],
      createdFromUtc: "2026-01-01T00:00:00Z",
      sortBy: "likes",
      sortOrder: "desc",
    });
    expect(toCommentsQueryParams({ search: "", authors: [] })).toEqual({
      take: 20,
    });
  });
});
