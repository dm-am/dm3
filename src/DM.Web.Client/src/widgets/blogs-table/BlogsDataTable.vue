<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import {
  useBlogsStore,
  useBlogDisplay,
  BlogStatusBadge,
  type Blog,
} from "@/entities/blog";
import { BlogsFilter, useBlogsFilter } from "@/features/blog-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { stableCacheKey } from "@/shared/lib/utils/keyedCache";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { BLOG_COLUMNS } from "./columns";

const blogsStore = useBlogsStore();
const { searchResult, searchLoading, searchError } = storeToRefs(blogsStore);

const { filterState, searchParams, hasActiveFilters } = useBlogsFilter();

function retrySearch() {
  blogsStore.searchBlogs(searchParams.value);
}

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

const sortableColumnKeys = new Set(["title", "status"]);

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

const blogs = computed(() => searchResult.value?.resources ?? []);

// The store's own key builder, so the widget and the store never disagree.
const paramsKey = computed(() => stableCacheKey(searchParams.value));

function blogRoute(blog: Blog) {
  return { name: "blog", params: { id: blog.publicId ?? blog.id } };
}

watch(
  paramsKey,
  () => {
    blogsStore.searchBlogs(searchParams.value);
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
  <div class="blogs-data-table">
    <BlogsFilter class="filters" />

    <ErrorState
      v-if="searchError"
      :message="searchError"
      :retry="retrySearch"
      class="error-state-block"
    />

    <!-- With nothing to show, a failed request gets no empty text beside its error -->
    <DataTable
      v-if="!searchError || blogs.length > 0"
      ref="tableRef"
      :columns="BLOG_COLUMNS"
      :data="blogs"
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
          :to="blogRoute(row)"
          :class="[
            'blog-link',
            {
              'new-item': isNew(row),
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
          :first-to="blogRoute(row)"
          :first-label="
            formatUnreadPublicationsTooltip(getUnreadPublications(row))
          "
          :second-value="getUnreadComments(row)"
          :second-to="blogRoute(row)"
          :second-label="formatUnreadCommentsTooltip(getUnreadComments(row))"
        />
      </template>

      <template #cell-authors="{ row }">
        <UserLink
          v-if="row.author"
          :user="row.author"
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
            <BlogStatusBadge :status="row.status" />
          </span>
        </Tooltip>
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
          :to="{ name: 'blogs' }"
          :use-query="true"
          :on-prefetch="blogsStore.prefetchPage"
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.blogs-data-table
  width: 100%

.filters
  margin-bottom: $medium

.error-state-block
  margin-bottom: $medium

.blog-link
  &.new-item
    +link-tone($accent-green, $accent-green-hover)
  &.closed-item
    +muted-link

.status-wrapper
  cursor: help

.assistant-count
  color: $text-muted
  cursor: help

.readers-count
  cursor: help
</style>
