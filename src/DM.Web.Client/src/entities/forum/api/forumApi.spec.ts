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
import type { BoardId } from "../model/types";
import forumApi, { NEWS_WIDGET_LIMIT } from "./forumApi";

const NEWS = "news" as BoardId;

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

/**
 * Who answers for a failed read of a board listing.
 *
 * The listing shows one itself: a strip stands where the rows would be, says
 * "Не удалось загрузить топики" and offers the read again. The response
 * interceptor raises a plate for a 403, a 429 and a 5xx, so the reader got the
 * same refusal in two places at once — and the plate is the one that reads as a
 * site-wide complaint about a board that simply would not load.
 *
 * The flag that settles it is the caller's to set, and these two assert the
 * method keeps it that way: it carries whatever the caller hands it, and
 * invents nothing for a caller that hands it nothing. The same endpoint is read
 * three more times to refresh the pinned block after a pin or a reorder, and
 * those reads show nothing at all about a failure — a flag pinned inside the
 * method would take away the only voice they have.
 */
describe("forumApi.getTopics", () => {
  /** The options the client was handed for the last read it was given. */
  const optionsOfLastCall = () =>
    vi.mocked(Api.get).mock.calls.at(-1)?.[3] as
      | { ownsRefusal?: boolean; ownsRateLimit?: boolean }
      | undefined;

  it("hands the client the flags its caller set", async () => {
    await forumApi.getTopics(
      NEWS,
      { number: 1 },
      { ownsRefusal: true, ownsRateLimit: true },
    );

    expect(optionsOfLastCall()).toEqual({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
  });

  it("leaves the refusal to the interceptor for a caller that is silent", async () => {
    await forumApi.getTopics(NEWS, { isAttached: true });

    expect(optionsOfLastCall()).toBeUndefined();
  });
});

/**
 * Who answers for a failed read of a board.
 *
 * The read that opens a board's page is shown by that page when it fails: the
 * error page of the status stands in place of the board (ForumPage). The
 * interceptor put its plate over it as well, and a guest refused with a 429
 * read "Упс! Слишком много запросов!" and "Слишком много попыток" at once, the
 * plate over the support block of a phone. The board is read twice more,
 * again after a sign-in and as the board of a topic, and neither shows a word
 * about a failure: the interceptor is their only voice. So the flag is the
 * caller's to set, as for the listing above, and the method only carries it.
 */
describe("forumApi.getBoard", () => {
  const GENERAL = "general" as BoardId;

  /** What the client was handed for the last read: address and options. */
  const lastCall = () => vi.mocked(Api.get).mock.calls.at(-1);

  it("hands the client the flags its caller set", async () => {
    await forumApi.getBoard(GENERAL, {
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    expect(lastCall()?.[0]).toBe("boards/general");
    expect(lastCall()?.[3]).toEqual({ ownsRefusal: true, ownsRateLimit: true });
  });

  it("leaves the refusal to the interceptor for a caller that is silent", async () => {
    await forumApi.getBoard(GENERAL);

    expect(lastCall()?.[0]).toBe("boards/general");
    expect(lastCall()?.[3]).toBeUndefined();
  });
});
