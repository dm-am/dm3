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

// The table lays out "auto", so the last activity takes one line where the
// column has room, and the width-less description absorbs the rest.
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

// DataTable wants a string id
const boardsData = computed(() =>
  (boards.value ?? []).map((b) => ({ ...b, id: b.id as string })),
);

type LastActivityType = "comment" | "topic" | null;
type BoardRow = (typeof boardsData)["value"][number];

function getLastActivityType(board: BoardRow): LastActivityType {
  const hasComment = !!board.lastComment;
  const hasTopic = !!board.lastTopic;

  if (!hasComment && !hasTopic) return null;
  if (hasComment && !hasTopic) return "comment";
  if (!hasComment && hasTopic) return "topic";

  const commentDate = new Date(board.lastComment!.createdUtc).getTime();
  const topicDate = new Date(board.lastTopic!.createdUtc).getTime();
  return commentDate >= topicDate ? "comment" : "topic";
}

/**
 * The topic of the last comment, with its hash and no page: the board payload
 * carries no comment count of that topic, and the board-wide total would give
 * a page worse than none.
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

/** The last topic at the page of the board's list it stands on. */
function lastTopicLink(board: BoardRow) {
  const lastTopic = board.lastTopic;
  if (!lastTopic) return null;
  return {
    name: "topic",
    params: { alias: board.alias, num: lastTopic.topicNumber },
  };
}

/** The forum's unread comments; a guest is answered with the totals, so has none. */
const unreadComments = computed(() =>
  user.value
    ? (boards.value ?? []).reduce(
        (sum, board) => sum + (board.unreadCommentsCount || 0),
        0,
      )
    : 0,
);

/** Marks the whole forum read; returns the refusal or null. */
async function markAllAsRead(): Promise<GeneralError | null> {
  const { error } = await forumApi.markForumAsRead();
  if (error) return error;
  // Served<number> needs the cast; the sidebar's counters read the same boards.
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
  <!-- The h1 comes from the ForumPage shell; the boards table is this page's
       navigation, so no board strip. The unread line stands while there is
       something to mark. -->
  <UnreadLine
    :count="unreadComments"
    :mark="markAllAsRead"
    failure="Не удалось отметить топики прочитанными"
    done="Все топики прочитаны"
  />

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
      {{
        row.commentsCount || 0
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
      <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
    </template>
  </DataTable>
</template>
