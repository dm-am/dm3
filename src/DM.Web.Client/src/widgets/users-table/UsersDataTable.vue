<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { formatDate } from "@/shared/lib/utils/datetime";
import {
  UserLink,
  UserRating,
  useCommunityStore,
  useUserDisplay,
} from "@/entities/user";
import { stableCacheKey } from "@/shared/lib/utils/keyedCache";
import { UsersFilter, useUsersFilter } from "@/features/user-filter";
import { buildStatusLines } from "@/shared/lib/utils/tooltipBuilders";

const communityStore = useCommunityStore();
const { searchResult, searchLoading, searchError } =
  storeToRefs(communityStore);

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, hasActiveFilters } = useUsersFilter();

function retrySearch() {
  communityStore.searchUsers(searchParams.value);
}

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Пользователей по заданным фильтрам не найдено"
    : "Пользователей пока нет",
);
const { isOnline, buildOnlineTooltip, buildRegistrationTooltip } =
  useUserDisplay();

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
    width: "20%",
    align: "left",
  },
  {
    key: "activity",
    label: "Активность",
    width: "12%",
    align: "center",
  },
  {
    key: "rating",
    label: "Рейтинг",
    width: "11%",
    align: "center",
  },
  {
    key: "reviews",
    label: "Рекомендации",
    width: "14%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "gameReviews",
    label: "Рецензии",
    width: "12%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "games",
    label: "Игры",
    width: "9%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "blogs",
    label: "Блоги",
    width: "9%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "registered",
    label: "Регистрация",
    width: "13%",
    align: "center",
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
const paramsKey = computed(() => stableCacheKey(searchParams.value));

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

// Paging scrolls the table itself back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}

// Build hosting tooltip for games (left number)
function buildHostingTooltip(row: {
  gamesHosting?: number;
  gamesHostingByStatus?: { draft: number; active: number; closed: number };
}): string {
  const total = row.gamesHosting ?? 0;
  if (total === 0) return "Нет игр в роли ведущего";
  const lines = ["В роли ведущего:"];
  lines.push(
    ...buildStatusLines(row.gamesHostingByStatus, ["игра", "игры", "игр"]),
  );
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
  lines.push(
    ...buildStatusLines(row.gamesPlayingByStatus, ["игра", "игры", "игр"]),
  );
  if (lines.length === 1) lines.push(`Игр: ${total}`);
  return lines.join("\n");
}

// Build blogs hosting tooltip
function buildBlogsTooltip(row: {
  blogsHosting?: number;
  blogsHostingByStatus?: { draft: number; active: number; closed: number };
}): string {
  const total = row.blogsHosting ?? 0;
  if (total === 0) return "Не ведет блогов";
  const lines = ["Ведет блоги:"];
  lines.push(
    ...buildStatusLines(row.blogsHostingByStatus, ["блог", "блога", "блогов"]),
  );
  if (lines.length === 1) lines.push(`Блогов: ${total}`);
  return lines.join("\n");
}
</script>

<template>
  <div class="users-data-table">
    <!-- Filters -->
    <UsersFilter class="filters" />

    <!-- Error state -->
    <ErrorState
      v-if="searchError"
      :message="searchError"
      :retry="retrySearch"
      class="error-state-block"
    />

    <!-- Table. Hidden when the request failed and there is nothing to
         show - an error must not be presented as an empty list. Stale
         rows (if any) stay visible under the error message. -->
    <DataTable
      v-if="!searchError || users.length > 0"
      ref="tableRef"
      :columns="columns"
      :data="users"
      :loading="searchLoading"
      :show-row-numbers="true"
      :start-row-number="
        searchResult?.paging ? searchResult.paging.skip + 1 : 1
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

      <!-- Games column: X/Y (hosting/playing) — links to the profile -->
      <template #cell-games="{ row }">
        <span class="games-cell">
          <Tooltip :text="buildHostingTooltip(row)">
            <router-link
              :to="{ name: 'profile', params: { username: row.username } }"
              class="stats-value"
              :aria-label="`Игр в роли ведущего: ${row.gamesHosting ?? 0}`"
              >{{ row.gamesHosting ?? 0 }}</router-link
            >
          </Tooltip>
          <span class="muted" aria-hidden="true">/</span>
          <Tooltip :text="buildPlayingTooltip(row)">
            <router-link
              :to="{ name: 'profile', params: { username: row.username } }"
              class="stats-value"
              :aria-label="`Игр в роли игрока: ${row.gamesPlaying ?? 0}`"
              >{{ row.gamesPlaying ?? 0 }}</router-link
            >
          </Tooltip>
        </span>
      </template>

      <!-- Blogs column: X (hosting) — link to the profile -->
      <template #cell-blogs="{ row }">
        <Tooltip :text="buildBlogsTooltip(row)">
          <router-link
            :to="{ name: 'profile', params: { username: row.username } }"
            class="stats-value"
            :aria-label="`Блогов: ${row.blogsHosting ?? 0}`"
            >{{ row.blogsHosting ?? 0 }}</router-link
          >
        </Tooltip>
      </template>

      <!-- Registration date column -->
      <template #cell-registered="{ row }">
        <Tooltip :text="buildRegistrationTooltip(row)" focusable>
          <span>{{ formatDate(row.registeredUtc) }}</span>
        </Tooltip>
      </template>

      <!-- Recommendations column: endorsements received — link to received-endorsements page -->
      <template #cell-reviews="{ row }">
        <router-link
          :to="{
            name: 'received-endorsements',
            params: { username: row.username },
          }"
          class="stats-value"
          :aria-label="`Рекомендаций: ${row.endorsementsReceived ?? 0}`"
          >{{ row.endorsementsReceived ?? 0 }}</router-link
        >
      </template>

      <!-- Game reviews received — the counter's second entry point. Until now
           it existed only as two StatLine rows on the profile, so a reader
           browsing the community had no way to see who is reviewed at all. -->
      <template #cell-gameReviews="{ row }">
        <router-link
          :to="{
            name: 'received-game-reviews',
            params: { username: row.username },
          }"
          class="stats-value"
          :aria-label="`Рецензий: ${row.gameReviewsReceived ?? 0}`"
          >{{ row.gameReviewsReceived ?? 0 }}</router-link
        >
      </template>

      <!-- Activity column (online/offline status) -->
      <!-- Compute isOnline once per row to ensure consistency between indicator and tooltip -->
      <template #cell-activity="{ row }">
        <template v-for="online in [isOnline(row)]" :key="String(online)">
          <Tooltip :text="buildOnlineTooltip(row, online)" focusable>
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
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
.users-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-state-block
  margin-bottom: $medium

.stats-value
  color: $link
  &:hover
    color: $link-hover

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
