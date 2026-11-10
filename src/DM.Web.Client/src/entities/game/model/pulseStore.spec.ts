import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { ApiResult, ListEnvelope } from "@/shared/api/models/common";
import type { Post } from "./postTypes";
import { createMockPost } from "./store.fixtures";
import { deferred } from "@/shared/lib/testing/tasks";

const getRatedPosts = vi.fn();

vi.mock("../api/postApi", () => ({
  default: {
    getRatedPosts: (...args: unknown[]) => getRatedPosts(...args),
  },
}));

const { usePulseStore } = await import("./pulseStore");

const page = (title: string): ApiResult<ListEnvelope<Post>> => ({
  data: {
    resources: [createMockPost(title, title)],
    paging: null,
  },
  error: null,
});

const titles = (posts: Post[]): string[] => posts.map((p) => p.id);

/**
 * Time between two calls, with nothing else faked.
 *
 * The request this store builds carries `lastReviewedFromUtc:
 * getWeekStartUtc()` — `Date.now()` minus seven days, taken at call time. Two
 * calls in the same millisecond produce the same string, so a key built from
 * the request looked exactly like a key built from the filter, and the two
 * facts below passed with the defect they exist to catch still in the code. A
 * real page has a round trip between the calls; this is that round trip.
 */
function aMomentPasses(): void {
  vi.setSystemTime(new Date(Date.now() + 1000));
}

describe("usePulseStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    getRatedPosts.mockReset();
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-06-01T21:00:00.000Z"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // The abandoned request asked what the newest asks, so only a count of requests tells them apart.
  it("drops the answer to an abandoned request that asked the same question", async () => {
    const first = deferred<ApiResult<ListEnvelope<Post>>>();
    const second = deferred<ApiResult<ListEnvelope<Post>>>();
    const third = deferred<ApiResult<ListEnvelope<Post>>>();
    getRatedPosts
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
      .mockReturnValueOnce(third.promise);

    const store = usePulseStore();

    const abandoned = store.fetchPosts({ number: 1 });
    const other = store.fetchPosts({ number: 2 });
    const current = store.fetchPosts({ number: 1 });

    third.resolve(page("свежая первая страница"));
    await current;
    second.resolve(page("вторая страница"));
    await other;
    first.resolve(page("брошенная первая страница"));
    await abandoned;

    expect(titles(store.posts)).toEqual(["свежая первая страница"]);
  });

  it("does not put a second request on the wire for the one already there", async () => {
    const only = deferred<ApiResult<ListEnvelope<Post>>>();
    getRatedPosts.mockReturnValue(only.promise);

    const store = usePulseStore();
    const started = store.fetchPosts({ number: 1 });
    aMomentPasses();
    const repeat = store.fetchPosts({ number: 1 });

    only.resolve(page("первая страница"));
    await Promise.all([started, repeat]);

    expect(getRatedPosts).toHaveBeenCalledTimes(1);
  });

  // Keyed by the filter: the request carries a timestamp taken at call time.
  it("answers the same page from the cache instead of asking again", async () => {
    getRatedPosts.mockResolvedValue(page("первая страница"));

    const store = usePulseStore();
    await store.fetchPosts({ number: 1 });
    aMomentPasses();
    await store.fetchPosts({ number: 1 });

    expect(getRatedPosts).toHaveBeenCalledTimes(1);
    expect(titles(store.posts)).toEqual(["первая страница"]);
  });

  it("keeps a cleared store cleared when the answer it was waiting for lands", async () => {
    const pending = deferred<ApiResult<ListEnvelope<Post>>>();
    getRatedPosts.mockReturnValue(pending.promise);

    const store = usePulseStore();
    const fetching = store.fetchPosts({ number: 1 });
    store.clear();

    pending.resolve(page("страница из прошлого"));
    await fetching;

    // clear() is the reader leaving the page. An answer that arrives afterwards
    // has nowhere to go, and putting it in the store resurrects what was cleared.
    expect(store.posts).toEqual([]);
  });
  // The words of the shared list of rated posts (RatedPostsList), so a failure reads the same on /pulse.
  it("keeps the posts shown and names the failure as the shared list does", async () => {
    getRatedPosts.mockResolvedValueOnce(page("первая страница"));
    const store = usePulseStore();
    await store.fetchPosts({ number: 1 });

    getRatedPosts.mockResolvedValueOnce({ data: null, error: { status: 500 } });
    aMomentPasses();
    await store.fetchPosts({ number: 2 });

    expect(store.error).toBe("Не удалось загрузить оцененные посты");
    expect(titles(store.posts)).toEqual(["первая страница"]);
  });
});
