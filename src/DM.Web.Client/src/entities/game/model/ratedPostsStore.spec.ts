/**
 * @vitest-environment node
 */

/**
 * The two homepage post blocks differ by one query key each, and swapping them
 * is invisible on screen: both would still show a post with reviews.
 *
 * "Лучший пост недели" ranks by rating INSIDE the current week — the window is
 * a filter on the post's creation date, and dropping it hands the block to the
 * best post of all time. "Последний оцененный пост" ranks by the freshest
 * review, not by rating, and takes no window at all.
 *
 * The window is seven days back from now, the same window the seeder uses when
 * it places the showcase post (DataSeeder.WeekStartUtc). If the two disagree,
 * the block is empty. It is rolling rather than pinned to Monday because a
 * calendar boundary empties the block for the first hours of every Monday, and
 * empties it for good once a fixture is a week old.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";

const { mockGetRatedPosts } = vi.hoisted(() => ({
  mockGetRatedPosts: vi.fn(),
}));

vi.mock("../api/gameApi", () => ({
  default: { getRatedPosts: mockGetRatedPosts },
}));

import { useRatedPostsStore } from "./ratedPostsStore";

describe("useRatedPostsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    mockGetRatedPosts.mockResolvedValue({
      data: { resources: [] },
      error: null,
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("ranks the best of the week by rating inside the last seven days", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-08-05T09:30:00.000Z"));

    await useRatedPostsStore().fetchBestOfWeek();

    expect(mockGetRatedPosts).toHaveBeenCalledWith({
      sortBy: "rating",
      hasReviews: true,
      createdAfter: "2026-07-29T09:30:00.000Z",
      take: 1,
    });
  });

  it("keeps a week behind it on a Monday morning, when a calendar week has none", async () => {
    vi.useFakeTimers();
    // Monday, half an hour in. A calendar window would ask for half an hour of
    // posts and show nothing, which is exactly what the homepage did.
    vi.setSystemTime(new Date("2026-08-03T00:30:00.000Z"));

    await useRatedPostsStore().fetchBestOfWeek();

    expect(mockGetRatedPosts).toHaveBeenCalledWith(
      expect.objectContaining({ createdAfter: "2026-07-27T00:30:00.000Z" }),
    );
  });

  it("picks the latest rated post by the freshest review, with no week window", async () => {
    await useRatedPostsStore().fetchLatestRated();

    expect(mockGetRatedPosts).toHaveBeenCalledWith({
      sortBy: "lastreview",
      hasReviews: true,
      take: 1,
    });
  });

  /**
   * The three blocks used to keep their own answer to "is mine still fresh":
   * two module-level counters and a `fetchedAt` on every per-user entry. The
   * bookkeeping is shared now (`shared/lib/utils/keyedCache`), and these hold
   * what the three questions may not do to each other.
   */
  describe("caching", () => {
    const post = (id: string) => ({ id }) as never;

    it("asks the server once for a block that has an answer", async () => {
      mockGetRatedPosts.mockResolvedValue({
        data: { resources: [post("p-1")] },
        error: null,
      });
      const store = useRatedPostsStore();

      await store.fetchBestOfWeek();
      await store.fetchBestOfWeek();

      expect(mockGetRatedPosts).toHaveBeenCalledTimes(1);
      expect(store.bestOfWeek?.id).toBe("p-1");
    });

    it("takes a week without a rated post for an answer", async () => {
      // "None" is what the server said, not a failure to load: the counters
      // this replaces kept a null as "never fetched" and asked again on every
      // visit to the home page.
      const store = useRatedPostsStore();

      await store.fetchBestOfWeek();
      await store.fetchBestOfWeek();

      expect(mockGetRatedPosts).toHaveBeenCalledTimes(1);
      expect(store.bestLoaded).toBe(true);
    });

    it("does not answer one block out of another block's entry", async () => {
      mockGetRatedPosts.mockResolvedValue({
        data: { resources: [post("p-1")] },
        error: null,
      });
      const store = useRatedPostsStore();

      await store.fetchBestOfWeek();
      await store.fetchLatestRated();
      await store.fetchBestPostOfUser("Tester");

      expect(mockGetRatedPosts).toHaveBeenCalledTimes(3);
    });

    it("re-reads the latest rated post when another one is excluded", async () => {
      mockGetRatedPosts.mockResolvedValue({
        data: { resources: [post("p-1"), post("p-2")] },
        error: null,
      });
      const store = useRatedPostsStore();

      await store.fetchLatestRated("p-1");
      await store.fetchLatestRated("p-2");

      // The answer is picked out of the page BY the excluded id, so one entry
      // for both would hand the second call the post it asked to avoid.
      expect(mockGetRatedPosts).toHaveBeenCalledTimes(2);
      expect(store.latestRated?.id).toBe("p-1");
    });

    it("keeps one entry per profile", async () => {
      mockGetRatedPosts.mockResolvedValue({
        data: { resources: [post("p-1")] },
        error: null,
      });
      const store = useRatedPostsStore();

      await store.fetchBestPostOfUser("Alice");
      await store.fetchBestPostOfUser("Bob");
      await store.fetchBestPostOfUser("Alice");

      expect(mockGetRatedPosts).toHaveBeenCalledTimes(2);
    });

    it("asks again when the caller forces it", async () => {
      const store = useRatedPostsStore();

      await store.fetchBestOfWeek();
      await store.fetchBestOfWeek(true);
      await store.fetchLatestRated();
      await store.fetchLatestRated(undefined, true);
      await store.fetchBestPostOfUser("Alice");
      await store.fetchBestPostOfUser("Alice", true);

      expect(mockGetRatedPosts).toHaveBeenCalledTimes(6);
    });

    it("lets the retry button retry after a refusal", async () => {
      mockGetRatedPosts
        .mockResolvedValueOnce({ data: null, error: { status: 500 } })
        .mockResolvedValueOnce({
          data: { resources: [post("p-1")] },
          error: null,
        });
      const store = useRatedPostsStore();

      await store.fetchBestPostOfUser("Alice");
      expect(store.bestPostErrorOf("Alice")).toBeTruthy();

      // Not forced: a cached refusal would leave the block refusing to try
      // for five minutes, which is what the entry's fetchedAt used to do.
      await store.fetchBestPostOfUser("Alice");

      expect(mockGetRatedPosts).toHaveBeenCalledTimes(2);
      expect(store.bestPostErrorOf("Alice")).toBeNull();
      expect(store.bestPostOfUser("Alice")?.id).toBe("p-1");
    });
  });
});
