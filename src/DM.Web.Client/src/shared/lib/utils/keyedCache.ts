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
  /** Ages an entry out without dropping it: stale is not absent. */
  expire(key: string): void;
  clear(): void;
}

/**
 * A keyed cache with an age limit and a size limit, evicting in insertion
 * order (the oldest key is the page furthest from the reader); the flow around
 * it stays with each store.
 */
export function createKeyedCache<T>(options: KeyedCacheOptions): KeyedCache<T> {
  const { ttlMs, maxEntries = 20 } = options;
  const entries = new Map<
    string,
    { value: T; storedAt: number; expired: boolean }
  >();

  function isFresh(entry: { storedAt: number; expired: boolean }): boolean {
    // A flag, not a doctored timestamp: ttlMs may be Infinity.
    return !entry.expired && Date.now() - entry.storedAt <= ttlMs;
  }

  return {
    get(key) {
      const entry = entries.get(key);
      return entry && isFresh(entry) ? entry.value : undefined;
    },

    getStale(key) {
      return entries.get(key)?.value;
    },

    isStale(key) {
      const entry = entries.get(key);
      return entry !== undefined && !isFresh(entry);
    },

    set(key, value) {
      entries.set(key, { value, storedAt: Date.now(), expired: false });
      if (entries.size > maxEntries) {
        // The iterator's first key is the oldest insertion.
        const oldest = entries.keys().next().value;
        if (oldest !== undefined) entries.delete(oldest);
      }
    },

    expire(key) {
      const entry = entries.get(key);
      if (entry) entry.expired = true;
    },

    clear() {
      entries.clear();
    },
  };
}

/** Whether a thing fetched once is still worth keeping, with no copy of it. */
export interface Freshness {
  /**
   * Recorded and not aged out. Not the negation of isStale(), which answers
   * false for something never recorded too.
   */
  isFresh(): boolean;
  /** True once the thing has been recorded and has since aged out. */
  isStale(): boolean;
  /** Record that it was just fetched. */
  touch(): void;
  /** Age it out on demand, without forgetting that it was ever fetched. */
  expire(): void;
  /** Forget it entirely — the next isStale() answers false, as it did at the start. */
  clear(): void;
}

/** The cache's freshness for useApiResource, which keeps its payload in a ref. */
export function createFreshness(ttlMs: number): Freshness {
  const SLOT = "it";
  const store = createKeyedCache<true>({ ttlMs, maxEntries: 1 });

  return {
    // `true` only while fresh: aged out and never written answer alike.
    isFresh: () => store.get(SLOT) === true,
    isStale: () => store.isStale(SLOT),
    touch: () => store.set(SLOT, true),
    expire: () => store.expire(SLOT),
    clear: () => store.clear(),
  };
}

/** A moment recorded once, read back as the time since it. */
export interface Stopwatch {
  /** Milliseconds since the stopwatch was started. */
  elapsedMs(): number;
  /** Milliseconds left of `totalMs` from that moment, floored at zero. */
  remainingMs(totalMs: number): number;
}

/**
 * Elapsed time with nothing kept beside it, for the unread badge's debounce
 * ceiling and the socket's first-start retries: "how long since" in one place.
 */
export function startStopwatch(): Stopwatch {
  const startedAt = Date.now();
  const elapsedMs = () => Date.now() - startedAt;

  return {
    elapsedMs,
    remainingMs: (totalMs) => Math.max(0, totalMs - elapsedMs()),
  };
}

/**
 * A cache key of search parameters, by sorted keys, so a new parameter is
 * covered because it exists. Empty values are dropped and arrays sorted.
 */
export function stableCacheKey(params: object): string {
  const normalized: Record<string, unknown> = {};
  // `object`: the callers' interfaces have no index signature for a Record.
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
