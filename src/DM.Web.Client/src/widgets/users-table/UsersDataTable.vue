<script setup lang="ts">
import { computed, watch } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  UserLink,
  UserRating,
  useCommunityStore,
  useUserDisplay,
} from "@/entities/user";
import { createCacheKey } from "@/entities/user/model/communityStore";
import { UsersFilter, useUsersFilter } from "@/features/user-filter";
import { buildStatusLines } from "@/shared/lib/utils/tooltipBuilders";

const communityStore = useCommunityStore();
const { searchResult, searchLoading, searchError } =
  storeToRefs(communityStore);

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, hasActiveFilters } = useUsersFilter();

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Пользователей по заданным фильтрам не найдено"
    : "Пользователей пока нет",
);
const {
  isOnline,
  buildOnlineTooltip,
  buildRegistrationTooltip,
  formatDateShort,
} = useUserDisplay();

// Map column keys to sort field names (only for sortable columns)
const columnToSortKey: Record<string, string> = {
  username: "username",
  activity: "lastActivity",
  rating: "rating",
  registered: "registered",
};

// Reverse mapping: sort field to column key
const sortKeyToColumn: Record<string, string> = Object.fromEntries(
  Object.entries(columnToSortKey).map(([col, sort]) => [sort, col]),
);

// Define table columns (UX order: identity → availability → quality →
// counts → tenure). Identifier/date columns left-aligned; short numeric
// count columns centered. Widths sum to 100%.
const columns: Column[] = [
  {
    key: "username",
    label: "Имя пользователя",
    width: "22%",
    align: "left",
    sortable: true,
  },
  {
    key: "activity",
    label: "Активность",
    width: "13%",
    align: "left",
    sortable: true,
    defaultDirection: "desc",
  },
  {
    key: "rating",
    label: "Рейтинг",
    width: "13%",
    align: "center",
    sortable: true,
    defaultDirection: "desc",
  },
  {
    key: "reviews",
    label: "Рекомендации",
    width: "16%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "games",
    label: "Игры",
    width: "11%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "blogs",
    label: "Блоги",
    width: "10%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "registered",
    label: "Регистрация",
    width: "15%",
    align: "left",
    sortable: true,
    defaultDirection: "desc",
    hideOnMobile: true,
  },
];

// Current sort state for DataTable (maps API sort fields to column keys)
const currentSort = computed<SortState | undefined>(() => {
  const sortBy = filterState.value.sortBy;
  const columnKey = sortKeyToColumn[sortBy];
  if (columnKey) {
    return {
      key: columnKey,
      direction: filterState.value.sortOrder,
    };
  }
  return undefined;
});

// Computed users array
const users = computed(() => searchResult.value?.resources ?? []);

// Refetch whenever the cache key (covering every filter param) changes.
// Shares the store's key builder so the widget and store never diverge.
const paramsKey = computed(() => createCacheKey(searchParams.value));

watch(
  paramsKey,
  () => {
    communityStore.searchUsers(searchParams.value);
  },
  { immediate: true },
);

// Prefetch next page when pagination becomes visible
function handlePrefetch(page: number) {
  communityStore.prefetchPage(page);
}

// Build hosting tooltip for games (left number)
function buildHostingTooltip(row: {
  gamesHosting?: number;
  gamesHostingByStatus?: { draft: number; active: number; closed: number };
}): string {
  const total = row.gamesHosting ?? 0;
  if (total === 0) return "Нет игр в роли ведущего";
  const lines = ["В роли ведущего:"];
  lines.push(...buildStatusLines(row.gamesHostingByStatus, "игры"));
  if (lines.length === 1) lines.push(`Игр: ${total}`);
  return lines.join("\n");
}

// Build playing tooltip for games (right number) - includes current and former players
function buildPlayingTooltip(row: {
  gamesPlaying?: number;
  gamesPlayingByStatus?: { draft: number; active: number; closed: number };
}): string {
  const total = row.gamesPlaying ?? 0;
  if (total === 0) return "Нет игр в роли игрока";
  const lines = ["В роли игрока:"];
  lines.push(...buildStatusLines(row.gamesPlayingByStatus, "игры"));
  if (lines.length === 1) lines.push(`Игр: ${total}`);
  return lines.join("\n");
}

// Build endorsements tooltip (recommendations received, not post reviews)
function buildEndorsementsTooltip(row: {
  endorsementsReceived?: number;
}): string {
  const received = row.endorsementsReceived ?? 0;
  return `Рекомендаций: ${received}`;
}

// Build blogs hosting tooltip
function buildBlogsTooltip(row: {
  blogsHosting?: number;
  blogsHostingByStatus?: { draft: number; active: number; closed: number };
}): string {
  const total = row.blogsHosting ?? 0;
  if (total === 0) return "Не ведет блогов";
  const lines = ["Ведет блоги:"];
  lines.push(...buildStatusLines(row.blogsHostingByStatus, "блоги"));
  if (lines.length === 1) lines.push(`Блогов: ${total}`);
  return lines.join("\n");
}
</script>

<template>
  <div class="users-data-table">
    <!-- Filters -->
    <UsersFilter class="filters" />

    <!-- Error state -->
    <div v-if="searchError" class="error-message">
      {{ searchError }}
    </div>

    <!-- Table -->
    <DataTable
      id="results"
      :columns="columns"
      :data="users"
      :loading="searchLoading"
      :show-row-numbers="true"
      :start-row-number="
        searchResult?.paging
          ? (searchResult.paging.current - 1) * searchResult.paging.size + 1
          : 1
      "
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <!-- Username column with role badges and search highlighting -->
      <template #cell-username="{ row }">
        <UserLink :user="row" :search-query="filterState.search" />
      </template>

      <template #cell-rating="{ row }">
        <UserRating :user="row" />
      </template>

      <!-- Games column: X/Y (hosting/playing) — plain counts with tooltips -->
      <template #cell-games="{ row }">
        <span class="games-cell">
          <Tooltip :text="buildHostingTooltip(row)">
            <span class="stats-value">{{ row.gamesHosting ?? 0 }}</span>
          </Tooltip>
          <span class="muted">/</span>
          <Tooltip :text="buildPlayingTooltip(row)">
            <span class="stats-value">{{ row.gamesPlaying ?? 0 }}</span>
          </Tooltip>
        </span>
      </template>

      <!-- Blogs column: X (hosting) — plain count with tooltip -->
      <template #cell-blogs="{ row }">
        <Tooltip :text="buildBlogsTooltip(row)">
          <span class="stats-value">{{ row.blogsHosting ?? 0 }}</span>
        </Tooltip>
      </template>

      <!-- Registration date column -->
      <template #cell-registered="{ row }">
        <Tooltip :text="buildRegistrationTooltip(row)">
          <span>{{ formatDateShort(row.registeredUtc) }}</span>
        </Tooltip>
      </template>

      <!-- Recommendations column: endorsements received — plain count -->
      <template #cell-reviews="{ row }">
        <Tooltip :text="buildEndorsementsTooltip(row)">
          <span class="stats-value">{{ row.endorsementsReceived ?? 0 }}</span>
        </Tooltip>
      </template>

      <!-- Activity column (online/offline status) -->
      <!-- Compute isOnline once per row to ensure consistency between indicator and tooltip -->
      <template #cell-activity="{ row }">
        <template v-for="online in [isOnline(row)]" :key="String(online)">
          <Tooltip :text="buildOnlineTooltip(row, online)">
            <span
              class="online-indicator"
              :class="{ online, offline: !online }"
            >
              {{ online ? "online" : "offline" }}
            </span>
          </Tooltip>
        </template>
      </template>

      <!-- Footer with pagination -->
      <template
        v-if="searchResult?.paging && searchResult.paging.pages > 1"
        #footer
      >
        <Paging
          :paging="searchResult.paging"
          :to="{ name: 'community' }"
          :use-query="true"
          :on-prefetch="handlePrefetch"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.users-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
  margin-bottom: $medium

.stats-value
  color: $text
  cursor: help

.games-cell
  white-space: nowrap

  .muted
    color: $text-muted
    margin: 0 0.1em

.online-indicator
  cursor: help

  &.online
    color: $accent-green

  &.offline
    color: $text-muted
</style>
