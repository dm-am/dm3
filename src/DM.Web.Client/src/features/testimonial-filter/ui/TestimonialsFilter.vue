<script setup lang="ts">
import { computed } from "vue";
import { useTestimonialsFilter, SORT_OPTIONS } from "../model";
import { useFilterSearch } from "@/shared/lib/composables";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import type { SortOption } from "@/shared/ui/Filters";

const props = withDefaults(
  defineProps<{
    /** Sort options override (contract for profile pages, e.g. given/received endorsements) */
    sortOptions?: SortOption[];
    /** Search input placeholder override */
    searchPlaceholder?: string;
  }>(),
  {
    sortOptions: undefined,
    searchPlaceholder: "Поиск по тексту или автору",
  },
);

const { filterState, setSearch, setSort, toggleSortOrder } =
  useTestimonialsFilter();

// Search with debounce
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

// Sort
const sortOptions = computed<SortOption[]>(
  () =>
    props.sortOptions ??
    SORT_OPTIONS.map((o) => ({
      value: o.value,
      label: o.label,
      hint: o.hint,
      defaultDirection: o.defaultDirection,
    })),
);

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
  <div class="testimonials-filter">
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        :placeholder="searchPlaceholder"
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
@import "@/assets/styles/Filters"

.testimonials-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-responsive
</style>
