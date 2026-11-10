<script setup lang="ts">
import { computed } from "vue";
import {
  usePollsFilter,
  SORT_OPTIONS,
  STATUS_OPTIONS,
  POLL_TYPE_OPTIONS,
} from "../model";
import type { PollsFilterKey, PollTypeFilter } from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import { formatDateRangeForDisplay } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterMenu,
  OptionsList,
  DateRangePicker,
  SortButton,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import type { PollSortBy, PollStatus } from "@/entities/poll";
import type { SortDirection } from "@/shared/lib/filters/types";

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

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown, navigateBack } =
  useFilterDropdown<PollsFilterKey>();

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

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  if (navPath.value.filter === "status") return "Статус";
  if (navPath.value.filter === "pollType") return "Тип опроса";
  if (navPath.value.filter === "startsRange") return "Дата начала";
  if (navPath.value.filter === "endsRange") return "Дата окончания";
  return null;
}

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

// The cast is safe: SortButton only ever reports back a value it was given.
function handleSortSelect(value: string, direction?: SortDirection) {
  setSort(value as PollSortBy, direction);
}

function handleStatusSelect(value: string) {
  setStatus(value as PollStatus | "");
  closeDropdown();
}

// Copies: the list takes a mutable array, the registry is read-only.
const statusOptions = [...STATUS_OPTIONS];
const pollTypeOptions = [...POLL_TYPE_OPTIONS];

const statusLabel = computed(() => {
  const opt = STATUS_OPTIONS.find((o) => o.value === filterState.value.status);
  return opt?.label || "";
});

function handlePollTypeSelect(value: string) {
  setPollType(value as PollTypeFilter);
  closeDropdown();
}

const pollTypeLabel = computed(() => {
  const opt = POLL_TYPE_OPTIONS.find(
    (o) => o.value === filterState.value.pollType,
  );
  return opt?.label || "";
});

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

const hasStartsFilter = computed(
  () => !!filterState.value.startsFromUtc || !!filterState.value.startsToUtc,
);

const startsFilterLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.startsFromUtc || null,
    to: filterState.value.startsToUtc || null,
  }),
);

const hasEndsFilter = computed(
  () => !!filterState.value.endsFromUtc || !!filterState.value.endsToUtc,
);

const endsFilterLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.endsFromUtc || null,
    to: filterState.value.endsToUtc || null,
  }),
);

// A non-default sort alone must not show a row with a lone "Сбросить".
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
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по названию и описанию"
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
        <OptionsList
          v-if="navPath?.filter === 'status'"
          :options="statusOptions"
          @select="handleStatusSelect"
        />

        <OptionsList
          v-if="navPath?.filter === 'pollType'"
          :options="pollTypeOptions"
          @select="handlePollTypeSelect"
        />

        <DateRangePicker
          v-if="navPath?.filter === 'startsRange'"
          :from-value="filterState.startsFromUtc || null"
          :to-value="filterState.startsToUtc || null"
          :show-clear-button="hasStartsFilter"
          allow-future
          @apply="handleStartsApply"
          @clear="handleStartsClear"
        />

        <DateRangePicker
          v-if="navPath?.filter === 'endsRange'"
          :from-value="filterState.endsFromUtc || null"
          :to-value="filterState.endsToUtc || null"
          :show-clear-button="hasEndsFilter"
          allow-future
          @apply="handleEndsApply"
          @clear="handleEndsClear"
        />
      </FilterMenu>

      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @sort-select="handleSortSelect"
        @update:sort-order="toggleSortOrder"
      />
    </div>

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
@use "@/assets/styles/Filters" as *

.polls-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar

+filter-dropdown-base
</style>
