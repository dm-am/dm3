import { ref, watch, onMounted, onUnmounted, type Ref } from "vue";
import { SEARCH_DEBOUNCE_MS } from "@/shared/lib/constants/search";

/**
 * A filter's search input with a debounce (UI_STANDARDS/controls.md, "Фильтры и выпадающие панели").
 *
 * @param filterSearch - Ref to the current search value from filter state
 * @param setSearch - Function to update the search in filter state
 * @param debounceMs - Debounce delay in ms (default: SEARCH_DEBOUNCE_MS)
 */
export function useFilterSearch(
  filterSearch: Ref<string>,
  setSearch: (value: string) => void,
  debounceMs = SEARCH_DEBOUNCE_MS,
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

  /** On blur or Enter. */
  function applySearch() {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
    if (localInput.value !== filterSearch.value) {
      setSearch(localInput.value);
    }
  }

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
