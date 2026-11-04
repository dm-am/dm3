<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import {
  useUsersFilter,
  ACTIVITY_OPTIONS,
  ROLE_OPTIONS,
  EXPERIENCE_OPTIONS,
  SORT_OPTIONS,
  optionsHint,
} from "../model";
import type { ActivityFilter, RoleFilter, ExperienceFilter } from "../model";
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
  OptionsList,
  NumericRangePicker,
  DateRangePicker,
  SortButton,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";

const {
  filterState,
  setSearch,
  setActivity,
  setOnlineFilter,
  setRole,
  setExperience,
  setRatingRange,
  setGamesHostingRange,
  setGamesPlayingRange,
  setBlogsHostingRange,
  setRegisteredRange,
  setSort,
  toggleSortOrder,
  clearFilters,
} = useUsersFilter();

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown, navigateBack } =
  useFilterDropdown();

const filterOptions = [
  {
    key: "activity",
    label: "Активность",
    hint: optionsHint(ACTIVITY_OPTIONS),
  },
  { key: "role", label: "Роль", hint: optionsHint(ROLE_OPTIONS) },
  { key: "experience", label: "Опыт", hint: optionsHint(EXPERIENCE_OPTIONS) },
  { key: "rating", label: "Рейтинг", hint: "Диапазон значений" },
  { key: "gamesHosting", label: "Игры (ведущий)", hint: "Диапазон значений" },
  { key: "gamesPlaying", label: "Игры (игрок)", hint: "Диапазон значений" },
  { key: "blogs", label: "Блоги", hint: "Диапазон значений" },
  { key: "registered", label: "Дата регистрации", hint: "Диапазон дат" },
];

function selectRootItem(key: string) {
  navPath.value = { filter: key };
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;
  const titles: Record<string, string> = {
    activity: "Активность",
    role: "Роль",
    experience: "Опыт",
    rating: "Рейтинг",
    gamesHosting: "Игры (ведущий)",
    gamesPlaying: "Игры (игрок)",
    blogs: "Блоги",
    registered: "Дата регистрации",
  };
  return titles[navPath.value.filter] ?? null;
}

const activityListOptions = ACTIVITY_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
}));

const roleListOptions = ROLE_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
}));

const experienceListOptions = EXPERIENCE_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
}));

function handleActivitySelect(value: string) {
  if (value === "online") {
    setActivity("active");
    setOnlineFilter("online");
  } else {
    setActivity(value as ActivityFilter);
    setOnlineFilter("all");
  }
  closeDropdown();
}

function handleRoleSelect(value: string) {
  setRole(value as RoleFilter);
  closeDropdown();
}

function handleExperienceSelect(value: string) {
  setExperience(value as ExperienceFilter);
  closeDropdown();
}

function handleRatingApply(min: number | null, max: number | null) {
  setRatingRange(min, max);
  closeDropdown();
}

function handleGamesHostingApply(min: number | null, max: number | null) {
  setGamesHostingRange(min, max);
  closeDropdown();
}

function handleGamesPlayingApply(min: number | null, max: number | null) {
  setGamesPlayingRange(min, max);
  closeDropdown();
}

function handleBlogsApply(min: number | null, max: number | null) {
  setBlogsHostingRange(min, max);
  closeDropdown();
}

function handleRegisteredApply(from: string | null, to: string | null) {
  setRegisteredRange(from, to);
  closeDropdown();
}

function handleRegisteredClear() {
  setRegisteredRange(null, null);
  closeDropdown();
}

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

const hasActivityFilter = computed(() => filterState.value.activity !== "all");
const activityLabel = computed(() => {
  const isOnline =
    filterState.value.activity === "active" &&
    filterState.value.onlineFilter === "online";
  const displayValue = isOnline ? "online" : filterState.value.activity;
  const opt = ACTIVITY_OPTIONS.find((o) => o.value === displayValue);
  return opt?.label ?? filterState.value.activity;
});

const hasRoleFilter = computed(() => filterState.value.role !== "all");
const roleLabel = computed(() => {
  const opt = ROLE_OPTIONS.find((o) => o.value === filterState.value.role);
  return opt?.label ?? String(filterState.value.role);
});

const hasExperienceFilter = computed(
  () => filterState.value.experience !== "all",
);
const experienceLabel = computed(() => {
  const opt = EXPERIENCE_OPTIONS.find(
    (o) => o.value === filterState.value.experience,
  );
  return opt?.label ?? filterState.value.experience;
});

const hasRatingFilter = computed(
  () =>
    filterState.value.ratingMin !== null ||
    filterState.value.ratingMax !== null,
);
const ratingLabel = computed(() =>
  formatNumberRangeForDisplay(
    filterState.value.ratingMin,
    filterState.value.ratingMax,
  ),
);

const hasGamesHostingFilter = computed(
  () =>
    filterState.value.gamesHostingMin !== null ||
    filterState.value.gamesHostingMax !== null,
);
const gamesHostingLabel = computed(() =>
  formatNumberRangeForDisplay(
    filterState.value.gamesHostingMin,
    filterState.value.gamesHostingMax,
  ),
);

const hasGamesPlayingFilter = computed(
  () =>
    filterState.value.gamesPlayingMin !== null ||
    filterState.value.gamesPlayingMax !== null,
);
const gamesPlayingLabel = computed(() =>
  formatNumberRangeForDisplay(
    filterState.value.gamesPlayingMin,
    filterState.value.gamesPlayingMax,
  ),
);

const hasBlogsFilter = computed(
  () =>
    filterState.value.blogsHostingMin !== null ||
    filterState.value.blogsHostingMax !== null,
);
const blogsLabel = computed(() =>
  formatNumberRangeForDisplay(
    filterState.value.blogsHostingMin,
    filterState.value.blogsHostingMax,
  ),
);

const hasRegisteredFilter = computed(
  () =>
    filterState.value.registeredFromUtc !== null ||
    filterState.value.registeredToUtc !== null,
);
const registeredLabel = computed(() => {
  return formatDateRangeForDisplay({
    from: filterState.value.registeredFromUtc,
    to: filterState.value.registeredToUtc,
  });
});

// The sort is the SortButton's: it does not raise the "Сбросить" row.
const hasBubbles = computed(() => {
  return (
    hasActivityFilter.value ||
    hasRoleFilter.value ||
    hasExperienceFilter.value ||
    hasRatingFilter.value ||
    hasGamesHostingFilter.value ||
    hasGamesPlayingFilter.value ||
    hasBlogsFilter.value ||
    hasRegisteredFilter.value
  );
});

function clearActivityFilter() {
  setActivity("all");
  setOnlineFilter("all");
}

function clearAll() {
  localInput.value = "";
  clearFilters();
}

function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    if (hasRegisteredFilter.value) {
      event.preventDefault();
      setRegisteredRange(null, null);
      return;
    }
    if (hasBlogsFilter.value) {
      event.preventDefault();
      setBlogsHostingRange(null, null);
      return;
    }
    if (hasGamesPlayingFilter.value) {
      event.preventDefault();
      setGamesPlayingRange(null, null);
      return;
    }
    if (hasGamesHostingFilter.value) {
      event.preventDefault();
      setGamesHostingRange(null, null);
      return;
    }
    if (hasRatingFilter.value) {
      event.preventDefault();
      setRatingRange(null, null);
      return;
    }
    if (hasExperienceFilter.value) {
      event.preventDefault();
      setExperience("all");
      return;
    }
    if (hasRoleFilter.value) {
      event.preventDefault();
      setRole("all");
      return;
    }
    if (hasActivityFilter.value) {
      event.preventDefault();
      clearActivityFilter();
      return;
    }
  }
}
</script>

<template>
  <div class="users-filter">
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по имени"
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

          <OptionsList
            v-if="navPath?.filter === 'activity'"
            :options="activityListOptions"
            @select="handleActivitySelect"
          />

          <OptionsList
            v-if="navPath?.filter === 'role'"
            :options="roleListOptions"
            @select="handleRoleSelect"
          />

          <OptionsList
            v-if="navPath?.filter === 'experience'"
            :options="experienceListOptions"
            @select="handleExperienceSelect"
          />

          <NumericRangePicker
            v-if="navPath?.filter === 'rating'"
            :min-value="filterState.ratingMin"
            :max-value="filterState.ratingMax"
            :allow-negative="true"
            @apply="handleRatingApply"
          />

          <NumericRangePicker
            v-if="navPath?.filter === 'gamesHosting'"
            :min-value="filterState.gamesHostingMin"
            :max-value="filterState.gamesHostingMax"
            @apply="handleGamesHostingApply"
          />

          <NumericRangePicker
            v-if="navPath?.filter === 'gamesPlaying'"
            :min-value="filterState.gamesPlayingMin"
            :max-value="filterState.gamesPlayingMax"
            @apply="handleGamesPlayingApply"
          />

          <NumericRangePicker
            v-if="navPath?.filter === 'blogs'"
            :min-value="filterState.blogsHostingMin"
            :max-value="filterState.blogsHostingMax"
            @apply="handleBlogsApply"
          />

          <DateRangePicker
            v-if="navPath?.filter === 'registered'"
            :from-value="filterState.registeredFromUtc"
            :to-value="filterState.registeredToUtc"
            :show-clear-button="hasRegisteredFilter"
            @apply="handleRegisteredApply"
            @clear="handleRegisteredClear"
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
      <FilterBubble
        v-if="hasActivityFilter"
        prefix="Активность:"
        :value="activityLabel"
        @remove="clearActivityFilter"
      />

      <FilterBubble
        v-if="hasRoleFilter"
        prefix="Роль:"
        :value="roleLabel"
        @remove="setRole('all')"
      />

      <FilterBubble
        v-if="hasExperienceFilter"
        prefix="Опыт:"
        :value="experienceLabel"
        @remove="setExperience('all')"
      />

      <FilterBubble
        v-if="hasRatingFilter"
        prefix="Рейтинг:"
        :value="ratingLabel"
        @remove="setRatingRange(null, null)"
      />

      <FilterBubble
        v-if="hasGamesHostingFilter"
        prefix="Игры (ведущий):"
        :value="gamesHostingLabel"
        @remove="setGamesHostingRange(null, null)"
      />

      <FilterBubble
        v-if="hasGamesPlayingFilter"
        prefix="Игры (игрок):"
        :value="gamesPlayingLabel"
        @remove="setGamesPlayingRange(null, null)"
      />

      <FilterBubble
        v-if="hasBlogsFilter"
        prefix="Блоги:"
        :value="blogsLabel"
        @remove="setBlogsHostingRange(null, null)"
      />

      <FilterBubble
        v-if="hasRegisteredFilter"
        prefix="Регистрация:"
        :value="registeredLabel"
        @remove="setRegisteredRange(null, null)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.users-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button

+dropdown-search-input
+filter-dropdown-base
</style>
