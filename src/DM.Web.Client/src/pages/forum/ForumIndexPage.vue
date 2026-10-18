<script setup lang="ts">
import { computed, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useBoardsStore, forumApi } from "@/entities/forum";
import { RemovedAccountLink, useAuthStore, UserLink } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { UnreadLine } from "@/shared/ui/UnreadLine";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import type { GeneralError } from "@/shared/api/models/common";

const store = useBoardsStore();
const { boards, boardsLoading, boardsError } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// Columns for boards table. The table lays out "auto" (DataTable), so the
// "Последняя активность" cells (longest username + "DD.MM.YYYY в HH:mm")
// take the room they need on one line where the column has it (1920), and
// the width-less "Описание" absorbs the remaining space — descriptions render
// on a single line wherever the viewport allows. In a narrower column the
// name and the date take a line each.
const columns: Column[] = [
  { key: "title", label: "Раздел", width: "18%", align: "left" },
  {
    key: "description",
    label: "Описание",
    align: "left",
  },
  { key: "topics", label: "Топики", width: "8%", align: "center" },
  { key: "comments", label: "Комментарии", width: "11%", align: "center" },
  {
    key: "lastActivity",
    label: "Последняя активность",
    align: "center",
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

/**
 * The unread comments of the whole forum, for the line under the heading. A
 * guest has none: the server answers a guest with the totals in these fields,
 * since there is nothing a guest could have read.
 */
const unreadComments = computed(() =>
  user.value
    ? (boards.value ?? []).reduce(
        (sum, board) => sum + (board.unreadCommentsCount || 0),
        0,
      )
    : 0,
);

/**
 * "Отметить прочитанными" of the line: the whole forum marked read, and the
 * refusal handed back to the line to say, or null once done.
 */
async function markAllAsRead(): Promise<GeneralError | null> {
  const { error } = await forumApi.markForumAsRead();
  if (error) return error;
  // Update local state (cast needed for Served<number> type). The line goes
  // with the count, and the sidebar's counters read the same boards.
  for (const board of boards.value ?? []) {
    (board as { unreadCommentsCount: number }).unreadCommentsCount = 0;
    (board as { unreadTopicsCount: number }).unreadTopicsCount = 0;
  }
  return null;
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
       shell hides it on the index) and no lead text. The line of the unread
       stands under the heading only while there is something to mark: the
       button it replaces stood shaded over a forum read to the end. -->
  <UnreadLine
    :count="unreadComments"
    :mark="markAllAsRead"
    failure="Не удалось отметить топики прочитанными"
    done="Все топики прочитаны"
  />

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
      <template
        v-if="getLastActivityType(row) === 'comment' && row.lastComment"
      >
        <UserLink
          v-if="row.lastComment.author"
          :user="row.lastComment.author"
          hide-badge
        /><RemovedAccountLink v-else />,
        <Tooltip
          :text="`Комментарий в &quot;${row.lastComment.topicTitle}&quot;`"
          ><router-link :to="lastCommentLink(row)!" class="last-activity-link"
            ><human-date :date="row.lastComment.createdUtc" /></router-link
        ></Tooltip>
      </template>
      <!-- Last activity is a new topic -->
      <template
        v-else-if="getLastActivityType(row) === 'topic' && row.lastTopic"
      >
        <UserLink
          v-if="row.lastTopic.author"
          :user="row.lastTopic.author"
          hide-badge
        /><RemovedAccountLink v-else />,
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
