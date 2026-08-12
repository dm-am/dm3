<script setup lang="ts">
/**
 * ProfileTopicsList — cross-board topics table for the profile "Топики" tab.
 *
 * Mirrors the forum's per-board TopicsList exactly where it can (same
 * DataTable, same useTopicsFilter composable, same URL-state filter
 * model, same cell idioms) and diverges only where the context demands:
 *
 *   1. Backend endpoint: `forumApi.getAllTopics` (the new cross-board
 *      `GET /v1/topics`) instead of the per-board route.
 *   2. The author filter is forced to the profile's username — the
 *      composable still tracks any extra authors in URL state, but the
 *      profile owner is always included regardless of the URL.
 *   3. The "Автор" column is hidden (always the same person) and a
 *      "Раздел" column shows the board the topic lives on — the one
 *      thing the per-board page can take for granted but the cross-board
 *      view cannot.
 *
 * Filter UX is identical to forum/newbies: search by title, date range,
 * sort (last activity / created / likes / title), pagination via URL.
 * Likes column is always rendered so the "sort by likes" option has a
 * visible target.
 */
import { computed, ref, watch } from "vue";
import type { Ref } from "vue";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { SvgIcon } from "@/shared/ui/Icon";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { forumApi, type Topic } from "@/entities/forum";
import type { ListEnvelope } from "@/shared/api/models/common";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { TopicsFilter, useTopicsFilter } from "@/features/topic-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { useAuthStore } from "@/shared/stores/auth";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const props = defineProps<{
  /** Profile owner whose topics we list. */
  username: string;
}>();

// Reuse the forum's filter composable verbatim. URL is the single source
// of truth, so a Topics-tab user gets the same bookmarkable, back-button-
// safe behaviour the forum page has.
const { filterState, searchParams, hasActiveFilters } = useTopicsFilter();

// Server-side filter always forces the profile owner as an author. The
// composable's URL-tracked `authors[]` is merged in so a visitor can
// also intersect with other co-authors if they want (rare, but free).
const apiQuery = computed(() => {
  const merged = new Set<string>(searchParams.value.authors ?? []);
  merged.add(props.username);
  return { ...searchParams.value, authors: [...merged] };
});

// We need full paging metadata for the Paging widget, so we keep the
// raw ListEnvelope here (useApiList strips .resources, which would
// throw away paging). Same cache-y idiom the rest of the app uses,
// just inlined for one screen.
const envelope: Ref<ListEnvelope<Topic> | null> = ref(null);

// Keeps any already-shown topics on a failure (stale-while-revalidate); the
// template surfaces the error only when there's nothing to show.
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

// Re-fetch whenever the (stringified) params change. Stringify dedupes
// adjacent identical states — switching tabs and back inside the same
// URL doesn't refire the request.
const paramsKey = computed(() => JSON.stringify(apiQuery.value));
watch(paramsKey, () => fetchTopics(), { immediate: true });

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Топиков по заданным фильтрам не найдено"
    : "Пользователь не создал ни одного топика",
);

const columns: Column[] = [
  { key: "title", label: "Топик", width: "40%", align: "left" },
  {
    key: "board",
    label: "Раздел",
    width: "14%",
    align: "left",
    hideOnMobile: true,
  },
  { key: "comments", label: "Комментарии", width: "10%", align: "center" },
  { key: "likes", label: "Лайки", width: "7%", align: "center" },
  {
    key: "created",
    label: "Дата создания",
    width: "13%",
    align: "center",
    hideOnMobile: true,
  },
  {
    key: "lastActivity",
    label: "Последняя активность",
    width: "16%",
    align: "center",
    hideOnMobile: true,
  },
];

function topicLink(row: Topic) {
  return `/forum/${row.board.alias}/${row.topicNumber}`;
}

// Paging scrolls the topics table back into view (not the page top)
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
    <!-- Same filter UI as forum/newbies; author scope is enforced
         server-side, so the author chip is suppressed. -->
    <TopicsFilter :hide-author="true" />

    <ErrorState v-if="loadError" :message="loadError" :retry="fetchTopics" />

    <DataTable
      v-if="!loadError || topics.length > 0"
      ref="tableRef"
      :columns="columns"
      :data="topics"
      :loading="loading"
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

      <template #cell-lastActivity="{ row }">
        <template v-if="row.lastComment">
          <Tooltip
            :text="row.lastComment.author?.username ?? 'удаленный пользователь'"
          >
            <router-link
              :to="`${topicLink(row)}#comment-${row.lastComment.id}`"
              class="last-activity-link"
            >
              <HumanDate :date="row.lastComment.createdUtc" />
            </router-link>
          </Tooltip>
        </template>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
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
  color: $link
  display: inline-flex
  align-items: center
  gap: 4px

  &:hover
    color: $link-hover

  &.closed
    opacity: 0.7

.topic-icon
  flex-shrink: 0

.board-link
  color: $link

  &:hover
    color: $link-hover

.muted
  color: $text-muted

.unread
  color: $link

.last-activity-link
  color: $link

  &:hover
    color: $link-hover
</style>
