import { ref, type Ref } from "vue";
import type { ApiResult, GeneralError } from "@/api/models/common";

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
  let fetchInProgress = false;

  async function fetch(force = false): Promise<void> {
    const now = Date.now();
    const age = now - lastFetched;
    const isStale = age > cacheMs;

    // Fresh cache — do nothing
    if (!force && data.value !== null && !isStale) {
      return;
    }

    // Prevent parallel fetches
    if (fetchInProgress && !force) {
      return;
    }

    // Stale-while-revalidate: return stale data, refresh in background
    if (staleWhileRevalidate && data.value !== null && isStale && !force) {
      fetchInProgress = true;
      fetcher()
        .then(({ data: newData, error: fetchError }) => {
          fetchInProgress = false;
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
          fetchInProgress = false;
        });
      return;
    }

    // No data or force — wait for load
    fetchInProgress = true;
    loading.value = true;
    error.value = null;

    try {
      const { data: newData, error: fetchError } = await fetcher();

      if (fetchError) {
        error.value = fetchError;
        data.value = null;
        return;
      }

      data.value = newData ?? null;
      lastFetched = Date.now();
    } finally {
      fetchInProgress = false;
      loading.value = false;
    }
  }

  function reset(): void {
    data.value = null;
    error.value = null;
    loading.value = false;
    lastFetched = 0;
    fetchInProgress = false;
  }

  return { data, error, loading, fetch, reset };
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
