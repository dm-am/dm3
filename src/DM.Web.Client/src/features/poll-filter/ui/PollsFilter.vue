<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import {
  usePollsFilter,
  SORT_OPTIONS,
  STATUS_OPTIONS,
  POLL_TYPE_OPTIONS,
} from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import { formatDateRangeForDisplay } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
  FilterDropdownItem,
  OptionsList,
  DateRangePicker,
  SortButton,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import type { PollStatus } from "@/entities/poll";

const {
  filterState,
  setStatus,
  setPollType,
  setSearch,
  setStartsFromUtc,
  setStartsToUtc,
  setEndsFromUtc,
  setEndsToUtc,
  setSort,
  toggleSortOrder,
  clearFilters,
} = usePollsFilter();

// Search with debounce
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
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
  {
    key: "endsRange",
    label: "Дата окончания",
    hint: "Фильтр по дате окончания",
  },
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

// The cast is safe: SortButton only ever reports back a value it was given.
function handleSortSelect(value: string, direction?: "asc" | "desc") {
  setSort(value as "starts" | "ends" | "status", direction);
}

// Status handling
function handleStatusSelect(value: string) {
  setStatus(value as PollStatus | "");
  closeDropdown();
}

function getStatusHint(value: string): string {
  switch (value) {
    case "":
      return "Все опросы";
    case "Pending":
      return "Еще не начались";
    case "Active":
      return "Сейчас идут";
    case "Closed":
      return "Уже закончились";
    default:
      return "";
  }
}

// Status options with hints (for OptionsList)
const statusOptions = computed(() =>
  STATUS_OPTIONS.map((o) => ({ ...o, hint: getStatusHint(o.value) })),
);

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
    case "":
      return "Все опросы";
    case "anonymous":
      return "Голоса скрыты";
    case "public":
      return "Видно кто голосовал";
    default:
      return "";
  }
}

// Poll type options with hints (for OptionsList)
const pollTypeOptions = computed(() =>
  POLL_TYPE_OPTIONS.map((o) => ({ ...o, hint: getPollTypeHint(o.value) })),
);

// Poll type label for bubble
const pollTypeLabel = computed(() => {
  const opt = POLL_TYPE_OPTIONS.find(
    (o) => o.value === filterState.value.pollType,
  );
  return opt?.label || "";
});

// Date range handlers
function handleStartsApply(from: string | null, to: string | null) {
  setStartsFromUtc(from || "");
  setStartsToUtc(to || "");
  closeDropdown();
}

function handleStartsClear() {
  setStartsFromUtc("");
  setStartsToUtc("");
  closeDropdown();
}

function handleEndsApply(from: string | null, to: string | null) {
  setEndsFromUtc(from || "");
  setEndsToUtc(to || "");
  closeDropdown();
}

function handleEndsClear() {
  setEndsFromUtc("");
  setEndsToUtc("");
  closeDropdown();
}

// Date range display
const hasStartsFilter = computed(() => {
  return !!filterState.value.startsFromUtc || !!filterState.value.startsToUtc;
});

const startsFilterLabel = computed(() => {
  return formatDateRangeForDisplay({
    from: filterState.value.startsFromUtc || null,
    to: filterState.value.startsToUtc || null,
  });
});

const hasEndsFilter = computed(() => {
  return !!filterState.value.endsFromUtc || !!filterState.value.endsToUtc;
});

const endsFilterLabel = computed(() => {
  return formatDateRangeForDisplay({
    from: filterState.value.endsFromUtc || null,
    to: filterState.value.endsToUtc || null,
  });
});

// Has bubbles (only when at least one actual filter bubble is rendered;
// non-default sorting alone must not show a row with a lone "Сбросить")
const hasBubbles = computed(() => {
  const state = filterState.value;
  return (
    state.status !== "" ||
    state.pollType !== "" ||
    hasStartsFilter.value ||
    hasEndsFilter.value
  );
});

function clearAll() {
  localInput.value = "";
  clearFilters();
}

function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    const state = filterState.value;
    if (hasEndsFilter.value) {
      event.preventDefault();
      setEndsFromUtc("");
      setEndsToUtc("");
      return;
    }
    if (hasStartsFilter.value) {
      event.preventDefault();
      setStartsFromUtc("");
      setStartsToUtc("");
      return;
    }
    if (state.pollType !== "") {
      event.preventDefault();
      setPollType("");
      return;
    }
    if (state.status !== "") {
      event.preventDefault();
      setStatus("");
    }
  }
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

          <!-- Status options -->
          <OptionsList
            v-if="navPath?.filter === 'status'"
            :options="statusOptions"
            @select="handleStatusSelect"
          />

          <!-- Poll type options -->
          <OptionsList
            v-if="navPath?.filter === 'pollType'"
            :options="pollTypeOptions"
            @select="handlePollTypeSelect"
          />

          <!-- Starts date range picker -->
          <DateRangePicker
            v-if="navPath?.filter === 'startsRange'"
            :from-value="filterState.startsFromUtc || null"
            :to-value="filterState.startsToUtc || null"
            :show-clear-button="hasStartsFilter"
            @apply="handleStartsApply"
            @clear="handleStartsClear"
          />

          <!-- Ends date range picker -->
          <DateRangePicker
            v-if="navPath?.filter === 'endsRange'"
            :from-value="filterState.endsFromUtc || null"
            :to-value="filterState.endsToUtc || null"
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
        @sort-select="handleSortSelect"
        @update:sort-order="toggleSortOrder"
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
@import "@/assets/styles/Filters"

.polls-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button
  +filter-responsive

+filter-dropdown-base
</style>
