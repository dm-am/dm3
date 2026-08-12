<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useBoardsStore, forumApi } from "@/entities/forum";
import { useAuthStore, UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const store = useBoardsStore();
const { boards, boardsLoading, boardsError } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const markingAllAsRead = ref(false);
const markAllError = ref(false);

// Columns for boards table. The table uses layout "auto" so the nowrap
// "Последняя активность" cells (longest username + "DD.MM.YYYY в HH:mm")
// take exactly the room they need on one line at any viewport, and the
// width-less "Описание" absorbs the remaining space — descriptions render
// on a single line wherever the viewport allows.
const columns: Column[] = [
  { key: "title", label: "Раздел", width: "18%", align: "left" },
  {
    key: "description",
    label: "Описание",
    align: "left",
    hideOnMobile: true,
  },
  { key: "topics", label: "Топики", width: "8%", align: "center" },
  { key: "comments", label: "Комментарии", width: "11%", align: "center" },
  {
    key: "lastActivity",
    label: "Последняя активность",
    align: "center",
    hideOnMobile: true,
  },
];

// Map boards to include 'id' as string for DataTable requirement
const boardsData = computed(() =>
  (boards.value ?? []).map((b) => ({ ...b, id: b.id as string })),
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

/**
 * Link to the last-comment topic, targeting the page containing that
 * comment. The board payload only carries board-wide aggregate counts
 * (Board.CommentsCount), not the per-topic comment count needed to compute
 * ceil(topicCommentsCount / size) — that field does not exist on
 * BoardLastComment (see DM.Web.Api/Features/Forum/Boards/Board.cs). Using
 * the board-wide total here would produce a page number worse than "none"
 * for boards with many topics, so this stays a plain topic+hash link
 * (matches the pre-existing behavior) until the backend exposes the
 * target topic's own CommentsCount on BoardLastComment.
 */
function lastCommentLink(board: BoardRow) {
  const lastComment = board.lastComment;
  if (!lastComment) return null;
  return {
    name: "topic",
    params: { alias: board.alias, num: lastComment.topicNumber },
    hash: `#comment-${lastComment.id}`,
  };
}

/**
 * Link to the last-created topic at the page it appears on within the
 * board's topic list (pinned topics aside), so a fresh topic still resolves
 * to the right page instead of always assuming page 1.
 */
function lastTopicLink(board: BoardRow) {
  const lastTopic = board.lastTopic;
  if (!lastTopic) return null;
  return {
    name: "topic",
    params: { alias: board.alias, num: lastTopic.topicNumber },
  };
}

async function markAllAsRead() {
  if (!boards.value) return;

  markingAllAsRead.value = true;
  markAllError.value = false;
  try {
    const { error } = await forumApi.markForumAsRead();
    if (error) {
      markAllError.value = true;
      return;
    }
    // Update local state (cast needed for Served<number> type)
    boards.value.forEach((board) => {
      (board as { unreadCommentsCount: number }).unreadCommentsCount = 0;
      (board as { unreadTopicsCount: number }).unreadTopicsCount = 0;
    });
  } finally {
    markingAllAsRead.value = false;
  }
}

function retryFetchBoards() {
  return store.fetchBoards(true);
}

onMounted(() => store.fetchBoards());
</script>

<template>
  <!-- The h1 "Форум" comes from the persistent ForumPage shell. The boards
       table below is the index's own navigation, so this page goes straight
       to the table — no board strip (it would duplicate the table, and the
       shell hides it on the index) and no lead text. -->
  <div v-if="user && boards" class="forum-actions">
    <Button
      :loading="markingAllAsRead"
      :disabled="!boards.some((b) => b.unreadCommentsCount)"
      @click="markAllAsRead"
    >
      Отметить все как прочитанное
    </Button>
  </div>
  <SecondaryText v-if="markAllError" class="mark-all-error" role="alert">
    Не удалось отметить топики прочитанными
  </SecondaryText>

  <!-- Error state: a failed load must not be presented as an empty list. -->
  <ErrorState
    v-if="boardsError && !boardsData.length"
    message="Не удалось загрузить разделы"
    :retry="retryFetchBoards"
  />

  <DataTable
    v-else
    :columns="columns"
    :data="boardsData"
    :loading="boardsLoading"
    empty-text="Разделов пока нет"
    table-layout="auto"
  >
    <template #cell-title="{ row }">
      <router-link
        :to="{ name: 'forum', params: { alias: row.alias } }"
        class="board-link"
      >
        {{ row.title }}
      </router-link>
    </template>

    <template #cell-description="{ row }">
      {{ row.description || VALUE_UNAVAILABLE }}
    </template>

    <template #cell-topics="{ row }">
      {{ row.topicsCount || 0 }}
    </template>

    <template #cell-comments="{ row }">
      {{ row.commentsCount || 0
      }}<!-- Unread suffix only for authenticated viewers with unread comments.
           Guests have no "unread" concept, so they see just the total. -->
      <template v-if="user && row.unreadCommentsCount"
        ><span class="muted" aria-hidden="true"> (</span
        ><router-link
          :to="{ name: 'forum', params: { alias: row.alias } }"
          class="unread"
          :aria-label="`Непрочитанные комментарии: ${row.unreadCommentsCount}`"
          >{{ row.unreadCommentsCount }}</router-link
        ><span class="muted" aria-hidden="true">)</span></template
      >
    </template>

    <template #cell-lastActivity="{ row }">
      <!-- Last activity is a comment -->
      <template v-if="getLastActivityType(row) === 'comment'">
        <UserLink
          v-if="row.lastComment.author"
          :user="row.lastComment.author"
          hide-badge
        /><span v-else class="muted">удаленный пользователь</span>,
        <Tooltip
          :text="`Комментарий в &quot;${row.lastComment.topicTitle}&quot;`"
          ><router-link :to="lastCommentLink(row)!" class="last-activity-link"
            ><human-date :date="row.lastComment.createdUtc" /></router-link
        ></Tooltip>
      </template>
      <!-- Last activity is a new topic -->
      <template v-else-if="getLastActivityType(row) === 'topic'">
        <UserLink
          v-if="row.lastTopic.author"
          :user="row.lastTopic.author"
          hide-badge
        /><span v-else class="muted">удаленный пользователь</span>,
        <Tooltip :text="`Новый топик &quot;${row.lastTopic.title}&quot;`"
          ><router-link :to="lastTopicLink(row)!" class="last-activity-link"
            ><human-date :date="row.lastTopic.createdUtc" /></router-link
        ></Tooltip>
      </template>
      <!-- No activity -->
      <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
    </template>
  </DataTable>
</template>

<style scoped lang="sass">
.forum-actions
  margin-bottom: $medium
  display: flex
  justify-content: flex-end

.mark-all-error
  display: block
  text-align: right
  margin-bottom: $medium
  color: $accent-red

// The name + date pair never wraps — with the auto table layout the
// column takes exactly the width this content needs
:deep(.col-lastActivity)
  white-space: nowrap
</style>
