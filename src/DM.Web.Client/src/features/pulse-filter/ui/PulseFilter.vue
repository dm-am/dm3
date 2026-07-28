<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
import { computed, ref, watch } from "vue";
import { vClickOutside } from "@/shared/directives";
import { gameApi } from "@/entities/game";
import { usePulseFilter } from "../model";
import type { PulseSortBy } from "../model";
import { useFilterSearch, useFilterDropdown } from "@/shared/lib/composables";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
  FilterDropdownItem,
  NumericRangePicker,
  DateRangePicker,
  SortButton,
  FilterBubble,
  ExpandableBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";
import type { BubbleValue } from "@/shared/ui/Filters";

// `hideAuthorFilter` — for pages with an implicit author scope (the profile
// "Полученные оценки" / "Оценил чужих постов" subpages): the scope is set
// by the container via a query param, the "Авторы" filter button is hidden
// to avoid confusing the user.
const props = withDefaults(
  defineProps<{
    hideAuthorFilter?: boolean;
  }>(),
  { hideAuthorFilter: false },
);

const {
  filterState,
  setSearch,
  setSort,
  setRatingRange,
  addAuthor,
  removeAuthor,
  setCreatedRange,
  setGameId,
  clearFilters,
} = usePulseFilter();

// Search input (with debounce)
const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

// =============================================================================
// SINGLE FILTER DROPDOWN (with navigation, like GamesFilter)
// =============================================================================

const { showDropdown, navPath, closeDropdown, toggleDropdown } =
  useFilterDropdown();

// Root level filter options. The "Авторы" option is hidden when the host
// has nailed the author scope down (profile subpages).
const filterOptions = computed(() => {
  const items = [
    { key: "rating", label: "Рейтинг", hint: "Диапазон рейтинга поста" },
    { key: "author", label: "Авторы", hint: "Фильтр по авторам постов" },
    { key: "date", label: "Дата создания", hint: "Когда был написан пост" },
  ];
  return props.hideAuthorFilter
    ? items.filter((i) => i.key !== "author")
    : items;
});

function selectRootItem(key: string) {
  navPath.value = { filter: key };
}

function navigateBack() {
  navPath.value = null;
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  if (navPath.value.filter === "rating") return "Рейтинг";
  if (navPath.value.filter === "author") return "Авторы";
  if (navPath.value.filter === "date") return "Дата создания";
  return null;
}

// Rating range
function handleRatingApply(min: number | null, max: number | null) {
  setRatingRange(min, max);
  closeDropdown();
}

function handleRatingClear() {
  setRatingRange(null, null);
  closeDropdown();
}

// Date range
function handleDateApply(from: string | null, to: string | null) {
  setCreatedRange(from, to);
  closeDropdown();
}

function handleDateClear() {
  setCreatedRange(null, null);
  closeDropdown();
}

// Sort options
const sortOptions = [
  {
    value: "lastreview",
    label: "Последние оцененные",
    hint: "По дате оценки",
    defaultDirection: "desc" as const,
  },
  {
    value: "rating",
    label: "По рейтингу",
    hint: "По сумме оценок",
    defaultDirection: "desc" as const,
  },
  {
    value: "reviewcount",
    label: "По количеству оценок",
    hint: "По числу отзывов",
    defaultDirection: "desc" as const,
  },
];

// Track pending sortBy to avoid race condition:
// SortButton emits update:sortBy THEN update:sortOrder in same tick,
// but filterState hasn't updated yet when sortOrder handler runs
let pendingSortBy: PulseSortBy | null = null;

function handleSortByChange(value: string) {
  const option = sortOptions.find((o) => o.value === value);
  pendingSortBy = value as PulseSortBy;
  setSort(value as PulseSortBy, option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  const sortBy = pendingSortBy ?? filterState.value.sortBy;
  pendingSortBy = null;
  if (
    order !== filterState.value.sortOrder ||
    sortBy !== filterState.value.sortBy
  ) {
    setSort(sortBy, order);
  }
}

// ── Bubbles ──

const hasRatingFilter = computed(
  () =>
    filterState.value.minRating !== null ||
    filterState.value.maxRating !== null,
);

const ratingBubbleLabel = computed(() => {
  const { minRating, maxRating } = filterState.value;
  if (minRating !== null && maxRating !== null)
    return `от ${minRating} до ${maxRating}`;
  if (minRating !== null) return `от ${minRating}`;
  if (maxRating !== null) return `до ${maxRating}`;
  return "";
});

const hasAuthorFilter = computed(
  () => filterState.value.authorUsernames.size > 0,
);

const authorsBubbleValues = computed<BubbleValue[]>(() =>
  [...filterState.value.authorUsernames].map((u) => ({ id: u, label: u })),
);

const hasDateFilter = computed(
  () =>
    filterState.value.createdFrom !== null ||
    filterState.value.createdTo !== null,
);

const dateBubbleLabel = computed(() => {
  const { createdFrom, createdTo } = filterState.value;
  const from = createdFrom ? formatDate(createdFrom) : null;
  const to = createdTo ? formatDate(createdTo) : null;
  if (from && to) return `с ${from} по ${to}`;
  if (from) return `с ${from}`;
  if (to) return `по ${to}`;
  return "";
});

// Game filter has no dropdown UI — it arrives via the ?game= URL param
// (deep link). The bubble makes it visible and removable.
const hasGameFilter = computed(() => filterState.value.gameId !== null);

const gameTitle = ref<string | null>(null);

watch(
  () => filterState.value.gameId,
  async (gameId) => {
    gameTitle.value = null;
    if (!gameId) return;
    const { data } = await gameApi.getGame(gameId);
    // Ignore stale response if the filter changed meanwhile;
    // on fetch failure the bubble falls back to the generic label.
    // The details endpoint wraps the game in a single-resource envelope.
    if (filterState.value.gameId === gameId && data) {
      gameTitle.value = data.resource?.title ?? null;
    }
  },
  { immediate: true },
);

const hasBubbles = computed(
  () =>
    hasRatingFilter.value ||
    (!props.hideAuthorFilter && hasAuthorFilter.value) ||
    hasDateFilter.value ||
    hasGameFilter.value,
);

function clearAll() {
  localInput.value = "";
  clearFilters();
}

// Backspace removes last active filter when search input is empty
function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    if (hasDateFilter.value) {
      event.preventDefault();
      setCreatedRange(null, null);
      return;
    }
    if (hasAuthorFilter.value) {
      event.preventDefault();
      const last = [...filterState.value.authorUsernames].pop();
      if (last) removeAuthor(last);
      return;
    }
    if (hasRatingFilter.value) {
      event.preventDefault();
      setRatingRange(null, null);
    }
  }
}
</script>

<template>
  <div class="pulse-filter">
    <!-- Filter bar row -->
    <div class="filter-bar">
      <!-- Search -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту поста"
        @input="handleInput"
        @blur="applySearch"
        @keydown="handleSearchKeydown"
      />

      <!-- Single filter dropdown with navigation -->
      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton
          label="Фильтры"
          :active="showDropdown"
          @click="toggleDropdown"
          @close="closeDropdown"
        />

        <FilterDropdown v-if="showDropdown" @close="closeDropdown">
          <!-- Header with back navigation (when inside a filter) -->
          <FilterDropdownHeader
            v-if="navPath"
            :title="getDropdownTitle() ?? ''"
            @back="navigateBack"
          />

          <!-- Root level: list of filter types -->
          <template v-if="!navPath">
            <FilterDropdownItem
              v-for="option in filterOptions"
              :key="option.key"
              :label="option.label"
              :hint="option.hint"
              :has-sub-options="true"
              @item-select="selectRootItem(option.key)"
            />
          </template>

          <!-- Level 2: authors guard — even if the URL came with author=X
               manually, in hideAuthor mode the sublevel cannot be opened
               (the option is absent from filterOptions above). -->
          <UserMultiSelect
            v-if="!hideAuthorFilter && navPath?.filter === 'author'"
            :selected-users="filterState.authorUsernames"
            placeholder="Поиск автора"
            @add="addAuthor"
            @remove="removeAuthor"
          />

          <!-- Level 2: Rating range -->
          <NumericRangePicker
            v-if="navPath?.filter === 'rating'"
            :min-value="filterState.minRating"
            :max-value="filterState.maxRating"
            :allow-negative="true"
            @apply="handleRatingApply"
            @clear="handleRatingClear"
          />

          <!-- Level 2: Date range -->
          <DateRangePicker
            v-if="navPath?.filter === 'date'"
            :from-value="filterState.createdFrom"
            :to-value="filterState.createdTo"
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
      <FilterBubble
        v-if="hasRatingFilter"
        prefix="Рейтинг:"
        :value="ratingBubbleLabel"
        @remove="setRatingRange(null, null)"
      />

      <FilterBubble
        v-if="
          !hideAuthorFilter &&
          hasAuthorFilter &&
          filterState.authorUsernames.size === 1
        "
        prefix="Автор:"
        :value="[...filterState.authorUsernames][0]"
        @remove="removeAuthor([...filterState.authorUsernames][0])"
      />
      <ExpandableBubble
        v-else-if="!hideAuthorFilter && hasAuthorFilter"
        prefix="Авторы:"
        :values="authorsBubbleValues"
        :max-visible="1"
        @remove="removeAuthor($event)"
      />

      <FilterBubble
        v-if="hasDateFilter"
        prefix="Дата создания:"
        :value="dateBubbleLabel"
        @remove="setCreatedRange(null, null)"
      />

      <FilterBubble
        v-if="hasGameFilter"
        :prefix="gameTitle ? 'Игра:' : undefined"
        :value="gameTitle ?? 'Игра'"
        @remove="setGameId(null)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
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
