<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { UserLink } from "@/entities/user";
import {
  useBlogsStore,
  useBlogDisplay,
  BlogStatusBadge,
  type Blog,
} from "@/entities/blog";
import { BlogsFilter, useBlogsFilter } from "@/features/blog-filter";
import type { BlogsSearchParams } from "@/features/blog-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const blogsStore = useBlogsStore();
const { searchResult, searchLoading, searchError } = storeToRefs(blogsStore);

// filterState is computed from URL (single source of truth, no sync needed)
const { filterState, searchParams, hasActiveFilters } = useBlogsFilter();

function retrySearch() {
  blogsStore.searchBlogs(searchParams.value);
}

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Блогов по заданным фильтрам не найдено"
    : "Блогов пока нет",
);
const {
  buildStatusTooltip,
  buildAssistantTooltip,
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
  {
    key: "title",
    label: "Название",
    width: "40%",
    align: "left",
  },
  { key: "authors", label: "Ведущие", width: "25%", align: "left" },
  {
    key: "status",
    label: "Статус блога",
    width: "25%",
    align: "left",
  },
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

// Closed blogs never get the green "new" highlight (matches BlogLink)
function isNewHighlight(blog: Blog): boolean {
  return blog.status !== "Closed" && isNew(blog);
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

// Paging scrolls the table itself back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}
</script>

<template>
  <div class="blogs-data-table">
    <!-- Filters -->
    <BlogsFilter class="filters" />

    <!-- Error state -->
    <ErrorState
      v-if="searchError"
      :message="searchError"
      :retry="retrySearch"
      class="error-state-block"
    />

    <!-- Table. Hidden when the request failed and there is nothing to show,
         so the empty-state text never appears next to the error message -->
    <DataTable
      v-if="!searchError || blogs.length > 0"
      ref="tableRef"
      :columns="columns"
      :data="blogs"
      :loading="searchLoading"
      :show-row-numbers="true"
      :start-row-number="
        searchResult?.paging ? searchResult.paging.skip + 1 : 1
      "
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <!-- Title column: Title (unread/comments) -->
      <template #cell-title="{ row }">
        <router-link
          :to="{ name: 'blog', params: { id: row.publicId ?? row.id } }"
          :class="[
            'blog-link',
            {
              'new-item': isNewHighlight(row),
              'closed-item': row.status === 'Closed',
            },
          ]"
        >
          <span
            v-if="filterState.search"
            v-html="highlightMatch(row.title, filterState.search)"
          ></span>
          <template v-else>{{ row.title }}</template> </router-link
        >{{ " "
        }}<CounterPair
          :first-value="getUnreadPublications(row)"
          :first-to="{ name: 'blog', params: { id: row.publicId ?? row.id } }"
          :first-label="
            formatUnreadPublicationsTooltip(getUnreadPublications(row))
          "
          :second-value="getUnreadComments(row)"
          :second-to="{ name: 'blog', params: { id: row.publicId ?? row.id } }"
          :second-label="formatUnreadCommentsTooltip(getUnreadComments(row))"
        />
      </template>

      <!-- Authors column (unified with games "Ведущие") -->
      <template #cell-authors="{ row }">
        <UserLink
          v-if="row.author"
          :user="row.author"
          :search-query="filterState.search"
          hide-badge
        /><template v-if="row.assistants?.length"
          >{{ " "
          }}<Tooltip :text="buildAssistantTooltip(row.assistants)" focusable>
            <span class="assistant-count">[+{{ row.assistants.length }}]</span>
          </Tooltip></template
        >
      </template>

      <!-- Status column with date tooltip -->
      <template #cell-status="{ row }">
        <Tooltip :text="buildStatusTooltip(row)" focusable>
          <span class="status-wrapper">
            <BlogStatusBadge :status="row.status" />
          </span>
        </Tooltip>
      </template>

      <!-- Readers column (unified with games) -->
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
          :to="{ name: 'blogs' }"
          :use-query="true"
          :on-prefetch="handlePrefetch"
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
.blogs-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-state-block
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
  &.closed-item
    color: $text-muted
    &:hover
      color: $link-hover

.status-wrapper
  cursor: help

.assistant-count
  color: $text-muted
  cursor: help

.readers-count
  cursor: help
</style>
