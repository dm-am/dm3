<script setup lang="ts">
import { computed, watch, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip, RichText } from "@/shared/ui/Tooltip";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { UserLink } from "@/entities/user";
import {
  useGamesStore,
  useGameDisplay,
  GameStatusBadge,
} from "@/entities/game";
import { GamesFilter, useGamesFilter } from "@/features/game-filter";
import type { GamesSearchParams } from "@/features/game-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const gamesStore = useGamesStore();
const { searchResult, searchLoading, searchError, tags } =
  storeToRefs(gamesStore);

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, hasActiveFilters } = useGamesFilter();

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

// Build slots tooltip with active characters
function buildSlotsTooltip(row: {
  recruitment?: { pcCount: number; pcLimit?: number | null };
  activeCharacters?: { name: string; ownerUsername: string }[];
}): string {
  const chars = row.activeCharacters ?? [];
  const pcCount = row.recruitment?.pcCount ?? 0;
  const pcLimit = row.recruitment?.pcLimit;

  const lines: string[] = [];

  // Characters
  if (chars.length > 0) {
    lines.push("Персонажи:");
    chars.forEach((c) => lines.push(`• ${c.name} (${c.ownerUsername})`));
  } else {
    lines.push("Нет персонажей");
  }

  // Free slots
  if (pcLimit != null) {
    const free = Math.max(0, pcLimit - pcCount);
    if (free > 0) {
      lines.push(`\nСвободных мест: ${free}`);
    } else {
      lines.push("\nМест нет");
    }
  } else {
    lines.push("\nМест: без ограничений");
  }

  return lines.join("\n");
}

// Ensure tags are loaded (sidebar may have loaded them already)
onMounted(() => {
  gamesStore.fetchTags();
});
const {
  buildTooltip,
  buildStatusTooltip,
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
} = useGameDisplay();

// Columns that have direct mapping to sort options
const sortableColumnKeys = new Set(["title", "status"]);

// Define table columns
const columns: Column[] = [
  {
    key: "title",
    label: "Название",
    width: "28%",
    align: "left",
    sortable: true,
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
    width: "18%",
    align: "left",
    sortable: true,
  },
  {
    key: "reviews",
    label: "Отзывы",
    width: "6%",
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

// Build assistant tooltip
function buildAssistantTooltip(assistants: { username: string }[]): string {
  const names = assistants.map((a) => a.username).join(", ");
  return `Ассистент${assistants.length > 1 ? "ы" : ""}: ${names}`;
}

// Format slots display for status column: "[N/M]" or "[N/∞]"
function formatSlots(row: {
  recruitment?: { pcCount: number; pcLimit?: number | null };
}): string {
  const pcCount = row.recruitment?.pcCount ?? 0;
  const pcLimit = row.recruitment?.pcLimit;
  return pcLimit != null ? `[${pcCount}/${pcLimit}]` : `[${pcCount}/∞]`;
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
</script>

<template>
  <div class="games-data-table">
    <!-- Filters -->
    <GamesFilter class="filters" />

    <!-- Error state -->
    <div v-if="searchError" class="error-message">
      {{ searchError }}
    </div>

    <!-- Table. Hidden when the request failed and there is nothing to
         show - an error must not be presented as an empty list. Stale
         rows (if any) stay visible under the error message. -->
    <DataTable
      v-if="!searchError || games.length > 0"
      :columns="columns"
      :data="games"
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
      <!-- Title column: Title (unread/comments) with search highlighting -->
      <template #cell-title="{ row }">
        <Tooltip :text="buildTooltip(row)">
          <router-link
            :to="{ name: 'game', params: { id: row.publicId || row.id } }"
            :class="['game-link', { 'new-item': isNew(row) }]"
          >
            <span
              v-if="filterState.search"
              v-html="highlightMatch(row.title, filterState.search)"
            ></span>
            <template v-else>{{ row.title }}</template>
          </router-link> </Tooltip
        >{{ " "
        }}<span class="counters"
          ><span class="muted">(</span
          ><Tooltip :text="formatUnreadPostsTooltip(getUnreadPosts(row))">
            <!-- First-unread endpoints bind the id as a Guid - pass row.id -->
            <router-link
              :to="{
                name: 'game-first-unread-post',
                params: { id: row.id },
              }"
              :aria-label="formatUnreadPostsTooltip(getUnreadPosts(row))"
              >{{ getUnreadPosts(row) }}</router-link
            > </Tooltip
          ><span class="muted">/</span
          ><Tooltip :text="formatUnreadCommentsTooltip(getUnreadComments(row))">
            <router-link
              :to="{
                name: 'game-first-unread-comment',
                params: { id: row.id },
              }"
              :aria-label="formatUnreadCommentsTooltip(getUnreadComments(row))"
              >{{ getUnreadComments(row) }}</router-link
            > </Tooltip
          ><span class="muted">)</span></span
        >
      </template>

      <!-- Master column -->
      <template #cell-master="{ row }">
        <UserLink
          v-if="row.master"
          :user="row.master"
          :search-query="filterState.search"
          hide-badge
        /><Tooltip
          v-if="row.assistants?.length"
          :text="buildAssistantTooltip(row.assistants)"
        >
          <span class="assistant-count">[+{{ row.assistants.length }}]</span>
        </Tooltip>
      </template>

      <!-- Status column with date tooltip + slots -->
      <template #cell-status="{ row }">
        <Tooltip :text="buildStatusTooltip(row)">
          <span class="status-wrapper">
            <GameStatusBadge
              :status="row.status"
              :is-recruiting="row.recruitment?.isOpen"
              :is-subsequent="row.recruitment?.isSubsequent"
              :closed-reason="row.closedReason"
            />
          </span>
        </Tooltip>
        <Tooltip :text="buildSlotsTooltip(row)">
          <span class="slots-indicator">{{ formatSlots(row) }}</span>
        </Tooltip>
      </template>

      <!-- Tags column: use tagIds + cached store tags for descriptions -->
      <template #cell-tags="{ row }">
        <div v-if="row.tagIds?.length" class="tags-list">
          <template v-for="(tagId, idx) in row.tagIds" :key="tagId">
            <!-- Tag with description tooltip from cached store -->
            <Tooltip>
              <template #content>
                <RichText
                  :text="
                    getTagInfo(tagId).description || getTagInfo(tagId).title
                  "
                />
              </template>
              <router-link
                :to="{ name: 'games', query: { requiredTags: String(tagId) } }"
                class="tag-link"
                >{{ getTagInfo(tagId).title }}</router-link
              > </Tooltip
            ><template v-if="idx < row.tagIds.length - 1">, </template>
          </template>
        </div>
      </template>

      <!-- Reviews column -->
      <template #cell-reviews="{ row }">
        <span class="reviews-cell">
          <Tooltip :text="`Рецензий на игру: ${row.gameReviewsCount ?? 0}`">
            <router-link
              :to="{
                name: 'game-reviews',
                params: { id: row.publicId || row.id },
              }"
              class="review-link"
              >{{ row.gameReviewsCount ?? 0 }}</router-link
            >
          </Tooltip>
          <span class="muted">/</span>
          <Tooltip :text="`Оценок постов: ${row.postReviewsCount ?? 0}`">
            <router-link
              :to="{
                name: 'game-post-reviews',
                params: { id: row.publicId || row.id },
              }"
              class="review-link"
              >{{ row.postReviewsCount ?? 0 }}</router-link
            >
          </Tooltip>
        </span>
      </template>

      <!-- Readers column -->
      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)">
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
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.games-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
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
  // .search-highlight styled globally in Reset.sass

.counters
  white-space: nowrap
  a
    color: $link
    &:hover
      color: $link-hover

.muted
  color: $text-muted

.tags-list
  line-height: 1.4
  word-wrap: break-word
  overflow-wrap: break-word

.tag-link
  color: $link
  &:hover
    color: $link-hover

.status-wrapper
  cursor: help

.slots-indicator
  color: $text-muted
  margin-left: 0.35em
  cursor: help

.assistant-count
  color: $text-muted
  margin-left: 0.25em
  cursor: help

.reviews-cell
  white-space: nowrap

.review-link
  color: $link
  &:hover
    color: $link-hover

.readers-count
  cursor: help
</style>
