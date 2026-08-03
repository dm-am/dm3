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
});
