<script setup lang="ts">
/**
 * ProfileBlogsTable — the user's hosted blogs (owner or assistant) as a
 * table with search, sort and REAL server-side pagination — same
 * query-driven `number`/`size` contract as the /blogs list page (see
 * widgets/blogs-table/BlogsDataTable.vue), reusing the existing
 * `usePaging().entitiesPerPage` page-size source. Blogs have no "player"
 * role, so there is no host/player toggle — the table covers hosted
 * blogs, and the gray reader-blogs line (ProfileBlogs) covers
 * subscriptions separately.
 *
 * Blog titles link to the blog page ('blog' route).
 *
 * `blogApi.getBlogsByHost` (entities/blog, out of this zone's ownership)
 * doesn't accept `skip`/`number` — it's a single-call-site convenience
 * wrapper for the "load up to N, no paging" case. Rather than extend that
 * shared API surface, this table calls the underlying `Api.get` client
 * directly with the same `hostUsernames` + `skip`/`take` query shape
 * `blogApi.getPublicBlogs` already uses for its own paging conversion.
 *
 * Four states: loading skeleton (DataTable) → error line → empty
 * (two-state) → content.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { Api } from "@/shared/api";
import { BlogStatusBadge, useBlogDisplay, type Blog } from "@/entities/blog";
import { UserLink } from "@/entities/user";
import type { ListEnvelope } from "@/shared/api/models/common";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{
  /** Profile owner whose hosted blogs we list. */
  username: string;
}>();

const route = useRoute();
const router = useRouter();
const { entitiesPerPage } = usePaging();

const {
  buildStatusTooltip,
  buildAssistantTooltip,
  getUnreadPublications,
  getUnreadComments,
  formatUnreadPublicationsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
} = useBlogDisplay();

// `searchInput` is the immediate v-model; `search` is the debounced value
// that actually drives the request (avoids a fetch per keystroke).
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

const pageNumber = computed(() => {
  const raw = route.query.number;
  if (!raw) return 1;
  const n = parseInt(String(raw), 10);
  return !isNaN(n) && n > 0 ? n : 1;
});

const apiParams = computed(() => {
  const pageSize = entitiesPerPage.value;
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
    take: pageSize,
  };
  if (pageNumber.value > 1) {
    params.skip = (pageNumber.value - 1) * pageSize;
  }
  return params;
});

const envelope = ref<ListEnvelope<Blog> | null>(null);

// clearErrorOnStart: what this table did before the composable — the error line
// goes away while the next page loads.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить блоги",
  clearErrorOnStart: true,
});

function fetchBlogs() {
  return run(
    () => Api.get<ListEnvelope<Blog>>("blogs", apiParams.value),
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

// Reset pagination when the search term changes — page 3 of an old query
// is meaningless for a new one and would render a fake-empty table.
watch(search, () => {
  if (route.query.number) {
    const query = { ...route.query };
    delete query.number;
    router.replace({ query });
  }
});

// Re-fetch whenever the effective query changes. Stringify dedupes
// adjacent identical states.
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

// Columns mirror /blogs (BlogsDataTable): same keys, order, widths and
// cell formats.
const columns: Column[] = [
  { key: "title", label: "Название", width: "40%", align: "left" },
  { key: "authors", label: "Ведущие", width: "25%", align: "left" },
  { key: "status", label: "Статус блога", width: "25%", align: "left" },
  { key: "readers", label: "Читатели", width: "10%", align: "center" },
];

// aria-sort only announces columns the table actually renders: "created"
// has no column here (same as /blogs), so sort state maps to "title" only.
const currentSort = computed<SortState | undefined>(() =>
  sortBy.value === "title"
    ? { key: "title", direction: sortOrder.value }
    : undefined,
);

// Closed blogs never get the green "new" highlight (matches BlogLink)
function isNewHighlight(blog: Blog): boolean {
  return blog.status !== "Closed" && isNew(blog);
}

// Paging scrolls the blogs table back into view (not the page top)
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
      :columns="columns"
      :data="blogs"
      :loading="loading"
      :show-row-numbers="true"
      :start-row-number="paging ? paging.skip + 1 : 1"
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <!-- Title column: Title (unread/comments), same format as /blogs -->
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

      <!-- Authors column (unified with games "Ведущие") -->
      <template #cell-authors="{ row }">
        <UserLink v-if="row.author" :user="row.author" hide-badge /><template
          v-if="row.assistants?.length"
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

.controls
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// Search fills all free space (same as the /blogs filter bar, where the
// search input is flex: 1).
.search
  flex: 1
  min-width: 200px

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
