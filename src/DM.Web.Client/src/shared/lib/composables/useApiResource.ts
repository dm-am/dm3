import { ref, type Ref } from "vue";
import type { ApiResult, GeneralError } from "@/shared/api/models/common";
import { createFreshness } from "@/shared/lib/utils/keyedCache";
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
 * API data with a TTL cache, stale-while-revalidate and one fetch at a time.
 *
 * @example
 * ```ts
 * const { data: games, fetch, reset } = useApiResource(
 *   () => gameCatalogApi.getParticipatingGames(),
 *   { cacheMs: 60_000 }
 * );
 *
 * // In component:
 * onMounted(() => fetch());
 * ```
 */
export function useApiResource<T>(
  fetcher: (fresh?: boolean) => Promise<ApiResult<T>>,
  options: UseApiResourceOptions = {},
): UseApiResourceReturn<T> {
  const { cacheMs = 60_000, staleWhileRevalidate = true } = options;

  const data = ref<T | null>(null) as Ref<T | null>;
  const error = ref<GeneralError | null>(null);
  const loading = ref(false);

  // Freshness from keyedCache, the one implementation of it; the payload lives
  // in the data ref, which reset() clears.
  const freshness = createFreshness(cacheMs);

  // A slow answer must not overwrite a forced one or revive what reset() cleared.
  const guard = createRequestGuard();

  // A second fetch() joins the one in flight, so `await fetch()` means data.
  let blockingInFlight: Promise<void> | null = null;

  // Background refreshes are never joined: that would make them block.
  let backgroundInFlight = false;

  async function fetch(force = false): Promise<void> {
    const isStale = freshness.isStale();

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
          // Lowered before the staleness check: a bumped guard left the flag
          // raised for good, and the resource never refreshed again.
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
            freshness.touch();
          }
        })
        .catch(() => {
          backgroundInFlight = false;
        });
      return;
    }

    // Set before the first await, so a caller rendering right after sees the spinner.
    const requestId = guard.next();
    loading.value = true;
    error.value = null;

    // Handed to the fetcher: a forced read of a `public, max-age=300` endpoint
    // must also bypass the browser's and the shared cache.
    const run = (async () => {
      try {
        const { data: newData, error: fetchError } = await fetcher(force);
        if (!guard.isCurrent(requestId)) return;

        if (fetchError) {
          error.value = fetchError;
          data.value = null;
          return;
        }

        data.value = newData ?? null;
        freshness.touch();
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

  /** Fetches again after a mutation without blanking the data first, as reset() does. */
  function invalidate(): Promise<void> {
    freshness.expire();

    // Nothing on screen to correct, and the next fetch reads fresh anyway.
    if (data.value === null) return Promise.resolve();

    return fetch(true);
  }

  function reset(): void {
    // A logout: answers still in flight are dropped.
    guard.next();
    data.value = null;
    error.value = null;
    loading.value = false;
    freshness.clear();
    blockingInFlight = null;
    backgroundInFlight = false;
  }

  return { data, error, loading, fetch, invalidate, reset };
}

/** For list endpoints: extracts .resources of the ListEnvelope. */
export function useApiList<T>(
  fetcher: (fresh?: boolean) => Promise<ApiResult<{ resources: T[] }>>,
  options: UseApiResourceOptions = {},
): UseApiResourceReturn<T[]> {
  const wrappedFetcher = async (fresh?: boolean): Promise<ApiResult<T[]>> => {
    const result = await fetcher(fresh);
    if (result.data) {
      return { data: result.data.resources, error: null };
    }
    return { data: null, error: result.error };
  };

  return useApiResource(wrappedFetcher, options);
}
