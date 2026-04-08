<script setup lang="ts">
import { computed } from "vue";
import { useReviewsFilter, SORT_OPTIONS, DEFAULT_FILTER_STATE } from "../model";
import { useFilterSearch } from "@/shared/lib/composables";
import { FilterSearchInput, SortButton, BubblesRow, FilterBubble } from "@/shared/ui/Filters";

const { filterState, setSearch, setSort, toggleSortOrder, clearFilters } = useReviewsFilter();

// Search with debounce
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch
);

// Sort
const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

function handleSortByChange(value: string) {
  const option = SORT_OPTIONS.find((o) => o.value === value);
  setSort(value as "created" | "author", option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  if (order !== filterState.value.sortOrder) {
    toggleSortOrder();
  }
}

// Show bubbles when any filter or sort is non-default
const hasBubbles = computed(() => {
  const state = filterState.value;
  const def = DEFAULT_FILTER_STATE;
  return (
    state.search !== "" ||
    state.sortBy !== def.sortBy ||
    state.sortOrder !== def.sortOrder
  );
});

function clearAll() {
  localInput.value = "";
  clearFilters();
}
</script>

<template>
  <div class="reviews-filter">
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту или автору"
        @input="handleInput"
        @keydown.enter="applySearch"
        @blur="applySearch"
      />

      <!-- Sort -->
      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @update:sort-by="handleSortByChange"
        @update:sort-order="handleSortOrderChange"
      />
    </div>

    <!-- Active filter bubbles -->
    <BubblesRow v-if="hasBubbles" @clear-all="clearAll">
      <FilterBubble
        v-if="filterState.search"
        prefix="Поиск:"
        :value="filterState.search"
        @remove="setSearch('')"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

.reviews-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-responsive
</style>
