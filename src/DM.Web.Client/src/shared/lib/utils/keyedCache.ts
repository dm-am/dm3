export interface KeyedCacheOptions {
  /** How long an entry counts as fresh. */
  ttlMs: number;
  /** How many entries to keep; the oldest are dropped first. Default: 20. */
  maxEntries?: number;
}

export interface KeyedCache<T> {
  /** The entry if it is still fresh, otherwise undefined. */
  get(key: string): T | undefined;
  /** The entry regardless of age — what stale-while-revalidate renders first. */
  getStale(key: string): T | undefined;
  /** True when an entry exists but has aged out. */
  isStale(key: string): boolean;
  set(key: string, value: T): void;
  clear(): void;
}

/**
 * A keyed cache with an age limit and a size limit.
 *
 * Seven stores kept their own `Map<string, { data, timestamp }>` with the same
 * 30-second window, the same twenty-entry cap and the same insertion-order
 * eviction — the differences between the copies were spelling. What actually
 * varies between them is the flow around the cache (what to show while
 * refreshing, when to prefetch), so that stays with each store and only the
 * bookkeeping moves here.
 *
 * Eviction is insertion order rather than least-recently-used: a Map iterates in
 * insertion order, and re-setting a key keeps its original position. That is
 * what the copies did, and for a cache holding pages of one search it is the
 * right shape anyway — the oldest key is the page the reader has moved furthest
 * away from.
 */
export function createKeyedCache<T>(options: KeyedCacheOptions): KeyedCache<T> {
  const { ttlMs, maxEntries = 20 } = options;
  const entries = new Map<string, { value: T; storedAt: number }>();

  function isFresh(storedAt: number): boolean {
    return Date.now() - storedAt <= ttlMs;
  }

  return {
    get(key) {
      const entry = entries.get(key);
      return entry && isFresh(entry.storedAt) ? entry.value : undefined;
    },

    getStale(key) {
      return entries.get(key)?.value;
    },

    isStale(key) {
      const entry = entries.get(key);
      return entry !== undefined && !isFresh(entry.storedAt);
    },

    set(key, value) {
      entries.set(key, { value, storedAt: Date.now() });
      if (entries.size > maxEntries) {
        // The iterator's first key is the oldest insertion.
        const oldest = entries.keys().next().value;
        if (oldest !== undefined) entries.delete(oldest);
      }
    },

    clear() {
      entries.clear();
    },
  };
}

/**
 * A cache key for a set of search parameters that does not have to be maintained.
 *
 * The hand-written keys this replaces listed every parameter and its default by
 * name, which reads fine and fails quietly: a filter added to the search and not
 * to its key makes two different searches share one entry, and the reader gets
 * the other one's results. Sorting the keys here means a parameter is covered
 * because it exists, not because somebody remembered it.
 *
 * Empty, null and undefined values are dropped so an untouched filter and a
 * cleared one agree. Arrays are sorted: the same tags in a different order are
 * the same search.
 */
export function stableCacheKey(params: object): string {
  const normalized: Record<string, unknown> = {};
  // `object` rather than Record<string, unknown>: the callers pass their own
  // search-params interfaces, and an interface without an index signature is not
  // assignable to a Record. Nothing here needs the declared shape.
  const source = params as Record<string, unknown>;

  for (const key of Object.keys(source).sort()) {
    const value = source[key];
    if (value === undefined || value === null || value === "") continue;
    normalized[key] = Array.isArray(value)
      ? [...value].map(String).sort().join(",")
      : value;
  }

  return JSON.stringify(normalized);
}
