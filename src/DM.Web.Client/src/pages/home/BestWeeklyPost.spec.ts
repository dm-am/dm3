/**
 * @vitest-environment jsdom
 */

// A refusal is not an answer: the retry waits under the skeleton, not under "no posts".
import { describe, it, expect, beforeEach, vi } from "vitest";
import BestWeeklyPost from "./BestWeeklyPost.vue";
import {
  expectSkeletonDuringRetry,
  holdRatedPosts,
  mountRatedPostBlock,
} from "@/entities/game/model/ratedPosts.fixtures";

describe("BestWeeklyPost", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the skeleton, not the empty text, while a retry is out", async () => {
    const { requests, answer } = holdRatedPosts();

    await expectSkeletonDuringRetry(
      mountRatedPostBlock(BestWeeklyPost),
      () => answer("rating", "fail"),
      () => answer("rating", []),
    );
    // The first load and the retry, each its own request.
    expect(requests).toHaveBeenCalledTimes(2);
  });
});
