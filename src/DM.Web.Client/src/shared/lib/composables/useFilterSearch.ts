import { ref, watch, onMounted, onUnmounted, type Ref } from "vue";

/**
 * Composable for filter search input with debounce.
 * Handles:
 * - Local input state synced with filter state
 * - 300ms debounce per PERFORMANCE.md
 * - Timer cleanup on unmount
 *
 * @param filterSearch - Ref to the current search value from filter state
 * @param setSearch - Function to update the search in filter state
 * @param debounceMs - Debounce delay in ms (default: 300)
 */
export function useFilterSearch(
  filterSearch: Ref<string>,
  setSearch: (value: string) => void,
  debounceMs = 300
) {
  const localInput = ref("");
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  // Sync local input with filter state on mount
  onMounted(() => {
    localInput.value = filterSearch.value;
  });

  // Keep localInput synced when filter state changes externally
  watch(filterSearch, (newSearch) => {
    localInput.value = newSearch;
  });

  /**
   * Handle input with debounce
   */
  function handleInput() {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }
    debounceTimer = setTimeout(() => {
      if (localInput.value !== filterSearch.value) {
        setSearch(localInput.value);
      }
    }, debounceMs);
  }

  /**
   * Apply search immediately (on blur or Enter)
   */
  function applySearch() {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
    if (localInput.value !== filterSearch.value) {
      setSearch(localInput.value);
    }
  }

  /**
   * Clear the search input
   */
  function clearSearch() {
    localInput.value = "";
    setSearch("");
  }

  // Cleanup timer on unmount
  onUnmounted(() => {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }
  });

  return {
    localInput,
    handleInput,
    applySearch,
    clearSearch,
  };
}
