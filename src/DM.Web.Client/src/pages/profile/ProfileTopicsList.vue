<script setup lang="ts">
/**
 * The topics of the profile's "Топики" tab across every board: the forum's
 * table and filter, the owner always among the authors, a "Раздел" column in
 * place of "Автор", and "Нравится" always drawn for its sort.
 */
import { computed, ref, watch } from "vue";
import type { Ref } from "vue";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { SvgIcon } from "@/shared/ui/Icon";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { forumApi, type Topic } from "@/entities/forum";
import { RemovedAccountLink, UserLink, useAuthStore } from "@/entities/user";
import type { ListEnvelope } from "@/shared/api/models/common";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { TopicsFilter, useTopicsFilter } from "@/features/topic-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{
  /** Profile owner whose topics we list. */
  username: string;
}>();

// The forum's filter, the address its only source.
const { filterState, searchParams, hasActiveFilters } = useTopicsFilter();

// The owner is always an author; other authors from the address narrow it further.
const apiQuery = computed(() => {
  const merged = new Set<string>(searchParams.value.authors ?? []);
  merged.add(props.username);
  return { ...searchParams.value, authors: [...merged] };
});

// The whole envelope: Paging needs its paging, which useApiList drops.
const envelope: Ref<ListEnvelope<Topic> | null> = ref(null);

// A failure keeps the topics shown; the error stands only over an empty list.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить топики" });

const authStore = useAuthStore();

function fetchTopics() {
  return run(
    () => forumApi.getAllTopics(apiQuery.value),
    (data) => {
      envelope.value = data;
    },
  );
}

const topics = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);

// Refetch on every change of the query; the string dedupes identical ones.
const paramsKey = computed(() => JSON.stringify(apiQuery.value));
watch(paramsKey, () => fetchTopics(), { immediate: true });

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Топиков по заданным фильтрам не найдено"
    : "Пользователь не создал ни одного топика",
);

// "Топик" takes what the others leave; their shares include the cell padding.
const columns: Column[] = [
  { key: "title", label: "Топик", align: "left" },
  {
    key: "board",
    label: "Раздел",
    width: "14.3%",
    align: "left",
  },
  { key: "comments", label: "Комментарии", width: "10.7%", align: "center" },
  { key: "likes", label: "Нравится", width: "8%", align: "center" },
  {
    key: "created",
    label: "Дата создания",
    width: "13.4%",
    align: "center",
  },
  {
    key: "lastActivity",
    label: "Последняя активность",
    width: "16.1%",
    align: "center",
  },
];

// The header announces the sort, as on a board; every sort is a column key.
const currentSort = computed<SortState>(() => ({
  key: filterState.value.sortBy,
  direction: filterState.value.sortOrder,
}));

function topicLink(row: Topic) {
  return `/forum/${row.board.alias}/${row.topicNumber}`;
}

// Paging scrolls the table back into view, not the page top.
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}

function boardLink(row: Topic) {
  return { name: "forum" as const, params: { alias: row.board.alias } };
}
</script>

<template>
  <div class="user-topics-list">
    <!-- The forum's filter; the author scope is the server's, so no author chip. -->
    <TopicsFilter :hide-author="true" />

    <ErrorState v-if="loadError" :message="loadError" :retry="fetchTopics" />

    <DataTable
      v-if="!loadError || topics.length > 0"
      ref="tableRef"
      :columns="columns"
      :data="topics"
      :loading="loading"
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <template #cell-title="{ row }">
        <router-link
          :to="topicLink(row)"
          :class="['topic-link', { closed: row.isClosed }]"
        >
          <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
          <span
            v-if="filterState.search"
            v-html="highlightMatch(row.title, filterState.search)"
          />
          <template v-else>{{ row.title }}</template>
        </router-link>
      </template>

      <template #cell-board="{ row }">
        <router-link :to="boardLink(row)" class="board-link">{{
          row.board.title
        }}</router-link>
      </template>

      <template #cell-comments="{ row }">
        <router-link :to="topicLink(row)">{{ row.commentsCount }}</router-link
        ><!-- Unread breakdown only makes sense for an authenticated viewer
             — a guest has no read/unread state, so "(0)" would be a
             meaningless suffix on every row. -->
        <template v-if="authStore.isAuthenticated"
          ><span class="muted"> (</span
          ><template v-if="row.unreadCommentsCount"
            ><router-link
              :to="`${topicLink(row)}#comment-${row.lastComment?.id}`"
              class="unread"
              >{{ row.unreadCommentsCount }}</router-link
            ></template
          ><template v-else><span class="muted">0</span></template
          ><span class="muted">)</span></template
        >
      </template>

      <template #cell-likes="{ row }">
        <span>{{ row.likesCount }}</span>
      </template>

      <template #cell-created="{ row }">
        <HumanDate :date="row.createdUtc" />
      </template>

      <!-- Who was last, then when, as the forum's table of topics. -->
      <template #cell-lastActivity="{ row }">
        <template v-if="row.lastComment">
          <UserLink
            v-if="row.lastComment.author"
            :user="row.lastComment.author"
            hide-badge
          /><RemovedAccountLink v-else />,
          <router-link
            :to="`${topicLink(row)}#comment-${row.lastComment.id}`"
            class="last-activity-link"
            ><HumanDate :date="row.lastComment.createdUtc"
          /></router-link>
        </template>
        <!-- Nobody answered: active since it was opened, as the forum shows it. -->
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

      <template v-if="paging && paging.pages && paging.pages > 1" #footer>
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
.user-topics-list
  display: flex
  flex-direction: column
  gap: $small

.topic-link
  display: inline-flex
  align-items: center
  gap: $minor

  &.closed
    opacity: $closed-opacity

.topic-icon
  flex-shrink: 0

.unread
  color: $link
</style>
