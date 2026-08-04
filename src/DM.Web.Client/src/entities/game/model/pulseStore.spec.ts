import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { ApiResult, ListEnvelope } from "@/shared/api/models/common";
import type { Post } from "./types";

const getRatedPosts = vi.fn();

vi.mock("../api/gameApi", () => ({
  default: {
    getRatedPosts: (...args: unknown[]) => getRatedPosts(...args),
  },
}));

const { usePulseStore } = await import("./pulseStore");

/** A response the test releases by hand. */
function deferred() {
  let release!: (value: ApiResult<ListEnvelope<Post>>) => void;
  const promise = new Promise<ApiResult<ListEnvelope<Post>>>((resolve) => {
    release = resolve;
  });
  return { promise, release };
}

const page = (title: string): ApiResult<ListEnvelope<Post>> => ({
  data: {
    resources: [{ id: title, text: title } as unknown as Post],
    paging: null,
  } as unknown as ListEnvelope<Post>,
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

  /**
   * The reader leaves a filter and comes back to it while the first answer is
   * still on the wire.
   *
   * This store used to decide "is this answer still wanted" by comparing the
   * request key it had built, which answers a different question: the key of the
   * abandoned first request equals the key of the third, so its answer was
   * accepted and painted over the newer one. The shared guard counts requests
   * rather than describing them, so the only answer that lands is the one asked
   * for last.
   */
  it("drops the answer to an abandoned request that asked the same question", async () => {
    const first = deferred();
    const second = deferred();
    const third = deferred();
    getRatedPosts
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
      .mockReturnValueOnce(third.promise);

    const store = usePulseStore();

    const abandoned = store.fetchPosts({ number: 1 });
    const other = store.fetchPosts({ number: 2 });
    const current = store.fetchPosts({ number: 1 });

    third.release(page("свежая первая страница"));
    await current;
    second.release(page("вторая страница"));
    await other;
    first.release(page("брошенная первая страница"));
    await abandoned;

    expect(titles(store.posts)).toEqual(["свежая первая страница"]);
  });

  it("does not put a second request on the wire for the one already there", async () => {
    const only = deferred();
    getRatedPosts.mockReturnValue(only.promise);

    const store = usePulseStore();
    const started = store.fetchPosts({ number: 1 });
    aMomentPasses();
    const repeat = store.fetchPosts({ number: 1 });

    only.release(page("первая страница"));
    await Promise.all([started, repeat]);

    expect(getRatedPosts).toHaveBeenCalledTimes(1);
  });

  /**
   * The cache is asked a question it can answer.
   *
   * The key used to be built from the request rather than from the filter, and
   * the request carries the start of the seven-day window as a millisecond
   * timestamp taken at call time. Every call therefore produced a key of its
   * own: the cache was written to, never read from, and the prefetch warmed
   * entries under keys nobody would ever ask for.
   */
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
    const pending = deferred();
    getRatedPosts.mockReturnValue(pending.promise);

    const store = usePulseStore();
    const fetching = store.fetchPosts({ number: 1 });
    store.clear();

    pending.release(page("страница из прошлого"));
    await fetching;

    // clear() is the reader leaving the page. An answer that arrives afterwards
    // has nowhere to go, and putting it in the store resurrects what was cleared.
    expect(store.posts).toEqual([]);
  });
});
