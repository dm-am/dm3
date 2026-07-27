import { ref, onUnmounted } from "vue";
import { communityApi } from "@/shared/api";
import type { User, UserActivityFilter } from "@/shared/api/models/community";

export interface UseUserSearchOptions {
  /** Page size requested from the API (default 6) */
  limit?: number;
  /** Debounce delay in ms before firing the request (default 150) */
  debounceMs?: number;
  /**
   * Activity filter. When omitted the request sends no `activity` param and
   * the backend applies its default Active-only filter.
   */
  activity?: UserActivityFilter;
}

/**
 * useUserSearch — debounced username lookup shared by the user pickers
 * (single UserAutocomplete, multi UserMultiSelect). Owns the debounce timer,
 * the raw `User[]` results, and the in-flight `loading` flag; the pickers do
 * their own shaping (mapping / filtering already-selected / slicing) on top.
 */
export function useUserSearch(options: UseUserSearchOptions = {}) {
  const { limit = 6, debounceMs = 150, activity } = options;

  const results = ref<User[]>([]);
  const loading = ref(false);
  let timer: ReturnType<typeof setTimeout> | null = null;

  function cancel() {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
  }

  async function run(query: string) {
    try {
      // Keep the request to two args when no activity filter is set, so the
      // backend default (Active-only) applies unchanged.
      const { data } =
        activity !== undefined
          ? await communityApi.searchUsers(query, limit, activity)
          : await communityApi.searchUsers(query, limit);
      results.value = data?.resources ?? [];
    } catch (error) {
      console.error("User search failed:", error);
      results.value = [];
    } finally {
      loading.value = false;
    }
  }

  function search(query: string) {
    cancel();
    if (!query.trim()) {
      results.value = [];
      loading.value = false;
      return;
    }
    // Set loading immediately to avoid a "not found" flash before the request.
    loading.value = true;
    timer = setTimeout(() => run(query), debounceMs);
  }

  function clear() {
    cancel();
    results.value = [];
    loading.value = false;
  }

  onUnmounted(cancel);

  return { results, loading, search, clear, cancel };
}
