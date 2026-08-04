import { ref, type Ref } from "vue";
import type { ApiResult, GeneralError } from "@/shared/api/models/common";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

export interface UseApiResourceOptions {
  /** Cache duration in milliseconds. Default: 60000 (60 seconds) */
  cacheMs?: number;
  /** Show stale data while fetching fresh data. Default: true */
  staleWhileRevalidate?: boolean;
}

export interface UseApiResourceReturn<T> {
  /** The fetched data, or null if not loaded */
  data: Ref<T | null>;
  /** Error from the last fetch, or null */
  error: Ref<GeneralError | null>;
  /** True while initial loading (no cached data) */
  loading: Ref<boolean>;
  /** Fetch data, optionally forcing refresh */
  fetch: (force?: boolean) => Promise<void>;
  /** Refetch because the data changed underneath, keeping what is on screen */
  invalidate: () => Promise<void>;
  /** Reset state (for logout) */
  reset: () => void;
}

/**
 * Unified composable for API data fetching with caching.
 *
 * Features:
 * - Automatic caching with configurable TTL
 * - Stale-while-revalidate pattern
 * - Prevents parallel fetches
 * - Error handling
 *
 * @example
 * ```ts
 * const { data: games, fetch, reset } = useApiResource(
 *   () => gameApi.getOwnGames(),
 *   { cacheMs: 60_000 }
 * );
 *
 * // In component:
 * onMounted(() => fetch());
 * ```
 */
export function useApiResource<T>(
  fetcher: () => Promise<ApiResult<T>>,
  options: UseApiResourceOptions = {},
): UseApiResourceReturn<T> {
  const { cacheMs = 60_000, staleWhileRevalidate = true } = options;

  const data = ref<T | null>(null) as Ref<T | null>;
  const error = ref<GeneralError | null>(null);
  const loading = ref(false);

  let lastFetched = 0;

  // Discards the answer to a request that a newer one has superseded. Without it
  // a slow first response overwrites a fresh forced one, and a response that
  // arrives after reset() resurrects the data reset() cleared.
  const guard = createRequestGuard();

  // The promise a caller can join. A second fetch() while one is in flight used
  // to get an already-resolved promise back, so `await fetch()` returned before
  // any data existed — and callers that read the store right after the await
  // (the sidebar blocks decide "failed" that way) drew an error on a healthy
  // request.
  let blockingInFlight: Promise<void> | null = null;

  // Background refreshes are deduplicated but never joined: joining one would
  // make stale-while-revalidate block, which is the one thing it exists not to do.
  let backgroundInFlight = false;

  async function fetch(force = false): Promise<void> {
    const now = Date.now();
    const age = now - lastFetched;
    const isStale = age > cacheMs;

    // Fresh cache — do nothing
    if (!force && data.value !== null && !isStale) {
      return;
    }

    if (blockingInFlight && !force) {
      return blockingInFlight;
    }

    if (backgroundInFlight && !force) {
      return;
    }

    // Stale-while-revalidate: return stale data, refresh in background
    if (staleWhileRevalidate && data.value !== null && isStale && !force) {
      const requestId = guard.next();
      backgroundInFlight = true;
      fetcher()
        .then(({ data: newData, error: fetchError }) => {
          // Lowered before the staleness check, not after. The flag says "my
          // request came back", not "my answer still counts": cleared after the
          // check, it stayed raised for good whenever a forced fetch or an
          // invalidate() bumped the guard while this refresh was on the wire,
          // and from then on every background refresh of that resource
          // returned at the flag above without asking the server. The data on
          // screen then aged forever. invalidateGameLists() invalidates six
          // resources at once after a mutation, so one overlap was enough.
          backgroundInFlight = false;
          if (!guard.isCurrent(requestId)) return;
          if (fetchError) {
            console.warn(
              "[useApiResource] Background refresh failed:",
              fetchError.title,
            );
            return;
          }
          if (newData !== undefined) {
            data.value = newData;
            error.value = null;
            lastFetched = Date.now();
          }
        })
        .catch(() => {
          backgroundInFlight = false;
        });
      return;
    }

    // No data or force — wait for load. loading and error are set synchronously,
    // before the first await, so a caller that renders right after calling
    // fetch() already sees the spinner.
    const requestId = guard.next();
    loading.value = true;
    error.value = null;

    const run = (async () => {
      try {
        const { data: newData, error: fetchError } = await fetcher();
        if (!guard.isCurrent(requestId)) return;

        if (fetchError) {
          error.value = fetchError;
          data.value = null;
          return;
        }

        data.value = newData ?? null;
        lastFetched = Date.now();
      } finally {
        if (guard.isCurrent(requestId)) {
          loading.value = false;
          blockingInFlight = null;
        }
      }
    })();

    blockingInFlight = run;
    return run;
  }

  /**
   * The mutation counterpart of reset(): the data on screen is now known to be
   * out of date, so fetch it again — but do not blank it first.
   *
   * reset() was being used for this, and it nulls the data. The blocks that
   * read it (the sidebar lists) fetch on mount and on a change of user, and the
   * shell is mounted once for the whole session, so nothing ever fetched again:
   * creating a game emptied "Мои игры" until a hard reload. Staleness became
   * absence, which is worse than the staleness it was meant to fix.
   */
  function invalidate(): Promise<void> {
    lastFetched = 0;

    // Nothing loaded means nothing on screen to correct, and the next fetch
    // will read fresh data anyway. Refetching here would put a request on the
    // wire for every list the caller invalidates, most of which no mounted
    // component is showing.
    if (data.value === null) return Promise.resolve();

    return fetch(true);
  }

  function reset(): void {
    // Bump the guard so any answer still in flight is dropped instead of
    // repopulating the state this just cleared — logout is the caller.
    guard.next();
    data.value = null;
    error.value = null;
    loading.value = false;
    lastFetched = 0;
    blockingInFlight = null;
    backgroundInFlight = false;
  }

  return { data, error, loading, fetch, invalidate, reset };
}

/**
 * Specialized version for list endpoints that return ListEnvelope.
 * Extracts .resources from the response.
 */
export function useApiList<T>(
  fetcher: () => Promise<ApiResult<{ resources: T[] }>>,
  options: UseApiResourceOptions = {},
): UseApiResourceReturn<T[]> {
  const wrappedFetcher = async (): Promise<ApiResult<T[]>> => {
    const result = await fetcher();
    if (result.data) {
      return { data: result.data.resources, error: null };
    }
    return { data: null, error: result.error };
  };

  return useApiResource(wrappedFetcher, options);
}
