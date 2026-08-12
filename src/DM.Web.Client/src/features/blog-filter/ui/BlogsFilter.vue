<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import { useBlogsFilter, STATUS_OPTIONS, SORT_OPTIONS } from "../model";
import type { StatusFilter } from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import { formatDateForDisplay } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
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
  navigateBack: navigateBackBase,
} = useFilterDropdown();

function closeDropdown() {
  closeDropdownBase();
}

function navigateBack() {
  // If at level 3 (date range input), go back to date type selection
  if (navPath.value?.group) {
    navPath.value = { filter: navPath.value.filter };
    return;
  }
  navigateBackBase();
}

// Root level filter options
const filterOptions = [
  { key: "status", label: "Статус блога", hint: "Оформляется, Открыт, Закрыт" },
  { key: "host", label: "Ведущие", hint: "Автор или ассистент" },
  { key: "dates", label: "Даты", hint: "Фильтр по датам" },
];

// Date type options (level 2)
const dateTypeOptions = [
  { value: "created", label: "Создание блога", hint: "По дате создания блога" },
  {
    value: "activated",
    label: "Открытие блога",
    hint: "По дате открытия блога",
  },
  { value: "closed", label: "Закрытие блога", hint: "По дате закрытия блога" },
];

// Status options for OptionsList
const statusListOptions = STATUS_OPTIONS.filter((o) => o.value !== "any").map(
  (o) => ({
    value: o.value,
    label: o.label,
    hint: o.hint,
  }),
);

function selectRootItem(key: string) {
  navPath.value = { filter: key };
}

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

// =============================================================================
// STATUS
// =============================================================================

function handleStatusSelect(value: string) {
  setStatus(value as StatusFilter);
  closeDropdown();
}

// =============================================================================
// HOSTS
// =============================================================================

function handleAddHost(username: string) {
  addHost(username);
}

// =============================================================================
// DATE RANGES
// =============================================================================

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

// =============================================================================
// SORT
// =============================================================================

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

// =============================================================================
// BUBBLES
// =============================================================================

const hasStatusFilter = computed(() => filterState.value.status !== "any");
const statusLabel = computed(() => {
  const opt = STATUS_OPTIONS.find((o) => o.value === filterState.value.status);
  return opt?.label ?? filterState.value.status;
});

const hasCreatedDateFilter = computed(() => {
  return (
    filterState.value.createdFromUtc !== null ||
    filterState.value.createdToUtc !== null
  );
});
const createdDateLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.createdFromUtc);
  const to = formatDateForDisplay(filterState.value.createdToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

const hasActivatedDateFilter = computed(() => {
  return (
    filterState.value.activatedFromUtc !== null ||
    filterState.value.activatedToUtc !== null
  );
});
const activatedDateLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.activatedFromUtc);
  const to = formatDateForDisplay(filterState.value.activatedToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

const hasClosedDateFilter = computed(() => {
  return (
    filterState.value.closedFromUtc !== null ||
    filterState.value.closedToUtc !== null
  );
});
const closedDateLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.closedFromUtc);
  const to = formatDateForDisplay(filterState.value.closedToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

// Sort is driven by the SortButton, not the filter bubbles — it must NOT
// trigger the "Сбросить" row (consistent with Games / Pulse / Polls).
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

// Hosts bubble values
const hostsBubbleValues = computed(() =>
  [...filterState.value.hostUsernames]
    .sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase(), "ru"))
    .map((username) => ({ id: username, label: username })),
);

function handleRemoveHost(id: string) {
  removeHost(id);
}

function clearAll() {
  localInput.value = "";
  clearFilters();
}

// Search input keyboard handler (for Backspace to remove filters)
function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    // Remove filters in reverse order
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
    <!-- Filter bar row -->
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по названию"
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
            :options="statusListOptions"
            @select="handleStatusSelect"
          />

          <!-- Hosts multi-select -->
          <UserMultiSelect
            v-if="navPath?.filter === 'host'"
            :selected-users="filterState.hostUsernames"
            placeholder="Поиск ведущего"
            @add="handleAddHost"
            @remove="handleRemoveHost"
          />

          <!-- Date type selection (level 2) -->
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

          <!-- Date range picker (level 3) -->
          <DateRangePicker
            v-if="navPath?.filter === 'dates' && navPath.group"
            :from-value="getCurrentDateValues().from"
            :to-value="getCurrentDateValues().to"
            :show-clear-button="hasCurrentDateFilter()"
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
        @sort-select="setSort"
        @update:sort-order="toggleSortOrder"
      />
    </div>

    <!-- Active filters row (bubbles) -->
    <BubblesRow v-if="hasBubbles" @clear-all="clearAll">
      <!-- Status bubble -->
      <FilterBubble
        v-if="hasStatusFilter"
        prefix="Статус:"
        :value="statusLabel"
        @remove="setStatus('any')"
      />

      <!-- Hosts bubble -->
      <ExpandableBubble
        v-if="filterState.hostUsernames.size > 0"
        :prefix="filterState.hostUsernames.size > 1 ? 'Ведущие:' : 'Ведущий:'"
        :values="hostsBubbleValues"
        :max-visible="1"
        @remove="handleRemoveHost"
      />

      <!-- Created date bubble -->
      <FilterBubble
        v-if="hasCreatedDateFilter"
        prefix="Создание:"
        :value="createdDateLabel"
        @remove="setCreatedRange(null, null)"
      />

      <!-- Activated date bubble -->
      <FilterBubble
        v-if="hasActivatedDateFilter"
        prefix="Открытие:"
        :value="activatedDateLabel"
        @remove="setActivatedRange(null, null)"
      />

      <!-- Closed date bubble -->
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
@import "@/assets/styles/Filters"

.blogs-filter
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
