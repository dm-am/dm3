<script setup lang="ts">
import { computed, watch, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { useBoardsStore, type Topic } from "@/entities/forum";
import { UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { TopicsFilter, useTopicsFilter } from "@/features/topic-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import BoardNavigation from "./BoardNavigation.vue";
import PinnedTopicsManager from "./PinnedTopicsManager.vue";
import { useAuthStore } from "@/shared/stores";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { UserRole } from "@/shared/api/models/common";

const route = useRoute();
const store = useBoardsStore();
const { topics, attachedTopics, topicsLoading, topicsError, selectedBoard } =
  storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// Section leaf owns the document title (the board name). TopicPage owns it on
// the topic subroute — the two are mutually exclusive router views, so there
// is no parent/child title conflict.
useDocumentTitle(() => selectedBoard.value?.title);

// Filter composable
const { filterState, searchParams, hasActiveFilters } = useTopicsFilter();

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Топиков по заданным фильтрам не найдено"
    : "Топиков пока нет",
);

// Moderator actions state
const pinningTopicId = ref<string | null>(null);
const showPinnedManager = ref(false);
const savingPinnedOrder = ref(false);

// Check if current user can moderate this board
const canModerate = computed(() => {
  if (!user.value) return false;

  // Global moderators/admins
  const isGlobalModerator =
    user.value.roles?.some((r: UserRole) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(
        r,
      ),
    ) ?? false;
  if (isGlobalModerator) return true;

  // Board-specific moderators
  const boardModerators = selectedBoard.value?.moderators ?? [];
  return boardModerators.some((m) => m.username === user.value?.username);
});

// Columns for topics table. Likes column is always rendered so the
// "sort by likes" filter has a visible target — the column doubles as
// confirmation of the active sort. Moderator actions column tacks onto
// the end only when the viewer can moderate this board.
const columns = computed<Column[]>(() => {
  // Alignment principle: identifier/text and date columns left, short numeric
  // columns center. Widths sum to ~100%; "Дата создания" gets enough room for
  // the "DD.MM.YYYY в HH:mm" format.
  const base: Column[] = [
    {
      key: "title",
      label: "Топик",
      width: canModerate.value ? "30%" : "38%",
      align: "left",
    },
    { key: "author", label: "Автор", width: "16%", align: "left" },
    { key: "comments", label: "Комментарии", width: "10%", align: "center" },
    { key: "likes", label: "Лайки", width: "8%", align: "center" },
    {
      key: "created",
      label: "Дата создания",
      width: "14%",
      align: "left",
      hideOnMobile: true,
    },
    {
      key: "lastActivity",
      label: "Последняя активность",
      width: "14%",
      align: "left",
      hideOnMobile: true,
    },
  ];

  if (canModerate.value) {
    base.push({ key: "actions", label: "", width: "8%", align: "center" });
  }

  return base;
});

// Display row type (Topic with isPinned flag)
type DisplayTopic = Topic & { isPinned: boolean };

// Combine attached + regular topics when no filters are active
const displayTopics = computed<DisplayTopic[]>(() => {
  if (hasActiveFilters.value) {
    // With filters: show only filtered results (attached mixed in)
    return (topics.value?.resources ?? []).map((t) => ({
      ...t,
      isPinned: t.isAttached,
    }));
  }

  // No filters: pinned first (from attachedTopics), then regular
  const pinned: DisplayTopic[] = (attachedTopics.value ?? []).map((t) => ({
    ...t,
    isPinned: true,
  }));
  const regular: DisplayTopic[] = (topics.value?.resources ?? []).map((t) => ({
    ...t,
    isPinned: false,
  }));
  return [...pinned, ...regular];
});

// Helper to generate topic link
function topicLink(row: DisplayTopic, page?: number) {
  const alias = selectedBoard.value?.alias || route.params.alias;
  const path = `/forum/${alias}/${row.topicNumber}`;
  return page && page > 1 ? `${path}?page=${page}` : path;
}

// Stable key for deduplication - only refetch when params actually change
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

// SINGLE watcher on paramsKey - handles initial load, filter changes, and board changes
// immediate: true ensures it fires on mount if board is already loaded
// Guard ensures no-op if board not yet loaded (will fire again when board loads)
watch(
  paramsKey,
  () => {
    if (!selectedBoard.value) return;
    store.searchTopics(searchParams.value);
  },
  { immediate: true },
);

// Toggle pin/unpin topic (moderator action)
async function handleTogglePin(row: DisplayTopic) {
  if (pinningTopicId.value) return;

  const topicId = String(row.id);
  pinningTopicId.value = topicId;
  try {
    await store.togglePinTopic(topicId);
  } finally {
    pinningTopicId.value = null;
  }
}

// Save pinned topics order (moderator action)
async function handleSavePinnedOrder(topicIds: string[]) {
  savingPinnedOrder.value = true;
  try {
    await store.reorderPinnedTopics(topicIds);
    showPinnedManager.value = false;
  } finally {
    savingPinnedOrder.value = false;
  }
}
</script>

<template>
  <div class="topics-page">
    <!-- Board Navigation -->
    <BoardNavigation />

    <!-- Moderators caption (muted, like other secondary lines). Rendered from
         the board payload — the board already carries its moderators, so no
         separate request is needed. Explicit space after the colon so a copied
         selection reads "Модераторы раздела: Name", not glued. -->
    <LeadText v-if="selectedBoard?.moderators?.length" class="moderators-line">
      Модераторы раздела:{{ " "
      }}<template
        v-for="(moderator, idx) in selectedBoard.moderators"
        :key="moderator.username"
        ><span v-if="idx > 0">, </span><UserLink :user="moderator"
      /></template>
    </LeadText>

    <!-- Moderator actions -->
    <div v-if="canModerate && attachedTopics?.length" class="moderator-actions">
      <Tooltip text="Изменить порядок закрепленных топиков">
        <button class="manage-pinned-button" @click="showPinnedManager = true">
          <SvgIcon name="pin" />
          Управление закрепленными ({{ attachedTopics.length }})
        </button>
      </Tooltip>
    </div>

    <!-- Filter controls -->
    <TopicsFilter />

    <!-- Error state: a failed load must not be presented as an empty list.
         Shown only when there are no stale rows to keep on screen
         (stale-while-revalidate keeps already-loaded topics otherwise). -->
    <div v-if="topicsError && !displayTopics.length" class="error-message">
      Не удалось загрузить топики. Попробуйте обновить страницу.
    </div>

    <!-- Topics table. Treat "board not yet resolved" as loading so the empty
         state never flashes before topics can be fetched. -->
    <DataTable
      v-else
      :columns="columns"
      :data="displayTopics"
      :loading="topicsLoading || !selectedBoard"
      :empty-text="emptyText"
    >
      <template #cell-title="{ row }">
        <Tooltip v-if="row.description" :text="row.description">
          <router-link
            :to="topicLink(row)"
            :class="[
              'topic-link',
              { pinned: row.isPinned, closed: row.isClosed },
            ]"
          >
            <SvgIcon v-if="row.isPinned" name="pin" class="topic-icon" />
            <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
            <span
              v-if="filterState.search"
              v-html="highlightMatch(row.title, filterState.search)"
            />
            <template v-else>{{ row.title }}</template>
          </router-link>
        </Tooltip>
        <router-link
          v-else
          :to="topicLink(row)"
          :class="[
            'topic-link',
            { pinned: row.isPinned, closed: row.isClosed },
          ]"
        >
          <SvgIcon v-if="row.isPinned" name="pin" class="topic-icon" />
          <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
          <span
            v-if="filterState.search"
            v-html="highlightMatch(row.title, filterState.search)"
          />
          <template v-else>{{ row.title }}</template>
        </router-link>
      </template>

      <template #cell-author="{ row }">
        <UserLink
          v-if="row.author"
          :user="row.author"
          :search-query="filterState.search"
        />
        <span v-else class="muted">удаленный пользователь</span>
      </template>

      <template #cell-comments="{ row }">
        <Tooltip :text="`Всего комментариев: ${row.commentsCount}`"
          ><router-link :to="topicLink(row)">{{
            row.commentsCount
          }}</router-link></Tooltip
        ><!-- Unread suffix only for authenticated viewers with unread comments.
             Guests have no "unread" concept, so they see just the total. -->
        <template v-if="user && row.unreadCommentsCount"
          ><span class="muted"> (</span
          ><Tooltip
            :text="`Непрочитанных комментариев: ${row.unreadCommentsCount}`"
            ><router-link
              :to="`${topicLink(row)}#comment-${row.lastComment?.id}`"
              class="unread"
              >{{ row.unreadCommentsCount }}</router-link
            ></Tooltip
          ><span class="muted">)</span></template
        >
      </template>

      <template #cell-likes="{ row }">
        <span>{{ row.likesCount }}</span>
      </template>

      <template #cell-created="{ row }">
        <HumanDate :date="row.createdUtc" format="DD.MM.YYYY [в] HH:mm" />
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
              <HumanDate
                :date="row.lastComment.createdUtc"
                format="DD.MM.YYYY [в] HH:mm"
              />
            </router-link>
          </Tooltip>
        </template>
        <span v-else class="muted">—</span>
      </template>

      <template v-if="canModerate" #cell-actions="{ row }">
        <Tooltip :text="row.isPinned ? 'Открепить топик' : 'Закрепить топик'">
          <button
            class="pin-button"
            :class="{
              pinned: row.isPinned,
              loading: pinningTopicId === row.id,
            }"
            :disabled="pinningTopicId !== null"
            @click="handleTogglePin(row)"
          >
            <SvgIcon :name="'pin'" />
          </button>
        </Tooltip>
      </template>

      <template v-if="topics?.paging && topics.paging.pages > 1" #footer>
        <Paging
          :paging="topics.paging"
          :to="{ name: 'forum', params: { alias: route.params.alias } }"
          :use-query="true"
          query-key="number"
        />
      </template>
    </DataTable>

    <!-- Pinned topics manager modal -->
    <PinnedTopicsManager
      v-if="showPinnedManager && attachedTopics"
      :topics="attachedTopics"
      :saving="savingPinnedOrder"
      @save="handleSavePinnedOrder"
      @close="showPinnedManager = false"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Variables"
@import "@/assets/styles/Themes"
@import "@/assets/styles/Inputs"

.topics-page
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
  &.pinned
    font-weight: bold
  &.closed
    opacity: 0.7
    &.pinned
      opacity: 1

.topic-icon
  flex-shrink: 0

.muted
  color: $text-muted

.unread
  color: $link
  &:hover
    color: $link-hover

.last-activity-link
  color: $link
  &:hover
    color: $link-hover

.pin-button
  padding: $minor $small
  +button

  &.pinned
    color: $link
    border-color: $link

  &.loading
    opacity: 0.5
    cursor: wait

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
  margin-bottom: $medium

.moderator-actions
  display: flex
  gap: $small
  margin-bottom: $medium

.manage-pinned-button
  display: inline-flex
  align-items: center
  gap: $small
  +button
</style>
