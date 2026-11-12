/**
 * @vitest-environment jsdom
 */

// The latest block asks at once with the best of the week, and shows nothing before both answered.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import LatestRatedPost from "./LatestRatedPost.vue";

const { mockGetRatedPosts } = vi.hoisted(() => ({
  mockGetRatedPosts: vi.fn(),
}));

vi.mock("@/entities/game/api/postApi", () => ({
  default: { getRatedPosts: mockGetRatedPosts },
}));

type Answer =
  | { data: { resources: { id: string }[] }; error: null }
  | { data: null; error: { status: number } };

/** Each request waits until the test answers it, by the sort it asked for. */
function holdRequests() {
  const pending = new Map<string, (answer: Answer) => void>();
  mockGetRatedPosts.mockImplementation(
    (params: { sortBy: string }) =>
      new Promise<Answer>((resolve) => pending.set(params.sortBy, resolve)),
  );
  return (sortBy: string, ids: string[] | "fail") =>
    pending.get(sortBy)!(
      ids === "fail"
        ? { data: null, error: { status: 500 } }
        : { data: { resources: ids.map((id) => ({ id })) }, error: null },
    );
}

function mountBlock() {
  const pinia = createPinia();
  setActivePinia(pinia);
  return mount(LatestRatedPost, {
    shallow: true,
    global: {
      plugins: [pinia],
      stubs: {
        GamePost: {
          props: ["post"],
          template: `<article class="post-stub">{{ post.id }}</article>`,
        },
        GamePostSkeleton: { template: `<div class="skeleton-stub" />` },
        // Real, so the click reaches the retry the block wires in.
        ErrorState: false,
      },
    },
  });
}

describe("LatestRatedPost", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("asks for the latest post without waiting for the best of the week", async () => {
    holdRequests();

    mountBlock();
    await flushPromises();

    // Neither request has an answer yet, and both are out.
    expect(mockGetRatedPosts).toHaveBeenCalledTimes(2);
    expect(mockGetRatedPosts).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: "rating" }),
    );
    expect(mockGetRatedPosts).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: "lastreview", take: 2 }),
    );
  });

  it("keeps the skeleton until the best of the week answers, then skips it", async () => {
    const answer = holdRequests();
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
    const answer = holdRequests();
    const wrapper = mountBlock();
    await flushPromises();
    answer("lastreview", "fail");
    answer("rating", ["best"]);
    await flushPromises();
    mockGetRatedPosts.mockClear();

    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(mockGetRatedPosts).toHaveBeenCalledTimes(1);
    expect(mockGetRatedPosts).toHaveBeenCalledWith(
      expect.objectContaining({ sortBy: "lastreview" }),
    );
  });
});
