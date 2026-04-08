<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import { usePollsFilter, SORT_OPTIONS, STATUS_OPTIONS, POLL_TYPE_OPTIONS, DEFAULT_FILTER_STATE } from "../model";
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
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import type { PollStatus } from "@/entities/poll";

const {
  filterState,
  hasActiveFilters,
  setStatus,
  setPollType,
  setSearch,
  setStartsFrom,
  setStartsTo,
  setEndsFrom,
  setEndsTo,
  setSort,
  toggleSortOrder,
  clearFilters,
} = usePollsFilter();

// Search with debounce
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch
);

// Filter dropdown with navigation
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
  { key: "status", label: "Статус", hint: "Активные или завершенные" },
  { key: "pollType", label: "Тип опроса", hint: "Анонимные или публичные" },
  { key: "startsRange", label: "Дата начала", hint: "Фильтр по дате начала" },
  { key: "endsRange", label: "Дата окончания", hint: "Фильтр по дате окончания" },
];

function selectRootItem(key: string) {
  navPath.value = { filter: key };
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  if (navPath.value.filter === "status") return "Статус";
  if (navPath.value.filter === "pollType") return "Тип опроса";
  if (navPath.value.filter === "startsRange") return "Дата начала";
  if (navPath.value.filter === "endsRange") return "Дата окончания";
  return null;
}

// Sort
const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

function handleSortByChange(value: string) {
  const option = SORT_OPTIONS.find((o) => o.value === value);
  setSort(value as "starts" | "ends" | "status", option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  if (order !== filterState.value.sortOrder) {
    toggleSortOrder();
  }
}

// Status handling
function handleStatusSelect(value: string) {
  setStatus(value as PollStatus | "");
  closeDropdown();
}

function getStatusHint(value: string): string {
  switch (value) {
    case "": return "Все опросы";
    case "Pending": return "Еще не начались";
    case "Active": return "Сейчас идут";
    case "Closed": return "Уже закончились";
    default: return "";
  }
}

// Status label for bubble
const statusLabel = computed(() => {
  const opt = STATUS_OPTIONS.find((o) => o.value === filterState.value.status);
  return opt?.label || "";
});

// Poll type handling
function handlePollTypeSelect(value: string) {
  setPollType(value as "anonymous" | "public" | "");
  closeDropdown();
}

function getPollTypeHint(value: string): string {
  switch (value) {
    case "": return "Все опросы";
    case "anonymous": return "Голоса скрыты";
    case "public": return "Видно кто голосовал";
    default: return "";
  }
}

// Poll type label for bubble
const pollTypeLabel = computed(() => {
  const opt = POLL_TYPE_OPTIONS.find((o) => o.value === filterState.value.pollType);
  return opt?.label || "";
});

// Date range handlers
function handleStartsApply(from: string | null, to: string | null) {
  setStartsFrom(from || "");
  setStartsTo(to || "");
  closeDropdown();
}

function handleStartsClear() {
  setStartsFrom("");
  setStartsTo("");
  closeDropdown();
}

function handleEndsApply(from: string | null, to: string | null) {
  setEndsFrom(from || "");
  setEndsTo(to || "");
  closeDropdown();
}

function handleEndsClear() {
  setEndsFrom("");
  setEndsTo("");
  closeDropdown();
}

// Date range display
const hasStartsFilter = computed(() => {
  return !!filterState.value.startsFrom || !!filterState.value.startsTo;
});

const startsFilterLabel = computed(() => {
  return formatDateRangeForDisplay({
    from: filterState.value.startsFrom || null,
    to: filterState.value.startsTo || null,
  });
});

const hasEndsFilter = computed(() => {
  return !!filterState.value.endsFrom || !!filterState.value.endsTo;
});

const endsFilterLabel = computed(() => {
  return formatDateRangeForDisplay({
    from: filterState.value.endsFrom || null,
    to: filterState.value.endsTo || null,
  });
});

// Has bubbles (show clear all when any filter OR non-default sorting)
const hasBubbles = computed(() => {
  const state = filterState.value;
  const def = DEFAULT_FILTER_STATE;
  return (
    state.status !== "" ||
    state.pollType !== "" ||
    state.search !== "" ||
    hasStartsFilter.value ||
    hasEndsFilter.value ||
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
  <div class="polls-filter">
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по названию и описанию"
        @input="handleInput"
        @keydown.enter="applySearch"
        @blur="applySearch"
      />

      <!-- Filter button -->
      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton :active="showDropdown" @click="toggleDropdown" />

        <FilterDropdown v-if="showDropdown">
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

          <!-- Status options -->
          <template v-if="navPath?.filter === 'status'">
            <FilterDropdownItem
              v-for="option in STATUS_OPTIONS"
              :key="option.value"
              :label="option.label"
              :hint="getStatusHint(option.value)"
              @item-select="handleStatusSelect(option.value)"
            />
          </template>

          <!-- Poll type options -->
          <template v-if="navPath?.filter === 'pollType'">
            <FilterDropdownItem
              v-for="option in POLL_TYPE_OPTIONS"
              :key="option.value"
              :label="option.label"
              :hint="getPollTypeHint(option.value)"
              @item-select="handlePollTypeSelect(option.value)"
            />
          </template>

          <!-- Starts date range picker -->
          <DateRangePicker
            v-if="navPath?.filter === 'startsRange'"
            :from-value="filterState.startsFrom || null"
            :to-value="filterState.startsTo || null"
            :show-clear-button="hasStartsFilter"
            @apply="handleStartsApply"
            @clear="handleStartsClear"
          />

          <!-- Ends date range picker -->
          <DateRangePicker
            v-if="navPath?.filter === 'endsRange'"
            :from-value="filterState.endsFrom || null"
            :to-value="filterState.endsTo || null"
            :show-clear-button="hasEndsFilter"
            @apply="handleEndsApply"
            @clear="handleEndsClear"
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

    <!-- Active filter bubbles -->
    <BubblesRow v-if="hasBubbles" @clear-all="clearAll">
      <FilterBubble
        v-if="filterState.status"
        prefix="Статус:"
        :value="statusLabel"
        @remove="setStatus('')"
      />
      <FilterBubble
        v-if="filterState.pollType"
        prefix="Тип:"
        :value="pollTypeLabel"
        @remove="setPollType('')"
      />
      <FilterBubble
        v-if="filterState.search"
        prefix="Поиск:"
        :value="filterState.search"
        @remove="setSearch('')"
      />
      <FilterBubble
        v-if="hasStartsFilter"
        prefix="Начало:"
        :value="startsFilterLabel"
        @remove="handleStartsClear"
      />
      <FilterBubble
        v-if="hasEndsFilter"
        prefix="Окончание:"
        :value="endsFilterLabel"
        @remove="handleEndsClear"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"
@import "src/assets/styles/Filters"

.polls-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button
  +filter-responsive

+filter-dropdown-base
</style>
