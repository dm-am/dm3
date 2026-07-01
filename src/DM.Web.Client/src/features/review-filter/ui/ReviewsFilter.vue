<script setup lang="ts">
import { computed } from "vue";
import { useReviewsFilter, SORT_OPTIONS } from "../model";
import { useFilterSearch } from "@/shared/lib/composables";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";

const { filterState, setSearch, setSort, toggleSortOrder } = useReviewsFilter();

// Search with debounce
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
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

      <!-- Sort (sorting is driven solely by this control, no bubble row) -->
      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @update:sort-by="handleSortByChange"
        @update:sort-order="handleSortOrderChange"
      />
    </div>
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
