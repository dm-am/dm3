<script setup lang="ts">
import { computed, watch } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { UserLink, useCommunityStore, useUserDisplay } from "@/entities/user";
import { UsersFilter, useUsersFilter } from "@/features/user-filter";
import type { UsersSearchParams } from "@/features/user-filter";
import { buildSubscribersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const communityStore = useCommunityStore();
const { searchResult, searchLoading, searchError } = storeToRefs(communityStore);

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, setSort, hasActiveFilters } = useUsersFilter();

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value ? "Пользователей по заданным фильтрам не найдено" : "Пользователей пока нет"
);
const {
  isOnline,
  buildRatingTooltip,
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

// Define table columns (UX order: identity → availability → quality → activity → social → tenure)
const columns: Column[] = [
  { key: "username", label: "Имя пользователя", width: "16%", align: "left", sortable: true },
  { key: "activity", label: "Активность", width: "12%", align: "left", sortable: true },
  { key: "rating", label: "Рейтинг", width: "13%", align: "left", sortable: true },
  { key: "reviews", label: "Рекомендации", width: "16%", align: "center", hideOnMobile: true },
  { key: "games", label: "Игры", width: "8%", align: "center", hideOnMobile: true },
  { key: "blogs", label: "Блоги", width: "7%", align: "center", hideOnMobile: true },
  { key: "subscribers", label: "Подписчики", width: "9%", align: "center", hideOnMobile: true },
  { key: "registered", label: "Регистрация", width: "13%", align: "left", sortable: true, hideOnMobile: true },
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

// Handle column header click for sorting (maps column keys to API sort fields)
function handleSort(column: Column, direction: "asc" | "desc") {
  const sortKey = columnToSortKey[column.key] || column.key;
  setSort(sortKey, direction);
}

// Computed users array
const users = computed(() => searchResult.value?.resources ?? []);

// Create stable key for search params (must include ALL filter params)
function createParamsKey(params: UsersSearchParams): string {
  return JSON.stringify({
    search: params.search || "",
    activity: params.activity || "active",
    isOnline: params.isOnline ?? false,
    role: params.role || "",
    isHonorary: params.isHonorary ?? false,
    isNewbie: params.isNewbie,
    minRating: params.minRating,
    maxRating: params.maxRating,
    minGamesHosting: params.minGamesHosting,
    maxGamesHosting: params.maxGamesHosting,
    minGamesPlaying: params.minGamesPlaying,
    maxGamesPlaying: params.maxGamesPlaying,
    minBlogsHosting: params.minBlogsHosting,
    maxBlogsHosting: params.maxBlogsHosting,
    registeredFromUtc: params.registeredFromUtc || "",
    registeredToUtc: params.registeredToUtc || "",
    sortBy: params.sortBy || "lastActivity",
    sortOrder: params.sortOrder || "desc",
    number: params.number || 1,
    size: params.size || 20,
  });
}

const paramsKey = computed(() => createParamsKey(searchParams.value));

// Fetch users when search params key changes
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

// Build status breakdown lines for tooltip
function buildStatusLines(counts: { draft: number; active: number; closed: number } | undefined, itemName: string): string[] {
  if (!counts) return [];
  const lines: string[] = [];
  if (counts.draft > 0) lines.push(`• Подготавливаемые ${itemName}: ${counts.draft}`);
  if (counts.active > 0) lines.push(`• Активные ${itemName}: ${counts.active}`);
  if (counts.closed > 0) lines.push(`• Закрытые ${itemName}: ${counts.closed}`);
  return lines;
}

// Build hosting tooltip for games (left number)
function buildHostingTooltip(row: { gamesHosting?: number; gamesHostingByStatus?: { draft: number; active: number; closed: number } }): string {
  const total = row.gamesHosting ?? 0;
  if (total === 0) return "Нет игр в роли ведущего";
  const lines = ["В роли ведущего:"];
  lines.push(...buildStatusLines(row.gamesHostingByStatus, "игры"));
  if (lines.length === 1) lines.push(`Игр: ${total}`);
  return lines.join("\n");
}

// Build playing tooltip for games (right number) - includes current and former players
function buildPlayingTooltip(row: { gamesPlaying?: number; gamesPlayingByStatus?: { draft: number; active: number; closed: number } }): string {
  const total = row.gamesPlaying ?? 0;
  if (total === 0) return "Нет игр в роли игрока";
  const lines = ["В роли игрока:"];
  lines.push(...buildStatusLines(row.gamesPlayingByStatus, "игры"));
  if (lines.length === 1) lines.push(`Игр: ${total}`);
  return lines.join("\n");
}

// Build endorsements tooltip
function buildEndorsementsTooltip(row: { reviewsReceived?: number }): string {
  const received = row.reviewsReceived ?? 0;
  return `Рекомендаций: ${received}`;
}

// Build blogs hosting tooltip
function buildBlogsTooltip(row: { blogsHosting?: number; blogsHostingByStatus?: { draft: number; active: number; closed: number } }): string {
  const total = row.blogsHosting ?? 0;
  if (total === 0) return "Не ведет блогов";
  const lines = ["Ведет блоги:"];
  lines.push(...buildStatusLines(row.blogsHostingByStatus, "блоги"));
  if (lines.length === 1) lines.push(`Блогов: ${total}`);
  return lines.join("\n");
}

// Get CSS class for rating quality score (green if positive, red if negative, default for zero)
function getRatingClass(score: number): string {
  if (score > 0) return "positive";
  if (score < 0) return "negative";
  return "";
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
      @sort="handleSort"
    >
      <!-- Username column with role/honorary badges and search highlighting -->
      <template #cell-username="{ row }">
        <UserLink :user="row" :search-query="filterState.search" />
      </template>

      <!-- Rating column: quality/quantity or n/a if disabled -->
      <template #cell-rating="{ row }">
        <template v-if="row.rating">
          <Tooltip text="Сумма оценок постов">
            <router-link
              :to="{ name: 'profile', params: { username: row.username } }"
              class="rating-quality"
              :class="getRatingClass(row.rating.postReviewScoreSum)"
              >{{ row.rating.postReviewScoreSum }}</router-link
            ></Tooltip
          ><span class="rating-separator">/</span
          ><Tooltip text="Количество постов"
            ><span class="rating-quantity">{{ row.rating.totalPosts }}</span></Tooltip
          >
        </template>
        <Tooltip :text="buildRatingTooltip(row)">
          <router-link
            v-if="!row.rating"
            :to="{ name: 'profile', params: { username: row.username } }"
            class="rating-na"
            >n/a</router-link
          >
        </Tooltip>
      </template>

      <!-- Games column: X/Y (hosting/playing) with separate tooltips -->
      <template #cell-games="{ row }">
        <span class="games-cell">
          <Tooltip :text="buildHostingTooltip(row)">
            <span class="stats-cell">{{ row.gamesHosting ?? 0 }}</span>
          </Tooltip>
          <span class="muted">/</span>
          <Tooltip :text="buildPlayingTooltip(row)">
            <span class="stats-cell">{{ row.gamesPlaying ?? 0 }}</span>
          </Tooltip>
        </span>
      </template>

      <!-- Blogs column: X (hosting) -->
      <template #cell-blogs="{ row }">
        <Tooltip :text="buildBlogsTooltip(row)">
          <span class="stats-cell">{{ row.blogsHosting ?? 0 }}</span>
        </Tooltip>
      </template>

      <!-- Registration date column -->
      <template #cell-registered="{ row }">
        <Tooltip :text="buildRegistrationTooltip(row)">
          <span>{{ formatDateShort(row.registeredUtc) }}</span>
        </Tooltip>
      </template>

      <!-- Reviews column: received count with link to profile -->
      <template #cell-reviews="{ row }">
        <Tooltip :text="buildEndorsementsTooltip(row)">
          <router-link
            :to="{ name: 'profile', params: { username: row.username } }"
            class="reviews-link"
          >
            {{ row.reviewsReceived ?? 0 }}
          </router-link>
        </Tooltip>
      </template>

      <!-- Activity column (online/offline status) -->
      <!-- Compute isOnline once per row to ensure consistency between indicator and tooltip -->
      <template #cell-activity="{ row }">
        <template v-for="online in [isOnline(row)]" :key="0">
          <Tooltip :text="buildOnlineTooltip(row, online)">
            <span class="online-indicator" :class="{ online, offline: !online }">
              {{ online ? "online" : "offline" }}
            </span>
          </Tooltip>
        </template>
      </template>

      <!-- Subscribers column (like readers in games) -->
      <template #cell-subscribers="{ row }">
        <Tooltip :text="buildSubscribersTooltip(row)">
          <span class="stats-cell">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <!-- Footer with pagination -->
      <template v-if="searchResult?.paging && searchResult.paging.pages > 1" #footer>
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

.stats-cell
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

// Rating column styles
.rating-quality
  font-weight: bold
  color: $link
  &:hover
    color: $link-hover
  &.positive
    color: $accent-green
    &:hover
      color: $accent-green-hover
  &.negative
    color: $accent-red
    &:hover
      color: $accent-red-hover

.rating-separator
  color: $text-muted
  margin: 0 0.15em

.rating-quantity
  color: $text
  cursor: help

.rating-na
  color: $link
  &:hover
    color: $link-hover

.reviews-link
  color: $text
  &:hover
    color: $link-hover
</style>
