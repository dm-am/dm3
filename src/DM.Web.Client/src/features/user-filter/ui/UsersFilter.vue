<script setup lang="ts">
import { computed } from "vue";
import { vClickOutside } from "@/shared/directives";
import {
  useUsersFilter,
  ACTIVITY_OPTIONS,
  ROLE_OPTIONS,
  HONORARY_OPTIONS,
  EXPERIENCE_OPTIONS,
  SORT_OPTIONS,
  DEFAULT_FILTER_STATE,
} from "../model";
import type { ActivityFilter, RoleFilter, HonoraryFilter, ExperienceFilter } from "../model";
import { UserRole } from "@/entities/user";
import { formatDateForDisplay } from "@/shared/lib/filters";
import { useFilterSearch, useFilterDropdown } from "@/shared/lib/composables";
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
  setHonorary,
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

// =============================================================================
// SEARCH INPUT (with debounce)
// =============================================================================

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch
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
  // From honorary, go back to role selection
  if (navPath.value?.filter === "honorary") {
    setRole("all");
    navPath.value = { filter: "role" };
    return;
  }
  navigateBackBase();
}

// Root level filter options
const filterOptions = [
  { key: "activity", label: "Активность", hint: "Онлайн, Активные, Все" },
  { key: "role", label: "Роль", hint: "Админ, Модератор, Наставник" },
  { key: "experience", label: "Опыт", hint: "Новички, Опытные" },
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
    honorary: "Пользователь",
    experience: "Опыт",
    rating: "Рейтинг",
    gamesHosting: "Игры (ведущий)",
    gamesPlaying: "Игры (игрок)",
    blogs: "Блоги",
    registered: "Дата регистрации",
  };
  return titles[navPath.value.filter] ?? null;
}

// =============================================================================
// FILTER OPTIONS
// =============================================================================

// Activity options
const activityListOptions = ACTIVITY_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
}));

// Role options
const roleListOptions = ROLE_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  hasSubOptions: "hasSubOptions" in o ? o.hasSubOptions : false,
}));

// Honorary options
const honoraryListOptions = HONORARY_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
}));

// Experience options
const experienceListOptions = EXPERIENCE_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
}));

// =============================================================================
// FILTER HANDLERS
// =============================================================================

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
  // For RegularUser, navigate to honorary sub-level
  if (value === UserRole.RegularUser) {
    setRole(value as RoleFilter);
    navPath.value = { filter: "honorary" };
    return;
  }
  setRole(value as RoleFilter);
  closeDropdown();
}

function handleHonorarySelect(value: string) {
  setHonorary(value as HonoraryFilter);
  closeDropdown();
}

function handleExperienceSelect(value: string) {
  setExperience(value as ExperienceFilter);
  closeDropdown();
}

// =============================================================================
// NUMERIC RANGE HANDLERS
// =============================================================================

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

// =============================================================================
// SORT
// =============================================================================

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

function handleSortByChange(value: string) {
  const option = SORT_OPTIONS.find((o) => o.value === value);
  setSort(value, option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  if (order !== filterState.value.sortOrder) {
    toggleSortOrder();
  }
}

// =============================================================================
// BUBBLES
// =============================================================================

// Activity bubble
const hasActivityFilter = computed(() => filterState.value.activity !== "all");
const activityLabel = computed(() => {
  const isOnline = filterState.value.activity === "active" && filterState.value.onlineFilter === "online";
  const displayValue = isOnline ? "online" : filterState.value.activity;
  const opt = ACTIVITY_OPTIONS.find((o) => o.value === displayValue);
  return opt?.label ?? filterState.value.activity;
});

// Role bubble
const hasRoleFilter = computed(() => filterState.value.role !== "all");
const roleLabel = computed(() => {
  const opt = ROLE_OPTIONS.find((o) => o.value === filterState.value.role);
  let label = opt?.label ?? String(filterState.value.role);
  if (filterState.value.role === UserRole.RegularUser && filterState.value.honorary === "honorary") {
    const honoraryOpt = HONORARY_OPTIONS.find((o) => o.value === "honorary");
    label = honoraryOpt?.label ?? "Почетные";
  }
  return label;
});

// Experience bubble
const hasExperienceFilter = computed(() => filterState.value.experience !== "all");
const experienceLabel = computed(() => {
  const opt = EXPERIENCE_OPTIONS.find((o) => o.value === filterState.value.experience);
  return opt?.label ?? filterState.value.experience;
});

// Rating bubble
const hasRatingFilter = computed(() => filterState.value.ratingMin !== null || filterState.value.ratingMax !== null);
const ratingLabel = computed(() => {
  const { ratingMin, ratingMax } = filterState.value;
  if (ratingMin !== null && ratingMax !== null) return `${ratingMin} — ${ratingMax}`;
  if (ratingMin !== null) return `от ${ratingMin}`;
  if (ratingMax !== null) return `до ${ratingMax}`;
  return "";
});

// Games hosting bubble
const hasGamesHostingFilter = computed(() => filterState.value.gamesHostingMin !== null || filterState.value.gamesHostingMax !== null);
const gamesHostingLabel = computed(() => {
  const { gamesHostingMin, gamesHostingMax } = filterState.value;
  if (gamesHostingMin !== null && gamesHostingMax !== null) return `${gamesHostingMin} — ${gamesHostingMax}`;
  if (gamesHostingMin !== null) return `от ${gamesHostingMin}`;
  if (gamesHostingMax !== null) return `до ${gamesHostingMax}`;
  return "";
});

// Games playing bubble
const hasGamesPlayingFilter = computed(() => filterState.value.gamesPlayingMin !== null || filterState.value.gamesPlayingMax !== null);
const gamesPlayingLabel = computed(() => {
  const { gamesPlayingMin, gamesPlayingMax } = filterState.value;
  if (gamesPlayingMin !== null && gamesPlayingMax !== null) return `${gamesPlayingMin} — ${gamesPlayingMax}`;
  if (gamesPlayingMin !== null) return `от ${gamesPlayingMin}`;
  if (gamesPlayingMax !== null) return `до ${gamesPlayingMax}`;
  return "";
});

// Blogs hosting bubble
const hasBlogsFilter = computed(() => filterState.value.blogsHostingMin !== null || filterState.value.blogsHostingMax !== null);
const blogsLabel = computed(() => {
  const { blogsHostingMin, blogsHostingMax } = filterState.value;
  if (blogsHostingMin !== null && blogsHostingMax !== null) return `${blogsHostingMin} — ${blogsHostingMax}`;
  if (blogsHostingMin !== null) return `от ${blogsHostingMin}`;
  if (blogsHostingMax !== null) return `до ${blogsHostingMax}`;
  return "";
});

// Registration date bubble
const hasRegisteredFilter = computed(() => filterState.value.registeredFromUtc !== null || filterState.value.registeredToUtc !== null);
const registeredLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.registeredFromUtc);
  const to = formatDateForDisplay(filterState.value.registeredToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

const hasBubbles = computed(() => {
  const state = filterState.value;
  const def = DEFAULT_FILTER_STATE;
  return (
    hasActivityFilter.value ||
    hasRoleFilter.value ||
    hasExperienceFilter.value ||
    hasRatingFilter.value ||
    hasGamesHostingFilter.value ||
    hasGamesPlayingFilter.value ||
    hasBlogsFilter.value ||
    hasRegisteredFilter.value ||
    state.sortBy !== def.sortBy ||
    state.sortOrder !== def.sortOrder
  );
});

function clearActivityFilter() {
  setActivity("all");
  setOnlineFilter("all");
}

function clearRoleFilter() {
  setRole("all");
  setHonorary("all");
}

function clearAll() {
  localInput.value = "";
  clearFilters();
}

// Search input keyboard handler
function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    // Remove filters in reverse order
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
      clearRoleFilter();
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
    <!-- Filter bar row -->
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по имени"
        @input="handleInput"
        @keydown="handleSearchKeydown"
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

          <!-- Activity options -->
          <OptionsList
            v-if="navPath?.filter === 'activity'"
            :options="activityListOptions"
            @select="handleActivitySelect"
          />

          <!-- Role options -->
          <OptionsList
            v-if="navPath?.filter === 'role'"
            :options="roleListOptions"
            @select="handleRoleSelect"
          />

          <!-- Honorary options (sub-level for RegularUser) -->
          <OptionsList
            v-if="navPath?.filter === 'honorary'"
            :options="honoraryListOptions"
            @select="handleHonorarySelect"
          />

          <!-- Experience options -->
          <OptionsList
            v-if="navPath?.filter === 'experience'"
            :options="experienceListOptions"
            @select="handleExperienceSelect"
          />

          <!-- Rating range -->
          <NumericRangePicker
            v-if="navPath?.filter === 'rating'"
            :min-value="filterState.ratingMin"
            :max-value="filterState.ratingMax"
            :allow-negative="true"
            min-label="От:"
            max-label="До:"
            @apply="handleRatingApply"
          />

          <!-- Games hosting range -->
          <NumericRangePicker
            v-if="navPath?.filter === 'gamesHosting'"
            :min-value="filterState.gamesHostingMin"
            :max-value="filterState.gamesHostingMax"
            min-label="От:"
            max-label="До:"
            @apply="handleGamesHostingApply"
          />

          <!-- Games playing range -->
          <NumericRangePicker
            v-if="navPath?.filter === 'gamesPlaying'"
            :min-value="filterState.gamesPlayingMin"
            :max-value="filterState.gamesPlayingMax"
            min-label="От:"
            max-label="До:"
            @apply="handleGamesPlayingApply"
          />

          <!-- Blogs hosting range -->
          <NumericRangePicker
            v-if="navPath?.filter === 'blogs'"
            :min-value="filterState.blogsHostingMin"
            :max-value="filterState.blogsHostingMax"
            min-label="От:"
            max-label="До:"
            @apply="handleBlogsApply"
          />

          <!-- Registration date range -->
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
        v-if="hasActivityFilter"
        prefix="Активность:"
        :value="activityLabel"
        @remove="clearActivityFilter"
      />

      <FilterBubble
        v-if="hasRoleFilter"
        prefix="Роль:"
        :value="roleLabel"
        @remove="clearRoleFilter"
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
@import "src/assets/styles/Filters"

.users-filter
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
