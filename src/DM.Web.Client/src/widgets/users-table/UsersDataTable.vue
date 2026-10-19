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
  type User,
} from "@/entities/user";
import { stableCacheKey } from "@/shared/lib/utils/keyedCache";
import { UsersFilter, useUsersFilter } from "@/features/user-filter";
import {
  buildStatusLines,
  type PluralForms,
} from "@/shared/lib/utils/tooltipBuilders";

const communityStore = useCommunityStore();
const { searchResult, searchLoading, searchError } =
  storeToRefs(communityStore);

const { filterState, searchParams, hasActiveFilters } = useUsersFilter();

function retrySearch() {
  communityStore.searchUsers(searchParams.value);
}

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Пользователей по заданным фильтрам не найдено"
    : "Пользователей пока нет",
);
const { isOnline, buildOnlineTooltip, buildRegistrationTooltip } =
  useUserDisplay();

const columnToSortKey: Record<string, string> = {
  username: "username",
  activity: "lastActivity",
  rating: "rating",
  registered: "registered",
};

const sortKeyToColumn: Record<string, string> = Object.fromEntries(
  Object.entries(columnToSortKey).map(([col, sort]) => [sort, col]),
);

// "Имя пользователя" takes the width the others leave; the shares are of the whole table.
const columns: Column[] = [
  {
    key: "username",
    label: "Имя пользователя",
    align: "left",
  },
  {
    key: "activity",
    label: "Активность",
    width: "11.5%",
    align: "center",
  },
  {
    key: "rating",
    label: "Рейтинг",
    width: "10.6%",
    align: "center",
  },
  {
    key: "reviews",
    label: "Рекомендации",
    width: "13.1%",
    align: "center",
  },
  {
    key: "gameReviews",
    label: "Рецензии",
    width: "11.5%",
    align: "center",
  },
  {
    key: "games",
    label: "Игры",
    width: "9%",
    align: "center",
  },
  {
    key: "blogs",
    label: "Блоги",
    width: "9%",
    align: "center",
  },
  {
    key: "registered",
    label: "Регистрация",
    width: "12.3%",
    align: "center",
  },
];

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

const users = computed(() => searchResult.value?.resources ?? []);

// The store's own key builder, so the widget and the store never disagree.
const paramsKey = computed(() => stableCacheKey(searchParams.value));

watch(
  paramsKey,
  () => {
    communityStore.searchUsers(searchParams.value);
  },
  { immediate: true },
);

// Paging scrolls the table itself back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}

const GAME_FORMS: PluralForms = ["игра", "игры", "игр"];

/** [none, heading, fallback]: the words of one role's tooltip. */
function roleTooltip(
  total: number | undefined,
  byStatus: Parameters<typeof buildStatusLines>[0],
  [none, heading, fallback]: [string, string, string],
  forms: PluralForms,
): string {
  if (!total) return none;
  const lines = [heading, ...buildStatusLines(byStatus, forms)];
  if (lines.length === 1) lines.push(`${fallback}: ${total}`);
  return lines.join("\n");
}

function buildHostingTooltip(row: User): string {
  return roleTooltip(
    row.gamesHosting,
    row.gamesHostingByStatus,
    ["Нет игр в роли ведущего", "В роли ведущего:", "Игр"],
    GAME_FORMS,
  );
}

function buildPlayingTooltip(row: User): string {
  return roleTooltip(
    row.gamesPlaying,
    row.gamesPlayingByStatus,
    ["Нет игр в роли игрока", "В роли игрока:", "Игр"],
    GAME_FORMS,
  );
}

function buildBlogsTooltip(row: User): string {
  return roleTooltip(
    row.blogsHosting,
    row.blogsHostingByStatus,
    ["Не ведет блогов", "Ведет блоги:", "Блогов"],
    ["блог", "блога", "блогов"],
  );
}
</script>

<template>
  <div class="users-data-table">
    <UsersFilter class="filters" />

    <ErrorState
      v-if="searchError"
      :message="searchError"
      :retry="retrySearch"
      class="error-state-block"
    />

    <!-- A failed search shows the refusal alone: no empty list, and no rows of the search before it -->
    <DataTable
      v-if="!searchError"
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
      <template #cell-username="{ row }">
        <UserLink :user="row" :search-query="filterState.search" />
      </template>

      <template #cell-rating="{ row }">
        <UserRating :user="row" />
      </template>

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

      <template #cell-registered="{ row }">
        <Tooltip :text="buildRegistrationTooltip(row)" focusable>
          <span>{{ formatDate(row.registeredUtc) }}</span>
        </Tooltip>
      </template>

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

      <!-- One reading of isOnline per row, so the mark and its tooltip agree -->
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

      <template
        v-if="searchResult?.paging && searchResult.paging.pages > 1"
        #footer
      >
        <Paging
          :paging="searchResult.paging"
          :to="{ name: 'community' }"
          :use-query="true"
          :on-prefetch="communityStore.prefetchPage"
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
