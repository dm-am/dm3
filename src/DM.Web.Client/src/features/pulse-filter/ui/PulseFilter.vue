<script setup lang="ts">
import {
  formatDateRangeForDisplay,
  formatNumberRangeForDisplay,
} from "@/shared/lib/filters";
import { computed, ref, watch } from "vue";
import { gameApi } from "@/entities/game";
import { usePulseFilter } from "../model";
import type { PulseFilterKey, PulseSortBy } from "../model";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import {
  FilterSearchInput,
  FilterMenu,
  NumericRangePicker,
  DateRangePicker,
  SortButton,
  FilterBubble,
  ExpandableBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";
import type { BubbleValue } from "@/shared/ui/Filters";
import type { SortDirection } from "@/shared/lib/filters/types";

// Pages with an implicit author scope (profile subpages) hide the "Авторы" filter.
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

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown } =
  useFilterDropdown<PulseFilterKey>();

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

function navigateBack() {
  navPath.value = null;
}

const dropdownTitle = computed(
  () =>
    filterOptions.value.find((o) => o.key === navPath.value?.filter)?.label ??
    "",
);

function handleRatingApply(min: number | null, max: number | null) {
  setRatingRange(min, max);
  closeDropdown();
}

function handleRatingClear() {
  setRatingRange(null, null);
  closeDropdown();
}

function handleDateApply(from: string | null, to: string | null) {
  setCreatedRange(from, to);
  closeDropdown();
}

function handleDateClear() {
  setCreatedRange(null, null);
  closeDropdown();
}

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
    hint: "По числу оценок",
    defaultDirection: "desc" as const,
  },
];

function handleSortSelect(value: string, direction?: SortDirection) {
  setSort(value as PulseSortBy, direction);
}

// No toggle action of its own: the flip is a re-sort on the field in effect.
function handleSortOrderChange(order: SortDirection) {
  setSort(filterState.value.sortBy, order);
}

const hasRatingFilter = computed(
  () =>
    filterState.value.minRating !== null ||
    filterState.value.maxRating !== null,
);

const ratingBubbleLabel = computed(() =>
  formatNumberRangeForDisplay(
    filterState.value.minRating,
    filterState.value.maxRating,
  ),
);

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

const dateBubbleLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.createdFrom,
    to: filterState.value.createdTo,
  }),
);

// The game arrives only via ?game= (a deep link); the bubble makes it visible and removable.
const hasGameFilter = computed(() => filterState.value.gameId !== null);

const gameTitle = ref<string | null>(null);

watch(
  () => filterState.value.gameId,
  async (gameId) => {
    gameTitle.value = null;
    if (!gameId) return;
    const { data } = await gameApi.getGame(gameId);
    // A stale answer is ignored; on failure the bubble keeps the generic label.
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
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту поста"
        @input="handleInput"
        @blur="applySearch"
        @keydown="handleSearchKeydown"
      />

      <FilterMenu
        v-model:nav-path="navPath"
        :open="showDropdown"
        :title="dropdownTitle"
        :options="filterOptions"
        @toggle="toggleDropdown"
        @close="closeDropdown"
        @back="navigateBack"
      >
        <!-- Shut in hideAuthor mode even with ?author= in the address; include inactive: an author away for 30 days stays findable. -->
        <UserMultiSelect
          v-if="!hideAuthorFilter && navPath?.filter === 'author'"
          :selected-users="filterState.authorUsernames"
          placeholder="Поиск автора"
          include-inactive
          @add="addAuthor"
        />

        <NumericRangePicker
          v-if="navPath?.filter === 'rating'"
          :min-value="filterState.minRating"
          :max-value="filterState.maxRating"
          :allow-negative="true"
          @apply="handleRatingApply"
          @clear="handleRatingClear"
        />

        <DateRangePicker
          v-if="navPath?.filter === 'date'"
          :from-value="filterState.createdFrom"
          :to-value="filterState.createdTo"
          @apply="handleDateApply"
          @clear="handleDateClear"
        />
      </FilterMenu>

      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @sort-select="handleSortSelect"
        @update:sort-order="handleSortOrderChange"
      />
    </div>

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
@use "@/assets/styles/Filters" as *

.pulse-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar

+filter-dropdown-base
</style>
