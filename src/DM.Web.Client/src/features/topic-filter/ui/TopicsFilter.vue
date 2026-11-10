<script setup lang="ts">
import { computed } from "vue";
import { useTopicsFilter, SORT_OPTIONS } from "../model";
import type { TopicsFilterKey } from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import { useAuthorDateFilterBar } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterMenu,
  DateRangePicker,
  SortButton,
  ExpandableBubble,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";

// The profile "Topics" tab implies the author; a deep link with ?authors= still works.
const props = withDefaults(
  defineProps<{
    hideAuthor?: boolean;
  }>(),
  { hideAuthor: false },
);

const filter = useTopicsFilter();
const {
  filterState,
  setSearch,
  addAuthor,
  removeAuthor,
  setDateRange,
  setSort,
  toggleSortOrder,
} = filter;

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown, navigateBack } =
  useFilterDropdown<TopicsFilterKey>();

const filterOptions = computed(() => {
  const items = [
    { key: "author", label: "Автор", hint: "Фильтр по автору топика" },
    {
      key: "dateRange",
      label: "Дата создания",
      hint: "Фильтр по периоду создания",
    },
  ];
  return props.hideAuthor ? items.filter((i) => i.key !== "author") : items;
});

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  if (navPath.value.filter === "author") return "Автор";
  if (navPath.value.filter === "dateRange") return "Дата создания";
  return null;
}

const {
  handleDateApply,
  handleDateClear,
  sortOptions,
  hasDateFilter,
  dateFilterLabel,
  hasBubbles,
  authorsBubbleValues,
  clearAll,
  handleSearchKeydown,
} = useAuthorDateFilterBar(filter, SORT_OPTIONS, localInput, closeDropdown);
</script>

<template>
  <div class="topics-filter">
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по заголовку"
        @input="handleInput"
        @keydown="handleSearchKeydown"
        @blur="applySearch"
      />

      <FilterMenu
        v-model:nav-path="navPath"
        :open="showDropdown"
        :title="getDropdownTitle()"
        :options="filterOptions"
        @toggle="toggleDropdown"
        @close="closeDropdown"
        @back="navigateBack"
      >
        <!-- Include inactive: an author away for 30 days stays findable. -->
        <UserMultiSelect
          v-if="!hideAuthor && navPath?.filter === 'author'"
          :selected-users="filterState.authors"
          placeholder="Поиск автора"
          include-inactive
          @add="addAuthor"
        />

        <DateRangePicker
          v-if="navPath?.filter === 'dateRange'"
          :from-value="filterState.createdFromUtc"
          :to-value="filterState.createdToUtc"
          :show-clear-button="hasDateFilter"
          @apply="handleDateApply"
          @clear="handleDateClear"
        />
      </FilterMenu>

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
        v-if="!hideAuthor && filterState.authors.size > 0"
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

.topics-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar

+dropdown-search-input
+filter-dropdown-base
</style>
