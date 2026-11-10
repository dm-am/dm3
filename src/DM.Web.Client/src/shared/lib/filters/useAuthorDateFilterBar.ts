/**
 * The bar over an author and date filter (createAuthorDateFilter): the date
 * level of its menu, its sort options, its bubbles, the reset and Backspace in
 * the empty search, which takes the last filter off.
 */
import { computed, type Ref } from "vue";
import type { AuthorDateFilter } from "./createAuthorDateFilter";
import type { SortOption } from "./types";
import { formatDateRangeForDisplay } from "./values";

export function useAuthorDateFilterBar<TSortBy extends string>(
  filter: AuthorDateFilter<TSortBy>,
  sortOptionsOfList: readonly SortOption[],
  localInput: Ref<string>,
  closeDropdown: () => void,
) {
  const { filterState, removeAuthor, setDateRange, clearFilters } = filter;

  function handleDateApply(from: string | null, to: string | null) {
    setDateRange(from, to);
    closeDropdown();
  }

  function handleDateClear() {
    setDateRange(null, null);
    closeDropdown();
  }

  const sortOptions = sortOptionsOfList.map((o) => ({
    value: o.value,
    label: o.label,
    hint: o.hint,
    defaultDirection: o.defaultDirection,
  }));

  const hasDateFilter = computed(
    () =>
      filterState.value.createdFromUtc !== null ||
      filterState.value.createdToUtc !== null,
  );

  const dateFilterLabel = computed(() =>
    formatDateRangeForDisplay({
      from: filterState.value.createdFromUtc,
      to: filterState.value.createdToUtc,
    }),
  );

  // The sort is the SortButton's: it does not raise the "Сбросить" row.
  const hasBubbles = computed(
    () => filterState.value.authors.size > 0 || hasDateFilter.value,
  );

  const authorsBubbleValues = computed(() =>
    [...filterState.value.authors]
      .sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase(), "ru"))
      .map((author) => ({ id: author, label: author })),
  );

  function clearAll() {
    localInput.value = "";
    clearFilters();
  }

  function handleSearchKeydown(event: KeyboardEvent) {
    if (event.key !== "Backspace" || localInput.value) return;
    if (filterState.value.createdFromUtc || filterState.value.createdToUtc) {
      event.preventDefault();
      setDateRange(null, null);
      return;
    }
    if (filterState.value.authors.size > 0) {
      event.preventDefault();
      const lastAuthor = [...filterState.value.authors].pop();
      if (lastAuthor) removeAuthor(lastAuthor);
    }
  }

  return {
    handleDateApply,
    handleDateClear,
    sortOptions,
    hasDateFilter,
    dateFilterLabel,
    hasBubbles,
    authorsBubbleValues,
    clearAll,
    handleSearchKeydown,
  };
}
