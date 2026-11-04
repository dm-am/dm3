<script setup lang="ts">
import { computed, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { vClickOutside } from "@/shared/directives";
import { useGamesFilter, SORT_OPTIONS } from "../model";
import {
  GAME_DATE_RANGES,
  gameDateRange,
  hasRange,
} from "../model/gameDateRanges";
import { useGamesStore } from "@/entities/game";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { useFilterDropdown } from "@/shared/lib/composables/useFilterDropdown";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
  FilterDropdownItem,
  SortButton,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";
import GameStatusMenu from "./GameStatusMenu.vue";
import GameDatesMenu from "./GameDatesMenu.vue";
import GameTagsMenu from "./GameTagsMenu.vue";
import GamesFilterBubbles from "./GamesFilterBubbles.vue";

const filter = useGamesFilter();
const {
  filterState,
  setSearch,
  clearStatus,
  removeTag,
  addHost,
  removeHost,
  setSort,
  toggleSortOrder,
  clearFilters,
  validateTagFilters,
} = filter;

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

const { showDropdown, navPath, closeDropdown, toggleDropdown } =
  useFilterDropdown();

const filterOptions = [
  {
    key: "status",
    label: "Статус игры",
    hint: "Оформляется, Идет игра, Закрыта",
  },
  { key: "host", label: "Ведущие", hint: "Мастер или ассистент" },
  { key: "dates", label: "Даты", hint: "Фильтр по датам" },
  { key: "tag", label: "Тег", hint: "Обязательный тег" },
  { key: "excludeTag", label: "Без тега", hint: "Исключить тег" },
];

function selectRootItem(key: string) {
  navPath.value = { filter: key };
}

function navigateBack() {
  if (!navPath.value) return;

  // Level 4: recruitment sub-options → level 3: Active sub-options
  if (navPath.value.recruitment) {
    navPath.value = {
      filter: navPath.value.filter,
      status: navPath.value.status,
    };
    return;
  }

  // Level 3: inside group or status sub-options → level 2
  if (navPath.value.group || navPath.value.status) {
    navPath.value = { filter: navPath.value.filter };
    return;
  }

  // Level 2 → root
  navPath.value = null;
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;

  if (navPath.value.filter === "status") {
    if (navPath.value.recruitment) return "Набор игроков";
    if (navPath.value.status === "Active") return "Идет игра";
    if (navPath.value.status === "Closed") return "Закрыта";
    return "Статус игры";
  }
  if (navPath.value.filter === "host") return "Ведущие";
  if (navPath.value.filter === "dates") {
    return gameDateRange(navPath.value.group)?.label ?? "Даты";
  }
  if (navPath.value.filter === "tag") return navPath.value.group || "Тег";
  if (navPath.value.filter === "excludeTag")
    return navPath.value.group || "Без тега";

  return null;
}

const gamesStore = useGamesStore();
const { tags: storeTags } = storeToRefs(gamesStore);

// The store dedupes concurrent fetches.
onMounted(async () => {
  await gamesStore.fetchTags();
  if (storeTags.value) {
    validateTagFilters(new Set(storeTags.value.map((t) => t.id)));
  }
});

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

function clearAll() {
  localInput.value = "";
  clearFilters();
}

function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    const state = filterState.value;
    // In reverse order of the bubbles.
    if (state.excludedTags.size > 0) {
      event.preventDefault();
      const lastTag = [...state.excludedTags].pop();
      if (lastTag) removeTag(lastTag);
      return;
    }
    if (state.requiredTags.size > 0) {
      event.preventDefault();
      const lastTag = [...state.requiredTags].pop();
      if (lastTag) removeTag(lastTag);
      return;
    }
    const lastDates = [...GAME_DATE_RANGES]
      .reverse()
      .find((range) => hasRange(state, range));
    if (lastDates) {
      event.preventDefault();
      filter[lastDates.setter](null, null);
      return;
    }
    if (state.hostUsernames.size > 0) {
      event.preventDefault();
      const lastHost = [...state.hostUsernames].pop();
      if (lastHost) removeHost(lastHost);
      return;
    }
    if (state.status !== null) {
      event.preventDefault();
      clearStatus();
      return;
    }
  }
}
</script>

<template>
  <div class="games-filter">
    <div class="filter-bar">
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по названию"
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

          <GameStatusMenu
            v-if="navPath?.filter === 'status'"
            v-model:nav-path="navPath"
            @close="closeDropdown"
          />

          <!-- Include inactive: masters of archived games stay findable. -->
          <UserMultiSelect
            v-if="navPath?.filter === 'host'"
            :selected-users="filterState.hostUsernames"
            placeholder="Поиск ведущего"
            include-inactive
            @add="addHost"
            @remove="removeHost"
          />

          <GameDatesMenu
            v-if="navPath?.filter === 'dates'"
            v-model:nav-path="navPath"
            @close="closeDropdown"
          />

          <GameTagsMenu
            v-if="navPath?.filter === 'tag' || navPath?.filter === 'excludeTag'"
            v-model:nav-path="navPath"
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

    <GamesFilterBubbles @clear-all="clearAll" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.games-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button

+filter-dropdown-base
</style>
