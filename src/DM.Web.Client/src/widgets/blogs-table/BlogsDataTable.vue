<script setup lang="ts">
import { computed, watch } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { UserLink } from "@/entities/user";
import {
  useBlogsStore,
  useBlogDisplay,
  BlogStatusBadge,
} from "@/entities/blog";
import { BlogsFilter, useBlogsFilter } from "@/features/blog-filter";
import type { BlogsSearchParams } from "@/features/blog-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const blogsStore = useBlogsStore();
const { searchResult, searchLoading, searchError } = storeToRefs(blogsStore);

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, setSort, hasActiveFilters } = useBlogsFilter();

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value ? "Блогов по заданным фильтрам не найдено" : "Блогов пока нет"
);
const {
  buildTooltip,
  buildStatusTooltip,
  getUnreadPublications,
  getUnreadComments,
  formatUnreadPublicationsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
} = useBlogDisplay();

// Columns that have direct mapping to sort options
const sortableColumnKeys = new Set(["title", "status"]);

// Define table columns (unified with GamesDataTable)
const columns: Column[] = [
  { key: "title", label: "Название", width: "40%", align: "left", sortable: true },
  { key: "authors", label: "Ведущие", width: "25%", align: "left" },
  { key: "status", label: "Статус блога", width: "25%", align: "left", sortable: true },
  { key: "readers", label: "Читатели", width: "10%", align: "center" },
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

// Handle column header click for sorting
function handleSort(column: Column, direction: "asc" | "desc") {
  setSort(column.key, direction);
}

// Computed blogs array
const blogs = computed(() => searchResult.value?.resources ?? []);

// Create stable key for search params (must include ALL filter params)
function createParamsKey(params: BlogsSearchParams): string {
  return JSON.stringify({
    search: params.search || "",
    status: params.status || "",
    hostUsernames: params.hostUsernames?.slice().sort() || [],
    createdFromUtc: params.createdFromUtc || "",
    createdToUtc: params.createdToUtc || "",
    activatedFromUtc: params.activatedFromUtc || "",
    activatedToUtc: params.activatedToUtc || "",
    closedFromUtc: params.closedFromUtc || "",
    closedToUtc: params.closedToUtc || "",
    sortBy: params.sortBy || "created",
    sortOrder: params.sortOrder || "desc",
    number: params.number || 1,
    size: params.size || 20,
  });
}

const paramsKey = computed(() => createParamsKey(searchParams.value));

// Build assistant tooltip (unified with games)
function buildAssistantTooltip(assistants: { username: string }[]): string {
  const names = assistants.map((a) => a.username).join(", ");
  return `Ассистент${assistants.length > 1 ? "ы" : ""}: ${names}`;
}

// Fetch blogs when search params key changes (immediate for initial load)
watch(
  paramsKey,
  () => {
    blogsStore.searchBlogs(searchParams.value);
  },
  { immediate: true },
);

// Prefetch next page when pagination becomes visible
function handlePrefetch(page: number) {
  blogsStore.prefetchPage(page);
}
</script>

<template>
  <div class="blogs-data-table">
    <!-- Filters -->
    <BlogsFilter class="filters" />

    <!-- Error state -->
    <div v-if="searchError" class="error-message">
      {{ searchError }}
    </div>

    <!-- Table -->
    <DataTable
      id="results"
      :columns="columns"
      :data="blogs"
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
      <!-- Title column: Title (unread/comments) -->
      <!-- TODO: Change to { name: 'blog', params: { id: row.id } } when blog detail page exists -->
      <template #cell-title="{ row }">
        <Tooltip :text="buildTooltip(row)">
          <router-link
            :to="{ name: 'blogs' }"
            :class="['blog-link', { 'new-item': isNew(row) }]"
          >
            <span v-if="filterState.search" v-html="highlightMatch(row.title, filterState.search)"></span>
            <template v-else>{{ row.title }}</template>
          </router-link
          >
        </Tooltip>{{ " "
        }}<span class="counters"
          ><span class="muted">(</span
          ><Tooltip :text="formatUnreadPublicationsTooltip(getUnreadPublications(row))">
            <router-link :to="{ name: 'blogs' }">{{
              getUnreadPublications(row)
            }}</router-link>
          </Tooltip
          ><span class="muted">/</span
          ><Tooltip :text="formatUnreadCommentsTooltip(getUnreadComments(row))">
            <router-link :to="{ name: 'blogs' }">{{
              getUnreadComments(row)
            }}</router-link>
          </Tooltip
          ><span class="muted">)</span></span
        >
      </template>

      <!-- Authors column (unified with games "Ведущие") -->
      <template #cell-authors="{ row }">
        <UserLink v-if="row.author" :user="row.author" :search-query="filterState.search" hide-badge /><Tooltip
          v-if="row.assistants?.length"
          :text="buildAssistantTooltip(row.assistants)"
        >
          <span class="assistant-count">[+{{ row.assistants.length }}]</span>
        </Tooltip>
      </template>

      <!-- Status column with date tooltip -->
      <template #cell-status="{ row }">
        <Tooltip :text="buildStatusTooltip(row)">
          <span class="status-wrapper">
            <BlogStatusBadge :status="row.status" />
          </span>
        </Tooltip>
      </template>

      <!-- Readers column (unified with games) -->
      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)">
          <span class="readers-count">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <!-- Footer with pagination -->
      <template v-if="searchResult?.paging && searchResult.paging.pages > 1" #footer>
        <Paging
          :paging="searchResult.paging"
          :to="{ name: 'blogs' }"
          :use-query="true"
          :on-prefetch="handlePrefetch"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.blogs-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
  margin-bottom: $medium

.blog-link
  color: $link
  word-wrap: break-word
  overflow-wrap: break-word
  &:hover
    color: $link-hover
  &.new-item
    color: $accent-green
    &:hover
      color: $accent-green-hover

.counters
  white-space: nowrap
  a
    color: $link
    &:hover
      color: $link-hover

.muted
  color: $text-muted

.status-wrapper
  cursor: help

.assistant-count
  color: $text-muted
  margin-left: 0.25em
  cursor: help

.readers-count
  cursor: help
</style>
