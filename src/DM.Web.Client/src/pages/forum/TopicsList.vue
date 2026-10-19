<script setup lang="ts">
import { computed, watch, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { DataTable, type SortState } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ContentText } from "@/shared/ui/Content";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { useBoardsStore } from "@/entities/forum";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { TopicsFilter, useTopicsFilter } from "@/features/topic-filter";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ActionResult } from "@/shared/ui/ActionResult";
import { TOPIC_COLUMNS, type DisplayTopic } from "./topicColumns";
import { useTopicPins } from "./useTopicPins";
import TopicTitleCell from "./TopicTitleCell.vue";
import TopicCreateForm from "./TopicCreateForm.vue";

const route = useRoute();
const store = useBoardsStore();
const { topics, attachedTopics, topicsLoading, topicsError, selectedBoard } =
  storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// The leaf owns the title; TopicPage owns it on the topic route.
useDocumentTitle(() => selectedBoard.value?.title);

const { filterState, searchParams, hasActiveFilters } = useTopicsFilter();
const { pageSize } = usePaging();

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Топиков по заданным фильтрам не найдено"
    : "Топиков пока нет",
);

// The sort told to the table: its header announces it (aria-sort).
const currentSort = computed<SortState>(() => ({
  key: filterState.value.sortBy,
  direction: filterState.value.sortOrder,
}));

// The listing mixes the pinned in whenever the store did not fetch them apart
// (attachedTopics === null): the store's own signal, so the two cannot drift.
const isUnifiedListing = computed(() => attachedTopics.value === null);

// The pinned block only on page 1, or it would repeat on every page.
const showPinnedBlock = computed(() => {
  if (isUnifiedListing.value) return false;
  const pageParam = route.query.number;
  if (!pageParam) return true;
  const page = parseInt(String(pageParam), 10);
  return !Number.isFinite(page) || page <= 1;
});

const tableScrollRef = ref<HTMLElement | null>(null);

const pins = useTopicPins({ showPinnedBlock, table: tableScrollRef });
const { canReorder, pinnedOrder, pinRefusal } = pins;

const displayTopics = computed<DisplayTopic[]>(() => {
  if (isUnifiedListing.value) {
    // Filtered: the results alone, isPinned kept so the marker survives a sort.
    return (topics.value?.resources ?? []).map((t) => ({
      ...t,
      isPinned: t.isAttached,
    }));
  }

  // Unfiltered: the pinned first, in the order a move has just given them.
  const pinned: DisplayTopic[] = showPinnedBlock.value
    ? pinnedOrder.order.value.map((t) => ({ ...t, isPinned: true }))
    : [];
  const regular: DisplayTopic[] = (topics.value?.resources ?? []).map((t) => ({
    ...t,
    isPinned: t.isAttached,
  }));
  return [...pinned, ...regular];
});

// Comments paging reads its page from "number".
function topicLink(row: DisplayTopic, page?: number) {
  return {
    name: "topic",
    params: {
      alias: selectedBoard.value?.alias || route.params.alias,
      num: row.topicNumber,
    },
    query: page && page > 1 ? { number: page } : undefined,
  };
}

/** The topic at the last page, where its newest comment stands with the default sort. */
function lastCommentLink(row: DisplayTopic) {
  const lastPage = Math.max(
    1,
    Math.ceil((row.commentsCount || 1) / pageSize.value),
  );
  return {
    ...topicLink(row, lastPage),
    hash: row.lastComment ? `#comment-${row.lastComment.id}` : undefined,
  };
}

// Paging scrolls back to the table, not to the page top.
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}

// Refetch only when the parameters actually change
function createParamsKey(
  params: typeof searchParams.value,
  boardId?: string,
): string {
  return JSON.stringify({
    boardId: boardId || "",
    search: params.search || "",
    authors: params.authors?.join(",") || "",
    createdFromUtc: params.createdFromUtc || "",
    createdToUtc: params.createdToUtc || "",
    sortBy: params.sortBy || "lastActivity",
    sortOrder: params.sortOrder || "desc",
    number: params.number || 1,
    size: params.size || 20,
  });
}

const paramsKey = computed(() =>
  createParamsKey(searchParams.value, selectedBoard.value?.id),
);

function fetchTopics() {
  if (!selectedBoard.value) return;
  // ForumPage resolves the new alias after this component's immediate watcher
  // ran: wait for the alias to match rather than fetch for the stale board.
  if (selectedBoard.value.alias !== route.params.alias) return;
  store.searchTopics(searchParams.value);
}

// The one watcher: the first load, filter and board changes.
watch(paramsKey, fetchTopics, { immediate: true });
</script>

<template>
  <!-- The header and the board strip are the ForumPage shell's; the strip names the board. -->
  <div class="topics-page">
    <div v-if="selectedBoard?.description" class="board-description">
      <ContentText :html="selectedBoard.description" />
    </div>

    <!-- What a move of a pinned topic did, for a screen reader. -->
    <span v-if="canReorder" class="visually-hidden" aria-live="polite">{{
      pinnedOrder.announcement.value
    }}</span>

    <div class="filter-row">
      <TopicsFilter class="filter-row-filter" />
    </div>

    <!-- A failed load is not an empty list; shown only without stale rows. The
         store's sentence, since the read owns its refusal. -->
    <ErrorState
      v-if="topicsError && !displayTopics.length"
      :message="topicsError"
      :retry="fetchTopics"
    />

    <!-- "Board not yet resolved" counts as loading, so the empty state never flashes. -->
    <div v-else ref="tableScrollRef" class="topics-table">
      <DataTable
        ref="tableRef"
        :columns="TOPIC_COLUMNS"
        :data="displayTopics"
        :loading="topicsLoading || !selectedBoard"
        :sort="currentSort"
        :empty-text="emptyText"
        :details-for="pinRefusal?.id ?? null"
      >
        <template #details>
          <ActionResult :text="pinRefusal?.text" class="pin-refusal" />
        </template>

        <template #cell-title="{ row, index }">
          <TopicTitleCell
            :row="row"
            :index="index"
            :to="topicLink(row)"
            :search="filterState.search"
            :pins="pins"
          />
        </template>

        <template #cell-author="{ row }">
          <UserLink v-if="row.author" :user="row.author" hide-badge />
          <RemovedAccountLink v-else />
        </template>

        <template #cell-comments="{ row }">
          <router-link
            :to="topicLink(row)"
            :aria-label="`Комментарии: ${row.commentsCount}`"
            >{{ row.commentsCount }}</router-link
          ><!-- Unread suffix only for authenticated viewers with unread comments.
             Guests have no "unread" concept, so they see just the total. -->
          <template v-if="user && row.unreadCommentsCount"
            ><span class="muted"> (</span
            ><router-link :to="lastCommentLink(row)" class="unread">{{
              row.unreadCommentsCount
            }}</router-link
            ><span class="muted">)</span></template
          >
        </template>

        <template #cell-likes="{ row }">
          <span>{{ row.likesCount }}</span>
        </template>

        <template #cell-created="{ row }">
          <HumanDate :date="row.createdUtc" />
        </template>

        <!-- Who was last, then when, as the forum index and DM2 write it. -->
        <template #cell-lastActivity="{ row }">
          <template v-if="row.lastComment">
            <UserLink
              v-if="row.lastComment.author"
              :user="row.lastComment.author"
              hide-badge
            /><RemovedAccountLink v-else />,
            <router-link :to="lastCommentLink(row)" class="last-activity-link"
              ><HumanDate :date="row.lastComment.createdUtc"
            /></router-link>
          </template>
          <!-- A topic nobody answered was last active when it was opened, as the server sorts it. -->
          <template v-else>
            <UserLink
              v-if="row.author"
              :user="row.author"
              hide-badge
            /><RemovedAccountLink v-else />,
            <router-link :to="topicLink(row)" class="last-activity-link"
              ><HumanDate :date="row.createdUtc"
            /></router-link>
          </template>
        </template>

        <template v-if="topics?.paging && topics.paging.pages > 1" #footer>
          <Paging
            :paging="topics.paging"
            :to="{ name: 'forum', params: { alias: route.params.alias } }"
            :use-query="true"
            query-key="number"
            :scroll-anchor="pagingAnchor"
          />
        </template>
      </DataTable>
    </div>

    <!-- The board's moderators from the board payload; the explicit space keeps a copy unglued. -->
    <p v-if="selectedBoard?.moderators?.length" class="moderators-line">
      <span class="moderators-label">Модераторы раздела:</span>{{ " "
      }}<template
        v-for="(moderator, idx) in selectedBoard.moderators"
        :key="moderator.username"
        ><span v-if="idx > 0">, </span><UserLink :user="moderator" hide-badge
      /></template>
    </p>

    <TopicCreateForm />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.topics-page
  display: flex
  flex-direction: column
  gap: $small

// A caption, not a pinned block, as the moderators line at the foot.
.board-description
  +muted-links-line

.pin-refusal
  --action-result-margin: 0

// The dragged row stays faded in its place; a grey line shows where it would stand.
:deep(tr:has(.pin-handle.held) > td)
  opacity: $opacity-half

:deep(tr:has(.pin-handle.drop-before) > td)
  box-shadow: inset 0 2px 0 $text-muted

:deep(tr:has(.pin-handle.drop-after) > td)
  box-shadow: inset 0 -2px 0 $text-muted

// The page's $small gap is the whole table-to-caption distance.
.moderators-line
  margin-top: 0
  +muted-links-line

  .moderators-label
    font-weight: 400

  // :deep: the links come from UserLink
  :deep(a)
    +muted-link

.filter-row
  display: flex
  align-items: flex-start
  justify-content: space-between
  gap: $small
  flex-wrap: wrap

.filter-row-filter
  flex: 1
  min-width: 0
</style>
