<script setup lang="ts">
import { computed } from "vue";
import { useTestimonialsFilter, SORT_OPTIONS } from "../model";
import type { TestimonialSortBy } from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import type { SortOption } from "@/shared/ui/Filters";
import type { SortDirection } from "@/shared/lib/filters/types";

const props = withDefaults(
  defineProps<{
    /** For profile pages (given and received endorsements). */
    sortOptions?: SortOption[];
    searchPlaceholder?: string;
  }>(),
  {
    searchPlaceholder: "Поиск по тексту или автору",
  },
);

const { filterState, setSearch, setSort, toggleSortOrder } =
  useTestimonialsFilter();

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const options = computed<SortOption[]>(
  () =>
    props.sortOptions ??
    SORT_OPTIONS.map((o) => ({
      value: o.value,
      label: o.label,
      hint: o.hint,
      defaultDirection: o.defaultDirection,
    })),
);

// The direction rides with the clicked option, so an override needs no directions of its own.
function handleSortSelect(value: string, direction?: SortDirection) {
  setSort(value as TestimonialSortBy, direction);
}
</script>

<template>
  <div class="testimonials-filter">
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        :placeholder="searchPlaceholder"
        @input="handleInput"
        @keydown.enter="applySearch"
        @blur="applySearch"
      />

      <!-- The sort alone drives the order: no bubble row. -->
      <SortButton
        :options="options"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @sort-select="handleSortSelect"
        @update:sort-order="toggleSortOrder"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.testimonials-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
</style>
