<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import { usePulseFilter } from "../model";
import type { PulseSortBy, MinRatingFilter } from "../model";
import { useFilterSearch, useFilterDropdown } from "@/shared/lib/composables";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownItem,
  SortButton,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";

const {
  filterState,
  setSearch,
  setSort,
  setMinRating,
  clearFilters,
} = usePulseFilter();

// Search input (with debounce)
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch
);

// Filter dropdown
const {
  showDropdown,
  closeDropdown,
  toggleDropdown,
} = useFilterDropdown();

// Rating filter options
const ratingOptions = [
  { value: "all", label: "Все", hint: "Любой рейтинг" },
  { value: "1", label: "Положительные", hint: "Рейтинг >= 1" },
  { value: "3", label: "Лучшие", hint: "Рейтинг >= 3" },
];

function handleRatingSelect(value: string) {
  if (value === "all") {
    setMinRating(null);
  } else {
    setMinRating(parseInt(value, 10) as MinRatingFilter);
  }
  closeDropdown();
}

// Sort options
const sortOptions = [
  { value: "lastreview", label: "Последние оцененные", hint: "По дате оценки", defaultDirection: "desc" as const },
  { value: "rating", label: "По рейтингу", hint: "По сумме оценок", defaultDirection: "desc" as const },
];

function handleSortByChange(value: string) {
  const option = sortOptions.find((o) => o.value === value);
  setSort(value as PulseSortBy, option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  setSort(filterState.value.sortBy, order);
}

// Rating bubble
const hasRatingFilter = computed(() => filterState.value.minRating !== null);
const ratingBubbleLabel = computed(() => {
  const rating = filterState.value.minRating;
  if (rating === 1) return "Положительные";
  if (rating === 3) return "Лучшие";
  return "";
});

const hasBubbles = computed(() => hasRatingFilter.value);

function clearAll() {
  localInput.value = "";
  clearFilters();
}
</script>

<template>
  <div class="pulse-filter">
    <!-- Filter bar row -->
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту поста"
        @input="handleInput"
        @blur="applySearch"
      />

      <!-- Rating filter -->
      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton
          label="Рейтинг"
          :active="showDropdown"
          @click="toggleDropdown"
        />

        <FilterDropdown v-if="showDropdown">
          <FilterDropdownItem
            v-for="option in ratingOptions"
            :key="option.value"
            :label="option.label"
            :hint="option.hint"
            @item-select="handleRatingSelect(option.value)"
          />
        </FilterDropdown>
      </div>

      <!-- Sort -->
      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @update:sort-by="handleSortByChange"
        @update:sort-order="handleSortOrderChange"
      />
    </div>

    <!-- Active filters row (bubbles) -->
    <BubblesRow v-if="hasBubbles" @clear-all="clearAll">
      <FilterBubble
        v-if="hasRatingFilter"
        prefix="Рейтинг:"
        :value="ratingBubbleLabel"
        @remove="setMinRating(null)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"
@import "src/assets/styles/Filters"

.pulse-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button
  +filter-responsive

+filter-dropdown-base
</style>
