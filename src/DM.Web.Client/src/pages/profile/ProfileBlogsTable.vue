<script setup lang="ts">
/**
 * The blogs the user hosts as a table with search, sort and server paging, on
 * the contract of /blogs.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { DataTable, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  BlogStatusBadge,
  blogApi,
  useBlogDisplay,
  type Blog,
} from "@/entities/blog";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import { BLOG_COLUMNS } from "@/widgets/blogs-table";
import type { ListEnvelope } from "@/shared/api/models/common";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { parsePageNumber } from "@/shared/lib/filters";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{
  /** Profile owner whose hosted blogs we list. */
  username: string;
}>();

const route = useRoute();
const router = useRouter();
const { pageSize } = usePaging();

const {
  buildStatusTooltip,
  buildAssistantTooltip,
  getUnreadPublications,
  getUnreadComments,
  formatUnreadPublicationsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
} = useBlogDisplay();

// `search` is the debounced `searchInput`, one fetch per pause.
const searchInput = ref("");
const search = ref("");
const sortBy = ref<"created" | "title">("created");
const sortOrder = ref<"asc" | "desc">("desc");

let searchTimer: ReturnType<typeof setTimeout> | undefined;
watch(searchInput, (value) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    search.value = value.trim();
  }, 300);
});

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

const apiParams = computed(() => {
  const size = pageSize.value;
  const params: {
    hostUsernames: string[];
    search?: string;
    sortBy: string;
    sortOrder: string;
    take: number;
    skip?: number;
  } = {
    hostUsernames: [props.username],
    search: search.value || undefined,
    sortBy: sortBy.value,
    sortOrder: sortOrder.value,
    take: size,
  };
  if (pageNumber.value > 1) {
    params.skip = (pageNumber.value - 1) * size;
  }
  return params;
});

const envelope = ref<ListEnvelope<Blog> | null>(null);

// The error line goes while the next page loads.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить блоги",
  clearErrorOnStart: true,
});

function fetchBlogs() {
  return run(
    () => blogApi.getHostedBlogs(apiParams.value),
    (data) => {
      envelope.value = data;
    },
  );
}

const blogs = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);

const hasActiveFilters = computed(() => search.value.trim().length > 0);

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Блогов по заданным фильтрам не найдено"
    : "Пользователь не ведет блогов",
);

// A page of the old query means nothing for a new one.
watch(search, () => {
  if (route.query.number) {
    const query = { ...route.query };
    delete query.number;
    router.replace({ query });
  }
});

// Refetch on every change of the query; the string dedupes identical ones.
const paramsKey = computed(() => JSON.stringify(apiParams.value));
watch(paramsKey, () => fetchBlogs(), { immediate: true });

const sortOptions = [
  {
    value: "created",
    label: "Дата создания",
    defaultDirection: "desc" as const,
  },
  { value: "title", label: "Название", defaultDirection: "asc" as const },
];

function handleSortSelect(value: string, direction?: "asc" | "desc") {
  sortBy.value = value === "title" ? "title" : "created";
  sortOrder.value = direction ?? "desc";
}

function handleSortOrder(order: "asc" | "desc") {
  sortOrder.value = order;
}

// aria-sort for drawn columns only: "created" has none, so the sort maps to "title".
const currentSort = computed<SortState | undefined>(() =>
  sortBy.value === "title"
    ? { key: "title", direction: sortOrder.value }
    : undefined,
);

// Paging scrolls the table back into view, not the page top.
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}
</script>

<template>
  <div class="profile-blogs-table">
    <div class="controls">
      <FilterSearchInput
        v-model="searchInput"
        placeholder="Поиск по названию"
        class="search"
      />

      <SortButton
        :options="sortOptions"
        :sort-by="sortBy"
        :sort-order="sortOrder"
        @sort-select="handleSortSelect"
        @update:sort-order="handleSortOrder"
      />
    </div>

    <ErrorState v-if="error" :message="error" :retry="fetchBlogs" />

    <DataTable
      v-if="!error || blogs.length > 0"
      ref="tableRef"
      :columns="BLOG_COLUMNS"
      :data="blogs"
      :loading="loading"
      :show-row-numbers="true"
      :start-row-number="paging ? paging.skip + 1 : 1"
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <!-- Title, unread and comments, as /blogs. -->
      <template #cell-title="{ row }">
        <router-link
          :to="{ name: 'blog', params: { id: row.publicId ?? row.id } }"
          :class="[
            'blog-link',
            {
              'new-item': isNew(row),
              'closed-item': row.status === 'Closed',
            },
          ]"
        >
          <span v-if="search" v-html="highlightMatch(row.title, search)"></span>
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

      <!-- Authors, as the masters of a game. -->
      <template #cell-authors="{ row }">
        <UserLink
          v-if="row.author"
          :user="row.author"
          hide-badge
        /><RemovedAccountLink v-else /><template v-if="row.assistants?.length"
          >{{ " "
          }}<Tooltip :text="buildAssistantTooltip(row.assistants)" focusable>
            <span class="assistant-count">[+{{ row.assistants.length }}]</span>
          </Tooltip></template
        >
      </template>

      <!-- Status, its date in the tooltip. -->
      <template #cell-status="{ row }">
        <Tooltip :text="buildStatusTooltip(row)" focusable>
          <span class="status-wrapper">
            <BlogStatusBadge :status="row.status" />
          </span>
        </Tooltip>
      </template>

      <!-- Readers, as on games. -->
      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)" focusable>
          <span class="readers-count">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <template v-if="paging && paging.pages > 1" #footer>
        <Paging
          :paging="paging"
          :to="{ name: 'profile', params: { username } }"
          :use-query="true"
          query-key="number"
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
.profile-blogs-table
  display: flex
  flex-direction: column
  gap: $medium

// The sort goes under the field whole where the two do not fit one line.
.controls
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// The search takes the free space, as on /blogs.
.search
  flex: 1
  min-width: $search-field-min-width

.blog-link
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
