<script setup lang="ts">
import { computed, ref, watch, onMounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip, TooltipContent } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { UserLink } from "@/entities/user";
import {
  useGamesStore,
  useGameDisplay,
  GameStatusBadge,
  GameStatus,
  type Game,
} from "@/entities/game";
import { GamesFilter, useGamesFilter } from "@/features/game-filter";
import type { GamesSearchParams } from "@/features/game-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const gamesStore = useGamesStore();
const { searchResult, searchLoading, searchError, tags, tagsError } =
  storeToRefs(gamesStore);

const route = useRoute();

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, hasActiveFilters } = useGamesFilter();

function retrySearch() {
  gamesStore.searchGames(searchParams.value);
}

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Игр по заданным фильтрам не найдено"
    : "Игр пока нет",
);

// O(1) tag lookup Map from cached store tags (for descriptions)
const tagMap = computed(() => {
  const map = new Map<number, { title: string; description?: string }>();
  for (const tag of tags.value ?? []) {
    map.set(tag.id, { title: tag.title, description: tag.description });
  }
  return map;
});

// Get tag info from cache (fallback to id if not loaded yet)
function getTagInfo(tagId: number): { title: string; description?: string } {
  return tagMap.value.get(tagId) ?? { title: `#${tagId}` };
}

// Ensure tags are loaded (sidebar may have loaded them already)
onMounted(() => {
  gamesStore.fetchTags();
});
const {
  buildStatusTooltip,
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
  formatSlots,
  buildSlotsTooltip,
  buildAssistantTooltip,
} = useGameDisplay();

// Title-link colour convention (same as GameLink/BlogLink): closed games are
// muted grey; the green "new" highlight excludes closed (muted takes priority).
function isClosedGame(game: Game): boolean {
  return game.status === GameStatus.Closed;
}
function isNewHighlight(game: Game): boolean {
  return !isClosedGame(game) && isNew(game);
}

// Columns that have direct mapping to sort options
const sortableColumnKeys = new Set(["title", "status"]);

// Define table columns
const columns: Column[] = [
  {
    key: "title",
    label: "Название",
    width: "28%",
    align: "left",
  },
  { key: "master", label: "Ведущие", width: "18%", align: "left" },
  {
    key: "tags",
    label: "Теги",
    width: "20%",
    align: "left",
    hideOnMobile: true,
  },
  {
    key: "status",
    label: "Статус игры",
    width: "16.3%",
    align: "left",
  },
  // One column, one meaning. It used to hold two counts behind a muted "/" —
  // reviews OF THE GAME and ratings of POSTS in it — told apart only by an
  // aria-label. A listing of games is about games, so the column keeps the
  // reviews; the ratings of posts address a player's post and are a row of the
  // game's own fact table now. The 1.7% comes out of "Статус игры", the only
  // column with spare width, so the header word fits inside its cell.
  {
    key: "reviews",
    label: "Рецензии",
    width: "7.7%",
    align: "center",
    hideOnMobile: true,
  },
  { key: "readers", label: "Читатели", width: "7%", align: "center" },
];

// Current sort state for DataTable (only when sorting by a column that exists in the table)
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

// Computed games array
const games = computed(() => searchResult.value?.resources ?? []);

// Create stable key for search params (must include ALL filter params)
function createParamsKey(params: GamesSearchParams): string {
  return JSON.stringify({
    search: params.search || "",
    status: params.status || "",
    recruitmentFilter: params.recruitmentFilter || "",
    closedReasonFilter: params.closedReasonFilter || "",
    requiredTags: params.requiredTags?.slice().sort() || [],
    excludedTags: params.excludedTags?.slice().sort() || [],
    hostUsernames: params.hostUsernames?.slice().sort() || [],
    createdFromUtc: params.createdFromUtc || "",
    createdToUtc: params.createdToUtc || "",
    activatedFromUtc: params.activatedFromUtc || "",
    activatedToUtc: params.activatedToUtc || "",
    closedFromUtc: params.closedFromUtc || "",
    closedToUtc: params.closedToUtc || "",
    recruitmentStartedFromUtc: params.recruitmentStartedFromUtc || "",
    recruitmentStartedToUtc: params.recruitmentStartedToUtc || "",
    sortBy: params.sortBy || "created",
    sortOrder: params.sortOrder || "desc",
    number: params.number || 1,
    size: params.size || 20,
  });
}

const paramsKey = computed(() => createParamsKey(searchParams.value));

// Build a tag-click destination that PRESERVES the current query (adds the
// clicked tag to requiredTags) instead of resetting all other filters.
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

// Fetch games when search params key changes (immediate for initial load)
watch(
  paramsKey,
  () => {
    gamesStore.searchGames(searchParams.value);
  },
  { immediate: true },
);

// Prefetch next page when pagination becomes visible
function handlePrefetch(page: number) {
  gamesStore.prefetchPage(page);
}

// Paging scrolls the table itself back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}
</script>

<template>
  <div class="games-data-table">
    <!-- Filters -->
    <GamesFilter class="filters" />

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
      <!-- Title column: Title (unread/comments) with search highlighting -->
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
        >{{ " "
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

      <!-- Master column -->
      <template #cell-master="{ row }">
        <UserLink
          v-if="row.master"
          :user="row.master"
          :search-query="filterState.search"
          hide-badge
        /><template v-if="row.assistants?.length"
          >{{ " "
          }}<Tooltip :text="buildAssistantTooltip(row.assistants)" focusable>
            <span class="assistant-count">[+{{ row.assistants.length }}]</span>
          </Tooltip></template
        >
      </template>

      <!-- Status column with date tooltip + slots -->
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

      <!-- Tags column: use tagIds + cached store tags for descriptions -->
      <template #cell-tags="{ row }">
        <!-- While the tag catalog hasn't loaded yet, render a shimmer
             placeholder instead of a raw "#id" link target -->
        <div v-if="row.tagIds?.length && !tags && !tagsError" class="tags-list">
          <span
            v-for="tagId in row.tagIds"
            :key="tagId"
            class="tag-shimmer"
          ></span>
        </div>
        <!-- On tag catalog load failure, render plain text (no link,
             no tooltip) rather than a dead "#id" link -->
        <div v-else-if="row.tagIds?.length && tagsError" class="tags-list">
          <template v-for="(tagId, idx) in row.tagIds" :key="tagId"
            ><span class="tag-pending">{{ getTagInfo(tagId).title }}</span
            ><template v-if="idx < row.tagIds.length - 1">, </template>
          </template>
        </div>
        <div v-else-if="row.tagIds?.length" class="tags-list">
          <template v-for="(tagId, idx) in row.tagIds" :key="tagId">
            <!-- Tag with description tooltip from cached store -->
            <Tooltip>
              <template #content>
                <TooltipContent
                  :text="
                    getTagInfo(tagId).description || getTagInfo(tagId).title
                  "
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

      <!-- Reviews column -->
      <template #cell-reviews="{ row }">
        <router-link
          :to="{
            name: 'game-reviews',
            params: { id: row.publicId || row.id },
          }"
          class="review-link"
          :aria-label="`Рецензии: ${row.gameReviewsCount ?? 0}`"
          >{{ row.gameReviewsCount ?? 0 }}</router-link
        >
      </template>

      <!-- Readers column -->
      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)" focusable>
          <span class="readers-count">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <!-- Footer with pagination -->
      <template
        v-if="searchResult?.paging && searchResult.paging.pages > 1"
        #footer
      >
        <Paging
          :paging="searchResult.paging"
          :to="{ name: 'games' }"
          :use-query="true"
          :on-prefetch="handlePrefetch"
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.games-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-state-block
  margin-bottom: $medium

.game-link
  color: $link
  word-wrap: break-word
  overflow-wrap: break-word
  &:hover
    color: $link-hover
  &.new-item
    color: $accent-green
    &:hover
      color: $accent-green-hover
  &.closed-item
    color: $text-muted
    &:hover
      color: $link-hover
  // .search-highlight styled globally in Reset.sass

.tags-list
  line-height: 1.4
  word-wrap: break-word
  overflow-wrap: break-word

.tag-link
  color: $link
  &:hover
    color: $link-hover

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

.slots-indicator
  color: $text-muted
  cursor: help

.assistant-count
  color: $text-muted
  cursor: help

.review-link
  color: $link
  &:hover
    color: $link-hover

.readers-count
  cursor: help
</style>
