<script setup lang="ts">
/**
 * The filter bar of a game room: the pulse bar (search, "Фильтры", sort) built
 * from the same shared controls, with the two filters only a room has — the
 * characters and whether a post has reviews — and the room's own order as the
 * default sort.
 */
import { computed, ref, watch } from "vue";
import { vClickOutside } from "@/shared/directives";
import {
  formatDateRangeForDisplay,
  formatNumberRangeForDisplay,
} from "@/shared/lib/filters";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
  FilterDropdownItem,
  NumericRangePicker,
  DateRangePicker,
  OptionsList,
  SortButton,
  FilterBubble,
  ExpandableBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import type { BubbleValue, ListOption } from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";
import type { Character } from "@/entities/game";
import {
  useRoomPostFilter,
  WITHOUT_CHARACTER,
  type RoomPostReviews,
  type RoomPostSortBy,
} from "../model";

const props = defineProps<{
  /** The game's characters, to pick from and to name in the bubbles. */
  characters: Character[];
}>();

const {
  filterState,
  setSearch,
  setSort,
  addAuthor,
  removeAuthor,
  addCharacter,
  removeCharacter,
  setRatingRange,
  setReviews,
  setCreatedRange,
  clearFilters,
} = useRoomPostFilter();

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown } =
  useFilterDropdown();

// The order of the menu is the order of the bubbles below the bar.
const filterOptions = [
  { key: "author", label: "Авторы", hint: "Фильтр по авторам постов" },
  { key: "character", label: "Персонажи", hint: "От чьего имени написан пост" },
  { key: "rating", label: "Рейтинг", hint: "Диапазон рейтинга поста" },
  { key: "reviews", label: "Оценки", hint: "С оценками или без" },
  { key: "date", label: "Дата создания", hint: "Когда был написан пост" },
];

const dropdownTitle = computed(
  () => filterOptions.find((o) => o.key === navPath.value?.filter)?.label ?? "",
);

function selectRootItem(key: string) {
  characterQuery.value = "";
  navPath.value = { filter: key };
}

function navigateBack() {
  navPath.value = null;
}

// ── Characters ──

const WITHOUT_CHARACTER_LABEL = "Без персонажа";
const characterQuery = ref("");

/** Every character of the game by id, for the bubbles. */
const characterNames = computed(
  () => new Map(props.characters.map((c) => [c.id as string, c.name])),
);

function characterLabel(id: string): string {
  if (id === WITHOUT_CHARACTER) return WITHOUT_CHARACTER_LABEL;
  return characterNames.value.get(id) ?? "Персонаж";
}

// Every character that has been in play: a post keeps the character it was
// written by whatever became of the character later, so a retired one stays.
// One still under review, or declined, has never written a post.
const WROTE_POSTS = new Set(["Active", "Retired"]);

// The ones already chosen stand in the bubbles and leave the list, the way the
// authors do.
const characterOptions = computed<ListOption[]>(() => {
  const chosen = filterState.value.characters;
  const options: ListOption[] = [];
  if (!chosen.has(WITHOUT_CHARACTER)) {
    options.push({
      value: WITHOUT_CHARACTER,
      label: WITHOUT_CHARACTER_LABEL,
      hint: "Мастер и помощники от своего имени",
    });
  }
  const sorted = props.characters
    .filter((c) => WROTE_POSTS.has(c.status))
    .sort((a, b) => a.name.localeCompare(b.name, "ru"));
  for (const c of sorted) {
    if (chosen.has(c.id as string)) continue;
    options.push({
      value: c.id as string,
      label: c.name,
      hint: c.isNpc ? "NPC" : c.author?.username,
    });
  }
  return options;
});

function handleCharacterSelect(id: string) {
  addCharacter(id);
  characterQuery.value = "";
}

// ── Reviews ──

const reviewOptions: (ListOption & { value: RoomPostReviews })[] = [
  { value: "with", label: "С оценками", hint: "Пост кто-то оценил" },
  { value: "without", label: "Без оценок", hint: "Пост пока никто не оценил" },
];

function handleReviewsSelect(value: string) {
  setReviews(value as RoomPostReviews);
  closeDropdown();
}

// ── Rating and date ──

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

// ── Sort ──

// The room's own order first: it is the default and what the room is.
const sortOptions = [
  {
    value: "created",
    label: "По порядку",
    hint: "По дате написания",
    defaultDirection: "asc" as const,
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
  {
    value: "lastreview",
    label: "Последние оцененные",
    hint: "По дате оценки",
    defaultDirection: "desc" as const,
  },
];

function handleSortSelect(value: string, direction?: "asc" | "desc") {
  setSort(value as RoomPostSortBy, direction);
}

function handleSortOrderChange(order: "asc" | "desc") {
  setSort(filterState.value.sortBy, order);
}

// ── Bubbles ──

const hasAuthorFilter = computed(
  () => filterState.value.authorUsernames.size > 0,
);
const authorValues = computed<BubbleValue[]>(() =>
  [...filterState.value.authorUsernames].map((u) => ({ id: u, label: u })),
);

const hasCharacterFilter = computed(
  () => filterState.value.characters.size > 0,
);
const characterValues = computed<BubbleValue[]>(() =>
  [...filterState.value.characters].map((id) => ({
    id,
    label: characterLabel(id),
  })),
);

const hasRatingFilter = computed(
  () =>
    filterState.value.minRating !== null ||
    filterState.value.maxRating !== null,
);
const ratingLabel = computed(() =>
  formatNumberRangeForDisplay(
    filterState.value.minRating,
    filterState.value.maxRating,
  ),
);

const reviewsLabel = computed(() =>
  filterState.value.reviews === "with" ? "есть" : "нет",
);

const hasDateFilter = computed(
  () =>
    filterState.value.createdFrom !== null ||
    filterState.value.createdTo !== null,
);
const dateLabel = computed(() =>
  formatDateRangeForDisplay({
    from: filterState.value.createdFrom,
    to: filterState.value.createdTo,
  }),
);

const hasBubbles = computed(
  () =>
    hasAuthorFilter.value ||
    hasCharacterFilter.value ||
    hasRatingFilter.value ||
    filterState.value.reviews !== null ||
    hasDateFilter.value,
);

function clearAll() {
  localInput.value = "";
  clearFilters();
}

// Backspace in an empty search takes the last bubble off the row.
function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key !== "Backspace" || localInput.value) return;
  const state = filterState.value;
  if (hasDateFilter.value) setCreatedRange(null, null);
  else if (state.reviews !== null) setReviews(null);
  else if (hasRatingFilter.value) setRatingRange(null, null);
  else if (hasCharacterFilter.value)
    removeCharacter([...state.characters].pop()!);
  else if (hasAuthorFilter.value)
    removeAuthor([...state.authorUsernames].pop()!);
  else return;
  event.preventDefault();
}

// The menu reopens at its root (useFilterDropdown drops the level on close),
// and the character search typed last time goes with it.
watch(showDropdown, (open) => {
  if (!open) characterQuery.value = "";
});
</script>

<template>
  <div class="room-post-filter">
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по тексту поста"
        @input="handleInput"
        @blur="applySearch"
        @keydown="handleSearchKeydown"
      />

      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton
          label="Фильтры"
          :active="showDropdown"
          @click="toggleDropdown"
          @close="closeDropdown"
        />

        <FilterDropdown v-if="showDropdown" @close="closeDropdown">
          <FilterDropdownHeader
            v-if="navPath"
            :title="dropdownTitle"
            @back="navigateBack"
          />

          <template v-if="!navPath">
            <FilterDropdownItem
              v-for="option in filterOptions"
              :key="option.key"
              :label="option.label"
              :hint="option.hint"
              has-sub-options
              @item-select="selectRootItem(option.key)"
            />
          </template>

          <UserMultiSelect
            v-if="navPath?.filter === 'author'"
            :selected-users="filterState.authorUsernames"
            placeholder="Поиск автора"
            include-inactive
            @add="addAuthor"
            @remove="removeAuthor"
          />

          <template v-if="navPath?.filter === 'character'">
            <div class="dropdown-search">
              <input
                v-model="characterQuery"
                type="text"
                class="dropdown-search-input"
                placeholder="Поиск персонажа"
                aria-label="Поиск персонажа"
              />
            </div>
            <OptionsList
              :options="characterOptions"
              :filter-query="characterQuery"
              @select="handleCharacterSelect"
            />
          </template>

          <NumericRangePicker
            v-if="navPath?.filter === 'rating'"
            :min-value="filterState.minRating"
            :max-value="filterState.maxRating"
            :allow-negative="true"
            @apply="handleRatingApply"
            @clear="handleRatingClear"
          />

          <OptionsList
            v-if="navPath?.filter === 'reviews'"
            :options="reviewOptions"
            @select="handleReviewsSelect"
          />

          <DateRangePicker
            v-if="navPath?.filter === 'date'"
            :from-value="filterState.createdFrom"
            :to-value="filterState.createdTo"
            @apply="handleDateApply"
            @clear="handleDateClear"
          />
        </FilterDropdown>
      </div>

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
        v-if="authorValues.length === 1"
        prefix="Автор:"
        :value="authorValues[0].label"
        @remove="removeAuthor(authorValues[0].id)"
      />
      <ExpandableBubble
        v-else-if="hasAuthorFilter"
        prefix="Авторы:"
        :values="authorValues"
        :max-visible="1"
        @remove="removeAuthor($event)"
      />

      <FilterBubble
        v-if="characterValues.length === 1"
        prefix="Персонаж:"
        :value="characterValues[0].label"
        @remove="removeCharacter(characterValues[0].id)"
      />
      <ExpandableBubble
        v-else-if="hasCharacterFilter"
        prefix="Персонажи:"
        :values="characterValues"
        :max-visible="1"
        @remove="removeCharacter($event)"
      />

      <FilterBubble
        v-if="hasRatingFilter"
        prefix="Рейтинг:"
        :value="ratingLabel"
        @remove="setRatingRange(null, null)"
      />

      <FilterBubble
        v-if="filterState.reviews !== null"
        prefix="Оценки:"
        :value="reviewsLabel"
        @remove="setReviews(null)"
      />

      <FilterBubble
        v-if="hasDateFilter"
        prefix="Дата создания:"
        :value="dateLabel"
        @remove="setCreatedRange(null, null)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.room-post-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button
  +filter-responsive

+filter-dropdown-base
+dropdown-search-input
</style>
