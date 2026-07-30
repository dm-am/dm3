import { afterEach, describe, expect, it, vi } from "vitest";
import { createKeyedCache, stableCacheKey } from "./keyedCache";

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
