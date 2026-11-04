import { ref, onUnmounted } from "vue";
import { userApi } from "../api";
import type { User, UserActivityFilter } from "@/shared/api/models/community";

export interface UseUserSearchOptions {
  limit?: number;
  debounceMs?: number;
  /** Without it the server shows the active users only. */
  activity?: UserActivityFilter;
}

// The pickers shape the raw results on top of it themselves.
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
    const { data } = await userApi.searchUsers(query, limit, activity);
    results.value = data?.resources ?? [];
    loading.value = false;
  }

  function search(query: string) {
    cancel();
    if (!query.trim()) {
      results.value = [];
      loading.value = false;
      return;
    }
    // At once, so "not found" does not flash before the request.
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
