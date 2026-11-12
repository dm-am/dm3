/**
 * @vitest-environment jsdom
 */

// The latest block asks at once with the best of the week, and shows nothing before both answered.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import LatestRatedPost from "./LatestRatedPost.vue";
import {
  expectSkeletonDuringRetry,
  holdRatedPosts,
  mountRatedPostBlock,
} from "@/entities/game/model/ratedPosts.fixtures";

const mountBlock = () => mountRatedPostBlock(LatestRatedPost);

describe("LatestRatedPost", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("asks for the latest post without waiting for the best of the week", async () => {
    const { requests } = holdRatedPosts();

    mountBlock();
    await flushPromises();

    // Neither request has an answer yet, and both are out.
    expect(requests).toHaveBeenCalledTimes(2);
    expect(requests).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: "rating" }),
    );
    expect(requests).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: "lastreview", take: 2 }),
    );
  });

  it("keeps the skeleton until the best of the week answers, then skips it", async () => {
    const { answer } = holdRatedPosts();
    const wrapper = mountBlock();
    await flushPromises();

    answer("lastreview", ["best", "p-2"]);
    await flushPromises();

    // Shown now, "best" would be swapped for "p-2" a moment later.
    expect(wrapper.find(".post-stub").exists()).toBe(false);
    expect(wrapper.find(".skeleton-stub").exists()).toBe(true);

    answer("rating", ["best"]);
    await flushPromises();

    expect(wrapper.find(".post-stub").text()).toBe("p-2");
  });

  it("asks only for the latest post again on a retry", async () => {
    const { requests, answer } = holdRatedPosts();
    const wrapper = mountBlock();
    await flushPromises();
    answer("lastreview", "fail");
    answer("rating", ["best"]);
    await flushPromises();
    requests.mockClear();

    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(requests).toHaveBeenCalledTimes(1);
    expect(requests).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: "lastreview" }),
    );
  });

  it("shows the skeleton, not the empty text, while a retry is out", async () => {
    const { answer } = holdRatedPosts();
    const wrapper = mountBlock();

    await expectSkeletonDuringRetry(
      wrapper,
      () => {
        answer("lastreview", "fail");
        answer("rating", ["best"]);
      },
      () => answer("lastreview", []),
    );
  });
});
