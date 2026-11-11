<script setup lang="ts">
import { computed, ref, watch, onMounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import {
  useGamesStore,
  useGameDisplay,
  GameStatusBadge,
  GameStatus,
  type Game,
} from "@/entities/game";
import { GamesFilter, useGamesFilter } from "@/features/game-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { stableCacheKey } from "@/shared/lib/utils/keyedCache";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { GAME_COLUMNS } from "./columns";

const gamesStore = useGamesStore();
const { searchResult, searchLoading, searchError, tags, tagsError } =
  storeToRefs(gamesStore);

const route = useRoute();

const { filterState, searchParams, hasActiveFilters } = useGamesFilter();

function retrySearch() {
  gamesStore.searchGames(searchParams.value);
}

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Игр по заданным фильтрам не найдено"
    : "Игр пока нет",
);

const tagMap = computed(() => {
  const map = new Map<number, { title: string; description?: string }>();
  for (const tag of tags.value ?? []) {
    map.set(tag.id, { title: tag.title, description: tag.description });
  }
  return map;
});

function getTagInfo(tagId: number): { title: string; description?: string } {
  return tagMap.value.get(tagId) ?? { title: `#${tagId}` };
}

onMounted(() => {
  gamesStore.fetchTags();
});
const {
  buildStatusTooltip,
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
  isNewHighlight,
  formatSlots,
  buildSlotsTooltip,
  buildAssistantTooltip,
} = useGameDisplay();

function isClosedGame(game: Game): boolean {
  return game.status === GameStatus.Closed;
}

const sortableColumnKeys = new Set(["title", "status"]);

const columns: Column[] = [
  GAME_COLUMNS.title,
  GAME_COLUMNS.master,
  GAME_COLUMNS.tags,
  GAME_COLUMNS.status,
  GAME_COLUMNS.reviews,
  GAME_COLUMNS.readers,
];

const currentSort = computed<SortState | undefined>(() => {
  const sortBy = filterState.value.sortBy;
  if (sortableColumnKeys.has(sortBy)) {
    return {
      key: sortBy,
      direction: filterState.value.sortOrder,
    };
  }
  return undefined;
});

const games = computed(() => searchResult.value?.resources ?? []);

// The store's own key builder, so the widget and the store never disagree.
const paramsKey = computed(() => stableCacheKey(searchParams.value));

// A tag adds to the current filters rather than replacing them.
function tagFilterQuery(tagId: number): Record<string, string> {
  const query: Record<string, string> = {};
  for (const [key, value] of Object.entries(route.query)) {
    if (key === "requiredTags" || key === "number") continue;
    if (typeof value === "string") query[key] = value;
  }
  const existing = filterState.value.requiredTags;
  const nextTags = new Set(existing);
  nextTags.add(tagId);
  query.requiredTags = [...nextTags].join(",");
  return query;
}

watch(
  paramsKey,
  () => {
    gamesStore.searchGames(searchParams.value);
  },
  { immediate: true },
);

// Paging scrolls the table itself back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}
</script>

<template>
  <div class="games-data-table">
    <GamesFilter class="filters" />

    <ErrorState
      v-if="searchError"
      :message="searchError"
      :retry="retrySearch"
      class="error-state-block"
    />

    <!-- With nothing to show, a failed request is not presented as an empty list -->
    <DataTable
      v-if="!searchError || games.length > 0"
      ref="tableRef"
      :columns="columns"
      :data="games"
      :loading="searchLoading"
      :show-row-numbers="true"
      :start-row-number="
        searchResult?.paging ? searchResult.paging.skip + 1 : 1
      "
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <template #cell-title="{ row }">
        <router-link
          :to="{ name: 'game', params: { id: row.publicId || row.id } }"
          :class="[
            'game-link',
            {
              'new-item': isNewHighlight(row),
              'closed-item': isClosedGame(row),
            },
          ]"
        >
          <span
            v-if="filterState.search"
            v-html="highlightMatch(row.title, filterState.search)"
          ></span>
          <template v-else>{{ row.title }}</template> </router-link
        >{{
          " "
        }}<!-- First-unread endpoints bind the id as a Guid - pass row.id -->
        <CounterPair
          :first-value="getUnreadPosts(row)"
          :first-to="{ name: 'game-first-unread-post', params: { id: row.id } }"
          :first-label="formatUnreadPostsTooltip(getUnreadPosts(row))"
          :second-value="getUnreadComments(row)"
          :second-to="{
            name: 'game-first-unread-comment',
            params: { id: row.id },
          }"
          :second-label="formatUnreadCommentsTooltip(getUnreadComments(row))"
        />
      </template>

      <template #cell-master="{ row }">
        <UserLink
          v-if="row.master"
          :user="row.master"
          :search-query="filterState.search"
          hide-badge
        /><RemovedAccountLink v-else /><template v-if="row.assistants?.length"
          >{{ " "
          }}<Tooltip :text="buildAssistantTooltip(row.assistants)" focusable>
            <span class="assistant-count">[+{{ row.assistants.length }}]</span>
          </Tooltip></template
        >
      </template>

      <template #cell-status="{ row }">
        <Tooltip :text="buildStatusTooltip(row)" focusable>
          <span class="status-wrapper">
            <GameStatusBadge
              :status="row.status"
              :is-recruiting="row.recruitment?.isOpen"
              :is-subsequent="row.recruitment?.isSubsequent"
              :closed-reason="row.closedReason"
            />
          </span> </Tooltip
        >{{ " "
        }}<Tooltip :text="buildSlotsTooltip(row)" focusable>
          <span class="slots-indicator">{{ formatSlots(row) }}</span>
        </Tooltip>
      </template>

      <template #cell-tags="{ row }">
        <!-- Until the tag catalog loads, a shimmer instead of a raw "#id" link -->
        <template v-if="row.tagIds?.length">
          <div v-if="!tags && !tagsError" class="tags-list">
            <span
              v-for="tagId in row.tagIds"
              :key="tagId"
              class="tag-shimmer"
            ></span>
          </div>
          <!-- A failed tag catalog gives plain text rather than a dead "#id" link -->
          <div v-else-if="tagsError" class="tags-list">
            <template v-for="(tagId, idx) in row.tagIds" :key="tagId"
              ><span class="tag-pending">{{ getTagInfo(tagId).title }}</span
              ><template v-if="idx < row.tagIds.length - 1">, </template>
            </template>
          </div>
          <div v-else class="tags-list">
            <template v-for="(tagId, idx) in row.tagIds" :key="tagId">
              <Tooltip>
                <template #content>
                  <TooltipContent
                    :html="getTagInfo(tagId).description"
                    :text="getTagInfo(tagId).title"
                  />
                </template>
                <router-link
                  :to="{ name: 'games', query: tagFilterQuery(tagId) }"
                  class="tag-link"
                  >{{ getTagInfo(tagId).title }}</router-link
                > </Tooltip
              ><template v-if="idx < row.tagIds.length - 1">, </template>
            </template>
          </div>
        </template>
      </template>

      <template #cell-reviews="{ row }">
        <router-link
          :to="{
            name: 'game-reviews',
            params: { id: row.publicId || row.id },
          }"
          class="review-link"
          :aria-label="`Рецензий: ${row.gameReviewsCount ?? 0}`"
          >{{ row.gameReviewsCount ?? 0 }}</router-link
        >
      </template>

      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)" focusable>
          <span class="readers-count">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <template
        v-if="searchResult?.paging && searchResult.paging.pages > 1"
        #footer
      >
        <Paging
          :paging="searchResult.paging"
          :to="{ name: 'games' }"
          :use-query="true"
          :on-prefetch="gamesStore.prefetchPage"
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *
@use "@/assets/styles/Skeleton" as *

.games-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-state-block
  margin-bottom: $medium

.game-link
  &.new-item
    +link-tone($accent-green, $accent-green-hover)
  &.closed-item
    +muted-link

.tags-list
  line-height: 1.4

.tag-pending
  color: $text-muted

.tag-shimmer
  display: inline-block
  width: 3.5em
  height: 1em
  margin-right: $tiny
  vertical-align: middle
  +skeleton-shimmer

.status-wrapper
  cursor: help

// "[0/∞]" is one token: the cell wraps between words, never inside the brackets.
.slots-indicator
  color: $text-muted
  cursor: help
  white-space: nowrap

.assistant-count
  color: $text-muted
  cursor: help

.readers-count
  cursor: help
</style>
