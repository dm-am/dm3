import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createFreshness,
  createKeyedCache,
  stableCacheKey,
  startStopwatch,
} from "./keyedCache";

describe("createKeyedCache", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("returns an entry while it is fresh", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000 });
    cache.set("a", 1);

    expect(cache.get("a")).toBe(1);
    expect(cache.isStale("a")).toBe(false);
  });

  it("withholds an entry that has aged out but still hands it over as stale", () => {
    vi.useFakeTimers();
    const cache = createKeyedCache<number>({ ttlMs: 1000 });
    cache.set("a", 1);

    vi.advanceTimersByTime(1001);

    // The distinction the stores need: get() decides whether to fetch,
    // getStale() decides what to render meanwhile.
    expect(cache.get("a")).toBeUndefined();
    expect(cache.getStale("a")).toBe(1);
    expect(cache.isStale("a")).toBe(true);
  });

  it("says nothing about a key it never held", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000 });

    expect(cache.get("missing")).toBeUndefined();
    expect(cache.getStale("missing")).toBeUndefined();
    // Not stale — absent. A store that treats these the same would refetch
    // forever on a key it has no entry for.
    expect(cache.isStale("missing")).toBe(false);
  });

  it("drops the oldest entry past the size limit", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000, maxEntries: 2 });
    cache.set("a", 1);
    cache.set("b", 2);
    cache.set("c", 3);

    expect(cache.get("a")).toBeUndefined();
    expect(cache.get("b")).toBe(2);
    expect(cache.get("c")).toBe(3);
  });

  it("keeps a rewritten key in its original position", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000, maxEntries: 2 });
    cache.set("a", 1);
    cache.set("b", 2);
    cache.set("a", 10);
    cache.set("c", 3);

    // Rewriting "a" did not make it younger, so it is still the one to go.
    expect(cache.get("a")).toBeUndefined();
    expect(cache.get("c")).toBe(3);
  });

  it("empties on clear", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000 });
    cache.set("a", 1);
    cache.clear();

    expect(cache.getStale("a")).toBeUndefined();
  });

  it("ages an entry out on demand without losing it", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000 });
    cache.set("a", 1);
    cache.expire("a");

    // A mutation makes what is stored wrong, but it is still what the reader
    // is looking at: dropping it would blank the screen instead of refreshing
    // it, which is the bug that made creating a game empty "Мои игры".
    expect(cache.get("a")).toBeUndefined();
    expect(cache.isStale("a")).toBe(true);
    expect(cache.getStale("a")).toBe(1);
  });

  it("ages out an entry that no elapsed time could age out", () => {
    const cache = createKeyedCache<number>({ ttlMs: Infinity });
    cache.set("a", 1);
    cache.expire("a");

    // A closed calendar period never expires by the clock, so expiry cannot be
    // written as a doctored timestamp — no timestamp is old enough.
    expect(cache.isStale("a")).toBe(true);
  });

  it("brings an expired entry back when it is written again", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000 });
    cache.set("a", 1);
    cache.expire("a");
    cache.set("a", 2);

    expect(cache.get("a")).toBe(2);
    expect(cache.isStale("a")).toBe(false);
  });

  it("says nothing about expiring a key it never held", () => {
    const cache = createKeyedCache<number>({ ttlMs: 1000 });
    cache.expire("missing");

    expect(cache.isStale("missing")).toBe(false);
    expect(cache.getStale("missing")).toBeUndefined();
  });
});

/**
 * The clock of the cache without the cache: a caller that holds the answer
 * itself and only needs to know whether it is still worth keeping.
 */
describe("createFreshness", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  /**
   * The question a caller actually asks is "may I skip this read", and isStale
   * cannot answer it: it says false for something fresh and false again for
   * something never read at all. A caller reading that as "leave it alone"
   * never makes the first read; one reading it as "go and read" makes a second
   * read a moment after the first. So the two are separate answers.
   */
  it("tells fresh apart from never recorded", () => {
    vi.useFakeTimers();
    const freshness = createFreshness(1000);

    expect(freshness.isFresh()).toBe(false);
    expect(freshness.isStale()).toBe(false);

    freshness.touch();
    expect(freshness.isFresh()).toBe(true);
    expect(freshness.isStale()).toBe(false);

    vi.advanceTimersByTime(1001);
    expect(freshness.isFresh()).toBe(false);
    expect(freshness.isStale()).toBe(true);
  });

  it("is no longer fresh once it is aged out on demand", () => {
    const freshness = createFreshness(1000);
    freshness.touch();
    freshness.expire();

    expect(freshness.isFresh()).toBe(false);
    expect(freshness.isStale()).toBe(true);
  });

  /**
   * Forgetting goes back to the start rather than to "stale": a read that
   * failed left nothing behind, and the next caller has to make it.
   */
  it("is no longer fresh once it is forgotten", () => {
    const freshness = createFreshness(1000);
    freshness.touch();
    freshness.clear();

    expect(freshness.isFresh()).toBe(false);
    expect(freshness.isStale()).toBe(false);
  });
});

/**
 * The callers that measure elapsed time without keeping an answer: a debounce
 * that may not be postponed past a ceiling, and a retry loop reporting how long
 * it has been trying. Both used to subtract a `Date.now()` of their own from
 * the clock, which is the arithmetic this module exists to hold once.
 */
describe("startStopwatch", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("counts from the moment it was started", () => {
    vi.useFakeTimers();
    const watch = startStopwatch();

    expect(watch.elapsedMs()).toBe(0);
    vi.advanceTimersByTime(1500);
    expect(watch.elapsedMs()).toBe(1500);
  });

  it("counts a ceiling down and stops at nothing left", () => {
    vi.useFakeTimers();
    const watch = startStopwatch();

    vi.advanceTimersByTime(1500);
    expect(watch.remainingMs(10000)).toBe(8500);

    // Past the ceiling there is no time left rather than negative time: a
    // caller handing this to setTimeout would otherwise have to clamp it, and
    // the one that forgot is the bug this replaces.
    vi.advanceTimersByTime(10000);
    expect(watch.remainingMs(10000)).toBe(0);
  });
});

describe("stableCacheKey", () => {
  it("ignores the order the parameters were written in", () => {
    expect(stableCacheKey({ b: 2, a: 1 })).toBe(stableCacheKey({ a: 1, b: 2 }));
  });

  it("treats an untouched filter and a cleared one as the same search", () => {
    const empty = stableCacheKey({});

    expect(stableCacheKey({ search: "" })).toBe(empty);
    expect(stableCacheKey({ search: undefined })).toBe(empty);
    expect(stableCacheKey({ search: null })).toBe(empty);
  });

  it("ignores the order of a multi-select", () => {
    expect(stableCacheKey({ tags: ["b", "a"] })).toBe(
      stableCacheKey({ tags: ["a", "b"] }),
    );
  });

  it("separates searches that differ", () => {
    expect(stableCacheKey({ search: "a" })).not.toBe(
      stableCacheKey({ search: "b" }),
    );
    // False is a value, not an absence: "only offline" is not "any presence".
    expect(stableCacheKey({ isOnline: false })).not.toBe(stableCacheKey({}));
  });

  it("covers a parameter nobody listed", () => {
    // The point of the whole function: a filter added to the query changes the
    // key without anybody editing the key.
    expect(stableCacheKey({ search: "a", newFilter: 5 })).not.toBe(
      stableCacheKey({ search: "a" }),
    );
  });
});
