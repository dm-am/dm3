<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import { useCommentsFilter } from "../model/useCommentsFilter";
import { SORT_OPTIONS } from "../model/types";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
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

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown, navigateBack } =
  useFilterDropdown();

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

function handleDateApply(from: string | null, to: string | null) {
  setDateRange(from, to);
  closeDropdown();
}

function handleDateClear() {
  setDateRange(null, null);
  closeDropdown();
}

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

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

// The sort is the SortButton's: it does not raise the "Сбросить" row.
const hasBubbles = computed(() => {
  const state = filterState.value;
  return state.authors.size > 0 || hasDateFilter.value;
});

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
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту"
        @input="handleInput"
        @keydown="handleSearchKeydown"
        @blur="applySearch"
      />

      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton
          :active="showDropdown"
          @click="toggleDropdown"
          @close="closeDropdown"
        />

        <FilterDropdown v-if="showDropdown" @close="closeDropdown">
          <FilterDropdownHeader
            v-if="navPath"
            :title="getDropdownTitle() ?? ''"
            @back="navigateBack"
          />

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

          <!-- Include inactive: an author away for 30 days stays findable. -->
          <UserMultiSelect
            v-if="navPath?.filter === 'author'"
            :selected-users="filterState.authors"
            placeholder="Поиск автора"
            include-inactive
            @add="addAuthor"
            @remove="removeAuthor"
          />

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

      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @sort-select="setSort"
        @update:sort-order="toggleSortOrder"
      />
    </div>

    <BubblesRow v-if="hasBubbles" @clear-all="clearAll">
      <ExpandableBubble
        v-if="filterState.authors.size > 0"
        :prefix="filterState.authors.size > 1 ? 'Авторы:' : 'Автор:'"
        :values="authorsBubbleValues"
        :max-visible="1"
        @remove="removeAuthor"
      />

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
@use "@/assets/styles/Filters" as *

.comments-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button

+dropdown-search-input
+filter-dropdown-base
</style>
