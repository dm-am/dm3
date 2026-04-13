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
import BoardNavigation from "./BoardNavigation.vue";
import PinnedTopicsManager from "./PinnedTopicsManager.vue";
import { useAuthStore } from "@/shared/stores";
import { UserRole } from "@/shared/api/models/common";

const route = useRoute();
const store = useBoardsStore();
const { topics, attachedTopics, topicsLoading, selectedBoard, moderators } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// Filter composable
const { searchParams, hasActiveFilters } = useTopicsFilter();

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value ? "Топиков по заданным фильтрам не найдено" : "Топиков пока нет"
);

// Moderator actions state
const pinningTopicId = ref<string | null>(null);
const showPinnedManager = ref(false);
const savingPinnedOrder = ref(false);

// Check if current user can moderate this board
const canModerate = computed(() => {
  if (!user.value) return false;

  // Global moderators/admins
  const isGlobalModerator = user.value.roles?.some((r: UserRole) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r),
  ) ?? false;
  if (isGlobalModerator) return true;

  // Board-specific moderators
  const boardModerators = selectedBoard.value?.moderators ?? [];
  return boardModerators.some(m => m.username === user.value?.username);
});

// Columns for topics table (with conditional actions column)
const columns = computed<Column[]>(() => {
  const base: Column[] = [
    { key: "title", label: "Топик", width: canModerate.value ? "35%" : "45%", align: "left" },
    { key: "author", label: "Автор", width: "17%", align: "left" },
    { key: "comments", label: "Комментарии", width: "9%", align: "center" },
    { key: "created", label: "Дата создания", width: "13%", align: "center", hideOnMobile: true },
    { key: "lastActivity", label: "Последняя активность", width: "16%", align: "center", hideOnMobile: true },
  ];

  if (canModerate.value) {
    base.push({ key: "actions", label: "", width: "10%", align: "center" });
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

    <!-- Moderators section -->
    <div v-if="moderators?.length" class="moderators-section">
      <span class="moderators-label">Модераторы раздела:</span>
      <template v-for="(user, idx) in moderators" :key="user.username">
        <span v-if="idx > 0">, </span>
        <UserLink :user="user" />
      </template>
    </div>

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

    <!-- Topics table -->
    <DataTable
      :columns="columns"
      :data="displayTopics"
      :loading="topicsLoading"
      :empty-text="emptyText"
    >
      <template #cell-title="{ row }">
        <Tooltip v-if="row.description" :text="row.description">
          <router-link
            :to="topicLink(row)"
            :class="['topic-link', { pinned: row.isPinned, closed: row.isClosed }]"
          >
            <SvgIcon v-if="row.isPinned" name="pin" class="topic-icon" />
            <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
            {{ row.title }}
          </router-link>
        </Tooltip>
        <router-link
          v-else
          :to="topicLink(row)"
          :class="['topic-link', { pinned: row.isPinned, closed: row.isClosed }]"
        >
          <SvgIcon v-if="row.isPinned" name="pin" class="topic-icon" />
          <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
          {{ row.title }}
        </router-link>
      </template>

      <template #cell-author="{ row }">
        <UserLink :user="row.author!" />
      </template>

      <template #cell-comments="{ row }">
        <Tooltip :text="`Всего комментариев: ${row.commentsCount}`"
          ><router-link :to="topicLink(row)">{{
            row.commentsCount
          }}</router-link></Tooltip
        ><span class="muted"> (</span
        ><template v-if="row.unreadCommentsCount"
          ><Tooltip :text="`Непрочитанных комментариев: ${row.unreadCommentsCount}`"
            ><router-link :to="`${topicLink(row)}#comment-${row.lastComment?.id}`" class="unread">{{
              row.unreadCommentsCount
            }}</router-link></Tooltip
          ></template
        ><template v-else
          ><span class="muted">0</span></template
        ><span class="muted">)</span>
      </template>

      <template #cell-created="{ row }">
        <HumanDate :date="row.createdUtc" format="DD.MM.YYYY [в] HH:mm" />
      </template>

      <template #cell-lastActivity="{ row }">
        <template v-if="row.lastComment">
          <Tooltip :text="row.lastComment.author.username">
            <router-link
              :to="`${topicLink(row)}#comment-${row.lastComment.id}`"
              class="last-activity-link"
            >
              <HumanDate :date="row.lastComment.createdUtc" format="DD.MM.YYYY [в] HH:mm" />
            </router-link>
          </Tooltip>
        </template>
        <span v-else class="muted">—</span>
      </template>

      <template v-if="canModerate" #cell-actions="{ row }">
        <Tooltip :text="row.isPinned ? 'Открепить топик' : 'Закрепить топик'">
          <button
            class="pin-button"
            :class="{ pinned: row.isPinned, loading: pinningTopicId === row.id }"
            :disabled="pinningTopicId !== null"
            @click="handleTogglePin(row)"
          >
            <SvgIcon :name="'pin'" />
          </button>
        </Tooltip>
      </template>

      <template v-if="topics?.paging && topics.paging.pages > 1 && !hasActiveFilters" #footer>
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

.topics-page
  display: flex
  flex-direction: column
  gap: $small

.moderators-section
  margin-bottom: $small
  font-size: 14px

.moderators-label
  color: $text-muted
  margin-right: $small

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

.last-activity-link
  color: $link
  &:hover
    color: $link-hover

.pin-button
  background: none
  border: 1px solid $border
  border-radius: 4px
  padding: 4px 8px
  cursor: pointer
  color: $text-muted
  transition: opacity 0.2s ease

  &:hover:not(:disabled)
    color: $link
    border-color: $link

  &.pinned
    color: $link
    border-color: $link

  &.loading
    opacity: 0.5
    cursor: wait

  &:disabled
    cursor: not-allowed
    opacity: 0.5

.moderator-actions
  display: flex
  gap: $small
  margin-bottom: $medium

.manage-pinned-button
  display: inline-flex
  align-items: center
  gap: 6px
  padding: $small $medium
  background: transparent
  border: 1px solid $link
  border-radius: 4px
  color: $link
  font-size: 14px
  cursor: pointer

  &:hover
    background: $link
    color: white
</style>
