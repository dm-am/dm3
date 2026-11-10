<script setup lang="ts">
import { computed } from "vue";
import { useBlogsFilter, STATUS_OPTIONS, SORT_OPTIONS } from "../model";
import type { BlogsFilterKey, StatusFilter } from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import { formatDateRangeForDisplay } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterMenu,
  FilterDropdownItem,
  DateRangePicker,
  OptionsList,
  SortButton,
  ExpandableBubble,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";

const {
  filterState,
  setSearch,
  setStatus,
  addHost,
  removeHost,
  setCreatedRange,
  setActivatedRange,
  setClosedRange,
  setSort,
  toggleSortOrder,
  clearFilters,
} = useBlogsFilter();

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const {
  showDropdown,
  navPath,
  closeDropdown,
  toggleDropdown,
  navigateBack: navigateBackBase,
} = useFilterDropdown<BlogsFilterKey>();

function navigateBack() {
  // From level 3 (date range input) back to the choice of the date type.
  if (navPath.value?.group) {
    navPath.value = { filter: navPath.value.filter };
    return;
  }
  navigateBackBase();
}

const filterOptions = [
  { key: "status", label: "Статус блога", hint: "Оформляется, Открыт, Закрыт" },
  { key: "host", label: "Ведущие", hint: "Автор или ассистент" },
  { key: "dates", label: "Даты", hint: "Фильтр по датам" },
];

const dateTypeOptions = [
  { value: "created", label: "Создание блога", hint: "По дате создания блога" },
  {
    value: "activated",
    label: "Открытие блога",
    hint: "По дате открытия блога",
  },
  { value: "closed", label: "Закрытие блога", hint: "По дате закрытия блога" },
];

const statusListOptions = STATUS_OPTIONS.filter((o) => o.value !== "any").map(
  (o) => ({
    value: o.value,
    label: o.label,
    hint: o.hint,
  }),
);

function selectDateType(value: string) {
  navPath.value = { filter: "dates", group: value };
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  if (navPath.value.filter === "status") return "Статус блога";
  if (navPath.value.filter === "host") return "Ведущие";
  if (navPath.value.filter === "dates") {
    if (navPath.value.group === "created") return "Создание блога";
    if (navPath.value.group === "activated") return "Открытие блога";
    if (navPath.value.group === "closed") return "Закрытие блога";
    return "Даты";
  }
  return null;
}

function handleStatusSelect(value: string) {
  setStatus(value as StatusFilter);
  closeDropdown();
}

function getCurrentDateValues(): { from: string | null; to: string | null } {
  const group = navPath.value?.group;
  if (group === "created") {
    return {
      from: filterState.value.createdFromUtc,
      to: filterState.value.createdToUtc,
    };
  }
  if (group === "activated") {
    return {
      from: filterState.value.activatedFromUtc,
      to: filterState.value.activatedToUtc,
    };
  }
  if (group === "closed") {
    return {
      from: filterState.value.closedFromUtc,
      to: filterState.value.closedToUtc,
    };
  }
  return { from: null, to: null };
}

function handleDateApply(from: string | null, to: string | null) {
  const group = navPath.value?.group;
  if (group === "created") {
    setCreatedRange(from, to);
  } else if (group === "activated") {
    setActivatedRange(from, to);
  } else if (group === "closed") {
    setClosedRange(from, to);
  }
  closeDropdown();
}

function handleDateClear() {
  const group = navPath.value?.group;
  if (group === "created") {
    setCreatedRange(null, null);
  } else if (group === "activated") {
    setActivatedRange(null, null);
  } else if (group === "closed") {
    setClosedRange(null, null);
  }
  closeDropdown();
}

function hasCurrentDateFilter(): boolean {
  const { from, to } = getCurrentDateValues();
  return from !== null || to !== null;
}

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

const hasStatusFilter = computed(() => filterState.value.status !== "any");
const statusLabel = computed(() => {
  const opt = STATUS_OPTIONS.find((o) => o.value === filterState.value.status);
  return opt?.label ?? filterState.value.status;
});

const hasCreatedDateFilter = computed(
  () =>
    filterState.value.createdFromUtc !== null ||
    filterState.value.createdToUtc !== null,
);
const createdDateLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.createdFromUtc,
    to: filterState.value.createdToUtc,
  }),
);

const hasActivatedDateFilter = computed(
  () =>
    filterState.value.activatedFromUtc !== null ||
    filterState.value.activatedToUtc !== null,
);
const activatedDateLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.activatedFromUtc,
    to: filterState.value.activatedToUtc,
  }),
);

const hasClosedDateFilter = computed(
  () =>
    filterState.value.closedFromUtc !== null ||
    filterState.value.closedToUtc !== null,
);
const closedDateLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.closedFromUtc,
    to: filterState.value.closedToUtc,
  }),
);

// The sort is the SortButton's: it does not raise the "Сбросить" row.
const hasBubbles = computed(() => {
  const state = filterState.value;
  return (
    hasStatusFilter.value ||
    state.hostUsernames.size > 0 ||
    hasCreatedDateFilter.value ||
    hasActivatedDateFilter.value ||
    hasClosedDateFilter.value
  );
});

const hostsBubbleValues = computed(() =>
  [...filterState.value.hostUsernames]
    .sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase(), "ru"))
    .map((username) => ({ id: username, label: username })),
);

function clearAll() {
  localInput.value = "";
  clearFilters();
}

function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    if (hasClosedDateFilter.value) {
      event.preventDefault();
      setClosedRange(null, null);
      return;
    }
    if (hasActivatedDateFilter.value) {
      event.preventDefault();
      setActivatedRange(null, null);
      return;
    }
    if (hasCreatedDateFilter.value) {
      event.preventDefault();
      setCreatedRange(null, null);
      return;
    }
    if (filterState.value.hostUsernames.size > 0) {
      event.preventDefault();
      const lastHost = [...filterState.value.hostUsernames].pop();
      if (lastHost) removeHost(lastHost);
      return;
    }
    if (hasStatusFilter.value) {
      event.preventDefault();
      setStatus("any");
      return;
    }
  }
}
</script>

<template>
  <div class="blogs-filter">
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по названию"
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
          :options="statusListOptions"
          @select="handleStatusSelect"
        />

        <!-- Include inactive: a host away for 30 days stays findable. -->
        <UserMultiSelect
          v-if="navPath?.filter === 'host'"
          :selected-users="filterState.hostUsernames"
          placeholder="Поиск ведущего"
          include-inactive
          @add="addHost"
        />

        <template v-if="navPath?.filter === 'dates' && !navPath.group">
          <FilterDropdownItem
            v-for="item in dateTypeOptions"
            :key="item.value"
            :label="item.label"
            :hint="item.hint"
            has-sub-options
            @item-select="selectDateType(item.value)"
          />
        </template>

        <DateRangePicker
          v-if="navPath?.filter === 'dates' && navPath.group"
          :from-value="getCurrentDateValues().from"
          :to-value="getCurrentDateValues().to"
          :show-clear-button="hasCurrentDateFilter()"
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
      <FilterBubble
        v-if="hasStatusFilter"
        prefix="Статус:"
        :value="statusLabel"
        @remove="setStatus('any')"
      />

      <ExpandableBubble
        v-if="filterState.hostUsernames.size > 0"
        :prefix="filterState.hostUsernames.size > 1 ? 'Ведущие:' : 'Ведущий:'"
        :values="hostsBubbleValues"
        :max-visible="1"
        @remove="removeHost"
      />

      <FilterBubble
        v-if="hasCreatedDateFilter"
        prefix="Создание:"
        :value="createdDateLabel"
        @remove="setCreatedRange(null, null)"
      />

      <FilterBubble
        v-if="hasActivatedDateFilter"
        prefix="Открытие:"
        :value="activatedDateLabel"
        @remove="setActivatedRange(null, null)"
      />

      <FilterBubble
        v-if="hasClosedDateFilter"
        prefix="Закрытие:"
        :value="closedDateLabel"
        @remove="setClosedRange(null, null)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.blogs-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar

+dropdown-search-input
+filter-dropdown-base
</style>
