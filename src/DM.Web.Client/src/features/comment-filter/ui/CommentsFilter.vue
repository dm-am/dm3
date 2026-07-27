<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import { useCommentsFilter, SORT_OPTIONS } from "../";
import { useFilterSearch, useFilterDropdown } from "@/shared/lib/composables";
import { formatDateRangeForDisplay } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
  FilterDropdownItem,
  DateRangePicker,
  SortButton,
  ExpandableBubble,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";

const {
  filterState,
  setSearch,
  addAuthor,
  removeAuthor,
  setDateRange,
  setSort,
  toggleSortOrder,
  clearFilters,
} = useCommentsFilter();

// =============================================================================
// SEARCH INPUT (with debounce)
// =============================================================================

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

// =============================================================================
// FILTER DROPDOWN (with navigation)
// =============================================================================

const {
  showDropdown,
  navPath,
  closeDropdown: closeDropdownBase,
  toggleDropdown,
  navigateBack,
} = useFilterDropdown();

function closeDropdown() {
  closeDropdownBase();
}

// Root level filter options
const filterOptions = [
  { key: "author", label: "Автор", hint: "Фильтр по автору комментария" },
  {
    key: "dateRange",
    label: "Дата создания",
    hint: "Фильтр по периоду создания",
  },
];

function selectRootItem(key: string) {
  navPath.value = { filter: key };
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  if (navPath.value.filter === "author") return "Автор";
  if (navPath.value.filter === "dateRange") return "Дата создания";
  return null;
}

// =============================================================================
// AUTHOR MULTI-SELECT
// =============================================================================

function handleAddAuthor(username: string) {
  addAuthor(username);
}

// =============================================================================
// DATE RANGE
// =============================================================================

function handleDateApply(from: string | null, to: string | null) {
  setDateRange(from, to);
  closeDropdown();
}

function handleDateClear() {
  setDateRange(null, null);
  closeDropdown();
}

// =============================================================================
// SORT
// =============================================================================

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

function handleSortByChange(value: string) {
  const option = SORT_OPTIONS.find((o) => o.value === value);
  setSort(value as any, option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  if (order !== filterState.value.sortOrder) {
    toggleSortOrder();
  }
}

// =============================================================================
// BUBBLES & DISPLAY
// =============================================================================

const hasDateFilter = computed(() => {
  return (
    filterState.value.createdFromUtc !== null ||
    filterState.value.createdToUtc !== null
  );
});

const dateFilterLabel = computed(() => {
  return formatDateRangeForDisplay({
    from: filterState.value.createdFromUtc,
    to: filterState.value.createdToUtc,
  });
});

// Sort is driven by the SortButton, not the filter bubbles — it must NOT
// trigger the "Сбросить" row (consistent with Games / Pulse / Polls).
const hasBubbles = computed(() => {
  const state = filterState.value;
  return state.authors.size > 0 || hasDateFilter.value;
});

// Authors bubble values
const authorsBubbleValues = computed(() =>
  [...filterState.value.authors]
    .sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase(), "ru"))
    .map((author) => ({ id: author, label: author })),
);

function handleRemoveAuthor(id: string) {
  removeAuthor(id);
}

function clearAll() {
  localInput.value = "";
  clearFilters();
}

// Search input keyboard handler (for Backspace to remove filters)
function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    if (filterState.value.createdFromUtc || filterState.value.createdToUtc) {
      event.preventDefault();
      setDateRange(null, null);
      return;
    }
    if (filterState.value.authors.size > 0) {
      event.preventDefault();
      const lastAuthor = [...filterState.value.authors].pop();
      if (lastAuthor) removeAuthor(lastAuthor);
      return;
    }
  }
}
</script>

<template>
  <div class="comments-filter">
    <!-- Filter bar row -->
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту"
        @input="handleInput"
        @keydown="handleSearchKeydown"
        @blur="applySearch"
      />

      <!-- Filter button -->
      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton
          :active="showDropdown"
          @click="toggleDropdown"
          @close="closeDropdown"
        />

        <FilterDropdown v-if="showDropdown" @close="closeDropdown">
          <!-- Navigation header when inside a sub-level -->
          <FilterDropdownHeader
            v-if="navPath"
            :title="getDropdownTitle() ?? ''"
            @back="navigateBack"
          />

          <!-- Root level items -->
          <template v-if="!navPath">
            <FilterDropdownItem
              v-for="item in filterOptions"
              :key="item.key"
              :label="item.label"
              :hint="item.hint"
              has-sub-options
              @item-select="selectRootItem(item.key)"
            />
          </template>

          <!-- Author multi-select -->
          <UserMultiSelect
            v-if="navPath?.filter === 'author'"
            :selected-users="filterState.authors"
            placeholder="Поиск автора"
            @add="handleAddAuthor"
            @remove="handleRemoveAuthor"
          />

          <!-- Date range picker -->
          <DateRangePicker
            v-if="navPath?.filter === 'dateRange'"
            :from-value="filterState.createdFromUtc"
            :to-value="filterState.createdToUtc"
            :show-clear-button="hasDateFilter"
            @apply="handleDateApply"
            @clear="handleDateClear"
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
      <!-- Expandable authors bubble -->
      <ExpandableBubble
        v-if="filterState.authors.size > 0"
        :prefix="filterState.authors.size > 1 ? 'Авторы:' : 'Автор:'"
        :values="authorsBubbleValues"
        :max-visible="1"
        @remove="handleRemoveAuthor"
      />

      <!-- Date filter bubble -->
      <FilterBubble
        v-if="hasDateFilter"
        prefix="Дата:"
        :value="dateFilterLabel"
        @remove="setDateRange(null, null)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

.comments-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button
  +filter-responsive

// Search input inside dropdown
+dropdown-search-input
+filter-dropdown-base
</style>
