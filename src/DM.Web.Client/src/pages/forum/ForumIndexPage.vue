<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useBoardsStore, forumApi, type Board } from "@/entities/forum";
import { useUserStore, UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";

const store = useBoardsStore();
const { boards, boardsLoading } = storeToRefs(store);
const { user } = storeToRefs(useUserStore());

const markingAllAsRead = ref(false);

// Columns for boards table
const columns: Column[] = [
  { key: "title", label: "Раздел", width: "18%", align: "left" },
  { key: "moderators", label: "Модераторы раздела", width: "28%", align: "left", hideOnMobile: true },
  { key: "topics", label: "Топики", width: "8%", align: "center" },
  { key: "comments", label: "Комментарии", width: "11%", align: "center" },
  { key: "lastActivity", label: "Последняя активность", width: "35%", align: "center", hideOnMobile: true },
];

// Map boards to include 'id' as string for DataTable requirement
const boardsData = computed(() =>
  (boards.value ?? []).map((b) => ({ ...b, id: b.id as string }))
);

// Determine last activity type: "comment" | "topic" | null
type LastActivityType = "comment" | "topic" | null;
type BoardRow = (typeof boardsData)["value"][number];

function getLastActivityType(board: BoardRow): LastActivityType {
  const hasComment = !!board.lastComment;
  const hasTopic = !!board.lastTopic;

  if (!hasComment && !hasTopic) return null;
  if (hasComment && !hasTopic) return "comment";
  if (!hasComment && hasTopic) return "topic";

  // Both exist - compare dates
  const commentDate = new Date(board.lastComment!.createdUtc).getTime();
  const topicDate = new Date(board.lastTopic!.createdUtc).getTime();
  return commentDate >= topicDate ? "comment" : "topic";
}

async function markAllAsRead() {
  if (!boards.value) return;

  markingAllAsRead.value = true;
  try {
    await forumApi.markForumAsRead();
    // Update local state (cast needed for Served<number> type)
    boards.value.forEach((board) => {
      (board as { unreadCommentsCount: number }).unreadCommentsCount = 0;
      (board as { unreadTopicsCount: number }).unreadTopicsCount = 0;
    });
  } finally {
    markingAllAsRead.value = false;
  }
}

onMounted(() => store.fetchBoards());
</script>

<template>
  <page-title>Форум</page-title>

  <div v-if="user && boards" class="forum-actions">
    <button
      class="mark-all-read-btn"
      :disabled="markingAllAsRead || !boards.some((b) => b.unreadCommentsCount)"
      @click="markAllAsRead"
    >
      {{ markingAllAsRead ? "Отмечаю..." : "Пометить все прочитанным" }}
    </button>
  </div>

  <DataTable
    :columns="columns"
    :data="boardsData"
    :loading="boardsLoading"
    empty-text="Нет доступных разделов"
  >
    <template #cell-title="{ row }">
      <Tooltip :text="row.description || undefined">
        <router-link :to="{ name: 'forum', params: { alias: row.alias } }" class="board-link">
          {{ row.title }}
        </router-link>
      </Tooltip>
    </template>

    <template #cell-moderators="{ row }">
      <template v-if="row.moderators?.length">
        <template v-for="(mod, idx) in row.moderators" :key="mod.username">
          <span v-if="idx > 0">, </span>
          <UserLink :user="mod" hide-badge />
        </template>
      </template>
      <span v-else class="muted">—</span>
    </template>

    <template #cell-topics="{ row }">
      {{ row.topicsCount || 0 }}
    </template>

    <template #cell-comments="{ row }">
      {{ row.commentsCount || 0
      }}<template v-if="row.unreadCommentsCount"
        ><span class="muted"> (</span
        ><Tooltip :text="`Непрочитанных комментариев: ${row.unreadCommentsCount}`">
          <router-link
            :to="{ name: 'forum', params: { alias: row.alias } }"
            class="unread"
          >{{ row.unreadCommentsCount }}</router-link>
        </Tooltip
        ><span class="muted">)</span></template
      >
    </template>

    <template #cell-lastActivity="{ row }">
      <!-- Last activity is a comment -->
      <template v-if="getLastActivityType(row) === 'comment'">
        <UserLink :user="row.lastComment.author" hide-badge />, <Tooltip :text='`Комментарий в "${row.lastComment.topicTitle}"`'><router-link
            :to="{ name: 'topic', params: { alias: row.alias, num: row.lastComment.topicNumber }, hash: `#comment-${row.lastComment.id}` }"
            class="last-activity-link"
          ><human-date :date="row.lastComment.createdUtc" format="DD.MM.YYYY HH:mm" /></router-link></Tooltip>
      </template>
      <!-- Last activity is a new topic -->
      <template v-else-if="getLastActivityType(row) === 'topic'">
        <UserLink :user="row.lastTopic.author" hide-badge />, <Tooltip :text='`Новый топик "${row.lastTopic.title}"`'><router-link
            :to="{ name: 'topic', params: { alias: row.alias, num: row.lastTopic.topicNumber } }"
            class="last-activity-link"
          ><human-date :date="row.lastTopic.createdUtc" format="DD.MM.YYYY HH:mm" /></router-link></Tooltip>
      </template>
      <!-- No activity -->
      <span v-else class="muted">—</span>
    </template>
  </DataTable>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Themes"
@import "@/assets/styles/Inputs"

.forum-actions
  margin-bottom: $medium
  display: flex
  justify-content: flex-end

.mark-all-read-btn
  +button

.board-link
  color: $link
  &:hover
    color: $link-hover

.muted
  color: $text-muted

.last-activity-link
  color: $link
  &:hover
    color: $link-hover

.unread
  color: $link
</style>
