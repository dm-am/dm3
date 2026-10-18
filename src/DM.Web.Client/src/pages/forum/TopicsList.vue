<script setup lang="ts">
import { computed, nextTick, watch, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LoginPrompt } from "@/features/auth";
import { ContentText } from "@/shared/ui/Content";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { useBoardsStore, type Topic } from "@/entities/forum";
import { RemovedAccountLink, UserLink, userIsAdmin } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { TopicsFilter, useTopicsFilter } from "@/features/topic-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { usePinnedOrder } from "./usePinnedOrder";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { useAnimatedHeightToggle } from "@/shared/lib/composables/useAnimatedHeightToggle";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { notifyFailure, refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { TOPIC_TITLE_MAX_LENGTH } from "@/shared/lib/constants/forum";

const route = useRoute();
const router = useRouter();
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
const { commentsPerPage } = usePaging();

// Two-state empty text
const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Топиков по заданным фильтрам не найдено"
    : "Топиков пока нет",
);

// The topic whose pin is on its way to the server.
const pinningTopicId = ref<string | null>(null);

// The pins answer to those the server lets pin a topic: an administrator and
// the moderators of this board (BoardIntentionResolver.AdministratesTopics),
// as the "Топик закрыт" box of the edit form does. A moderator of the site who
// does not moderate this board is refused a pin, so he reads the listing as
// everybody does.
const canPin = computed(() => {
  if (!user.value) return false;
  if (userIsAdmin(user.value)) return true;
  const boardModerators = selectedBoard.value?.moderators ?? [];
  return boardModerators.some((m) => m.username === user.value?.username);
});

// Columns for topics table. Likes column is always rendered so the
// "sort by likes" filter has a visible target — the column doubles as
// confirmation of the active sort. The pins of a moderator stand in the title
// cell, so every viewer has the same columns.
const columns = computed<Column[]>(() => {
  // Alignment principle: identifier/text columns left, short values (counts
  // and dates) center. Every column but "Топик" has a px width, set once to
  // fit the longest real content (longest seeded username + role badge, the
  // full "01.01.2020 в 01:00" date-time, the one-line column labels), and so
  // the same on every board and page; "Топик" absorbs the remaining space.
  //
  // Those widths add up to 740px besides "Топик", and the content column is
  // 556px at 1280 and 716px at 1440. The table lays out "auto" (DataTable):
  // where the column has the room, 1920, every column stands at its px width;
  // where it has not, the px columns give up what they can and their words
  // move to a second line.
  return [
    {
      key: "title",
      label: "Топик",
      align: "left",
    },
    // NOTE: DataTable cells are content-box, so these px are the CONTENT
    // width — the 12px+12px cell padding sits on top of them.
    // 180px: longest seeded author cell "Робот-Администратор [A]" is
    // ~176px, on one line wherever the column has the room.
    { key: "author", label: "Автор", width: "180px", align: "left" },
    // 100px: the bold "Комментарии" label (~97px) is the widest content.
    { key: "comments", label: "Комментарии", width: "100px", align: "center" },
    { key: "likes", label: "Лайки", width: "46px", align: "center" },
    // Date columns: the widest content is the header label itself, not the
    // "01.01.2020 в 01:00" value (~132px at regular weight) — the bold
    // "Дата создания" is ~103px, the bold "Последняя активность" ~158px
    // (measured at 700 16px PT Sans). Each column is sized to keep its
    // label AND value on one line; "Топик" absorbs the difference. Where the
    // table is narrower the date goes to two lines, over the time. The last
    // activity writes a name before its date and takes the lines it needs.
    {
      key: "created",
      label: "Дата создания",
      width: "134px",
      align: "center",
    },
    {
      key: "lastActivity",
      label: "Последняя активность",
      width: "160px",
      align: "center",
    },
  ];
});

// The sort of the listing, told to the table: its header announces the sort
// (aria-sort). Every sort the filter offers is a column key here
// (lastActivity, created, likes, title).
const currentSort = computed<SortState>(() => ({
  key: filterState.value.sortBy,
  direction: filterState.value.sortOrder,
}));

// Display row type (Topic with isPinned flag)
type DisplayTopic = Topic & { isPinned: boolean };

// A "unified" listing (pinned mixed in with regular topics, no separate
// pinned block rendered above) happens whenever the store didn't fetch
// attachedTopics separately — i.e. attachedTopics === null. This is the
// actual signal the store uses (hasFilters branch in searchTopics), so
// mirroring it here (instead of re-deriving hasActiveFilters) keeps the two
// definitions from drifting apart when a new filter is added to one but not
// the other.
const isUnifiedListing = computed(() => attachedTopics.value === null);

// Pinned topics are only shown as their own leading block on page 1 — from
// page 2 onward they would otherwise repeat at the top of every page.
const showPinnedBlock = computed(() => {
  if (isUnifiedListing.value) return false;
  const pageParam = route.query.number;
  if (!pageParam) return true;
  const page = parseInt(String(pageParam), 10);
  return !Number.isFinite(page) || page <= 1;
});

// The order of the pinned topics is changed on the block itself, by the pin of
// a pinned topic: dragged, or moved with the arrow keys. Only where the block
// stands on its own, at the head of the first page with no filters, and only
// when it holds two topics to swap.
const canReorder = computed(
  () =>
    canPin.value &&
    showPinnedBlock.value &&
    (attachedTopics.value?.length ?? 0) > 1,
);

const pinnedOrder = usePinnedOrder({
  pinned: attachedTopics,
  enabled: canReorder,
  send: (topicIds) => store.reorderPinnedTopics(topicIds),
  refused: (error) =>
    notifyFailure(error, "Не удалось упорядочить закрепленные топики"),
});

// Combine attached + regular topics when no filters are active
const displayTopics = computed<DisplayTopic[]>(() => {
  if (isUnifiedListing.value) {
    // With filters: show only filtered results (attached mixed in). Always
    // map isPinned from isAttached so the pin marker survives sort changes.
    return (topics.value?.resources ?? []).map((t) => ({
      ...t,
      isPinned: t.isAttached,
    }));
  }

  // No filters: pinned first (from attachedTopics, page 1 only, in the order
  // a move has just given them), then regular
  const pinned: DisplayTopic[] = showPinnedBlock.value
    ? pinnedOrder.order.value.map((t) => ({ ...t, isPinned: true }))
    : [];
  const regular: DisplayTopic[] = (topics.value?.resources ?? []).map((t) => ({
    ...t,
    isPinned: t.isAttached,
  }));
  return [...pinned, ...regular];
});

// Helper to generate topic link. Comments pagination reads its page number
// from the "number" query key (see PagingWithSeparators usage in
// CommentsList.vue / TopicPage's <router-view>), not "page".
function topicLink(row: DisplayTopic, page?: number) {
  const alias = selectedBoard.value?.alias || route.params.alias;
  const path = `/forum/${alias}/${row.topicNumber}`;
  return page && page > 1 ? `${path}?number=${page}` : path;
}

/**
 * Link to the topic at the page containing its most recent comment. With the
 * default ascending comment sort the newest comment lives on the last page,
 * computable client-side from the topic's total comment count — avoids
 * landing the reader on page 1 where the linked comment does not exist.
 */
function lastCommentLink(row: DisplayTopic) {
  const lastPage = Math.max(
    1,
    Math.ceil((row.commentsCount || 1) / commentsPerPage.value),
  );
  const base = topicLink(row, lastPage);
  return row.lastComment ? `${base}#comment-${row.lastComment.id}` : base;
}

// Paging scrolls the topics table back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
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

function fetchTopics() {
  if (!selectedBoard.value) return;
  // The store's board may briefly lag behind the URL (ForumPage resolves the
  // new alias in a post-flush watcher, AFTER this component's immediate
  // watcher already ran on mount). Fetching for the stale board would waste
  // a request and flash the previous board's rows — wait for the alias
  // match; the paramsKey watcher re-fires once ForumPage commits the board.
  if (selectedBoard.value.alias !== route.params.alias) return;
  store.searchTopics(searchParams.value);
}

// SINGLE watcher on paramsKey - handles initial load, filter changes, and board changes
// immediate: true ensures it fires on mount if board is already loaded
// Guard ensures no-op if board not yet loaded (will fire again when board loads)
watch(paramsKey, fetchTopics, { immediate: true });

/**
 * The last refused pin, in a line opened under the row of the topic whose pin
 * button was pressed (DataTable `detailsFor`).
 */
const pinRefusal = ref<{ id: DisplayTopic["id"]; text: string } | null>(null);

// Toggle pin/unpin topic (moderator action)
async function handleTogglePin(row: DisplayTopic) {
  // Not while an order is being saved: the save names the topics the block
  // holds, and a pin taken on or off under it would make it name others.
  if (pinningTopicId.value || pinnedOrder.saving.value) return;

  const topicId = String(row.id);
  // Read the direction BEFORE the request: on success the row is replaced by
  // the refreshed listing, and the refusal has to name what was attempted.
  const wasPinned = row.isPinned;
  pinningTopicId.value = topicId;
  pinRefusal.value = null;
  try {
    const { error } = await store.togglePinTopic(topicId);
    if (error) {
      const text = refusalHere(
        error,
        wasPinned ? "Не удалось открепить топик" : "Не удалось закрепить топик",
      );
      if (text) pinRefusal.value = { id: row.id, text };
    }
  } finally {
    pinningTopicId.value = null;
  }
}

/**
 * A press on the pin of a title pins or unpins its topic. The click the browser
 * sends after a drag of the pin let go is not a press. Pressed from the
 * keyboard, the pin keeps the caret: the row moves into the pinned block or
 * out of it, and focus on a node moved in the page falls to <body>.
 */
async function onPinClick(row: DisplayTopic, event: MouseEvent) {
  if (pinnedOrder.isDragClick()) return;
  const fromKeyboard = event.detail === 0;
  await handleTogglePin(row);
  if (!fromKeyboard) return;
  await nextTick();
  tableScrollRef.value
    ?.querySelector<HTMLElement>(`[data-pin-toggle="${row.id}"]`)
    ?.focus();
}

/** What the pin of a title does, under the pointer. */
function pinHint(row: DisplayTopic, index: number): string {
  if (!row.isPinned) return "Закрепить топик";
  return isMovable(row, index)
    ? "Открепить топик. Перетащите, чтобы изменить порядок"
    : "Открепить топик";
}

// =============================================================================
// ORDER OF THE PINNED TOPICS (moderator action, on the listing itself)
// =============================================================================

/** Whether a row is a topic of the pinned block that can be moved. */
function isMovable(row: DisplayTopic, index: number): boolean {
  return (
    canReorder.value && row.isPinned && index < pinnedOrder.order.value.length
  );
}

/** A press on a pin starts a drag only on a topic of the block. */
function onPinPointerDown(
  row: DisplayTopic,
  index: number,
  event: PointerEvent,
) {
  if (!isMovable(row, index)) return;
  pinnedOrder.onPointerDown(index, event, pinnedRows);
}

/** The rows of the pinned block on screen, in their order. */
function pinnedRows(): HTMLElement[] {
  const handles =
    tableScrollRef.value?.querySelectorAll<HTMLElement>("[data-pin-handle]") ??
    [];
  return [...handles]
    .map((handle) => handle.closest("tr"))
    .filter((row): row is HTMLTableRowElement => row !== null);
}

/**
 * Where the line of a drag stands at a row: above it when the held topic
 * would stand before it, below the last row when after all of them.
 */
function dropMark(row: DisplayTopic, index: number): string | null {
  if (!isMovable(row, index)) return null;
  const gap = pinnedOrder.target.value;
  if (gap === null) return null;
  if (gap === index) return "drop-before";
  const last = pinnedOrder.order.value.length - 1;
  return gap === last + 1 && index === last ? "drop-after" : null;
}

// The pin that moved its topic with an arrow keeps the caret: the listing
// draws the row anew in its place, and focus on a node taken out of the page
// falls to <body>. Only a topic of the block moves.
async function onPinKeydown(
  row: DisplayTopic,
  index: number,
  event: KeyboardEvent,
) {
  if (!isMovable(row, index)) return;
  const moved = pinnedOrder.onKeydown(index, event);
  if (!moved) return;
  await nextTick();
  tableScrollRef.value
    ?.querySelector<HTMLElement>(`[data-pin-handle="${moved}"]`)
    ?.focus();
}

const tableScrollRef = ref<HTMLElement | null>(null);

// =============================================================================
// CREATE TOPIC
// =============================================================================

// "Создать топик" is offered by the server's answer for this viewer on this
// board (board.viewer, TopicService.MayCreate): the board's policy against the
// role, and the ban. The news and the contests take topics from the moderation
// alone, and a member does not get a button there that the server would refuse.
// A guest is invited to sign in only where signing in opens a topic to an
// ordinary member: in the news it opens nothing.
const canCreateTopic = computed(
  () => !!user.value && !!selectedBoard.value?.viewer?.canCreateTopic,
);
const invitesSignIn = computed(
  () => !user.value && !!selectedBoard.value?.viewer?.canCreateTopicAfterSignIn,
);

// The answer is for whoever held the session when the board was read. Signing
// in or out on the page makes it somebody else's, so the board is read again;
// until it answers, the button is not offered.
watch(
  () => user.value?.username ?? null,
  (username, before) => {
    if (username === before) return;
    const alias = selectedBoard.value?.alias;
    if (alias) void store.trySelectBoardByAlias(alias);
  },
);

const showCreateForm = ref(false);
// Tool reveal: unified animation via the low-level toggle, intentionally
// OUT of the registry — "Развернуть все" reads content, never opens tools.
const createFormZoneRef = ref<HTMLElement | null>(null);
const {
  pinnedHeight: createFormPinnedHeight,
  toggle: animateCreateForm,
  onTransitionEnd: onCreateFormTransitionEnd,
} = useAnimatedHeightToggle(createFormZoneRef, (next) => {
  showCreateForm.value = next;
});
const newTitle = ref("");
const newText = ref("");
const creating = ref(false);
const createErrors = ref<Record<string, string[]>>({});
const createGeneralError = ref<string | null>(null);

function toggleCreateForm() {
  animateCreateForm(!showCreateForm.value);
  // setState runs synchronously inside the animated toggle — the ref
  // already holds the new value here; clear the draft when closing.
  if (!showCreateForm.value) {
    newTitle.value = "";
    newText.value = "";
    createErrors.value = {};
    createGeneralError.value = null;
  }
}

async function handleCreateTopic() {
  if (!selectedBoard.value || creating.value) return;

  creating.value = true;
  createErrors.value = {};
  createGeneralError.value = null;
  try {
    const { data, error } = await store.createTopic(selectedBoard.value.alias, {
      title: newTitle.value.trim(),
      text: newText.value,
    });

    if (error) {
      const fieldErrors = parseApiErrors(error);
      createErrors.value = fieldErrors;
      // Fall back to the general error title when the response carried no
      // field-level validation errors (e.g. a GeneralError, not a
      // BadRequestError) — a conflict, a board that no longer exists.
      //
      // The statuses the interceptor speaks for - the board policy refusing
      // the topic, a rate limit, a dead server - are said here too, and its
      // plate is taken down: the form stays open with the title and the text
      // still in it, and the line is where the reader looks. An expired
      // session and a dead connection stay with the plate.
      if (!Object.keys(fieldErrors).length) {
        createGeneralError.value = refusalHere(
          error,
          "Не удалось создать топик",
        );
      }
      return;
    }

    const created = data?.resource;
    if (created) {
      showCreateForm.value = false;
      newTitle.value = "";
      newText.value = "";
      router.push({
        name: "topic",
        params: {
          alias: selectedBoard.value.alias,
          num: created.topicNumber,
        },
      });
    }
  } finally {
    creating.value = false;
  }
}

const titleError = computed(() => getFieldError(createErrors.value, "title"));
const textError = computed(() => getFieldError(createErrors.value, "text"));
</script>

<template>
  <!-- The shared forum header stack (h1 + board strip) is rendered by the
       persistent ForumPage shell above this leaf. The highlighted strip item
       names the current board and carries the whole "where am I" role — the
       board name is not repeated as a heading here. -->
  <div class="topics-page">
    <!-- Board description (doc 4.2.3.7.2) — server-rendered BBCode. -->
    <div v-if="selectedBoard?.description" class="board-description">
      <ContentText :html="selectedBoard.description" />
    </div>

    <!-- What a move of a pinned topic did, for a screen reader: the move
         itself is only seen. -->
    <span v-if="canReorder" class="visually-hidden" aria-live="polite">{{
      pinnedOrder.announcement.value
    }}</span>

    <!-- Filter controls -->
    <div class="filter-row">
      <TopicsFilter class="filter-row-filter" />
    </div>

    <!-- Error state: a failed load must not be presented as an empty list.
         Shown only when there are no stale rows to keep on screen
         (stale-while-revalidate keeps already-loaded topics otherwise).

         The sentence comes from the store, which is where the refusal is
         read: this strip is the one carrier of it now — the request is sent
         with the flags that keep the interceptor's plate quiet — so a fixed
         phrase here would drop what the server said, and the seconds a rate
         limit asks the reader to wait along with it. -->
    <ErrorState
      v-if="topicsError && !displayTopics.length"
      :message="topicsError"
      :retry="fetchTopics"
    />

    <!-- Topics table. Treat "board not yet resolved" as loading so the empty
         state never flashes before topics can be fetched. The scroll wrapper
         plus the table's min-width (see styles) are a floor for narrow
         content areas: the fixed columns keep their guaranteed widths and
         the "Топик" column keeps a readable remainder — the table scrolls
         inside the wrapper instead of crushing the title to zero. -->
    <div v-else ref="tableScrollRef" class="topics-table">
      <DataTable
        ref="tableRef"
        :columns="columns"
        :data="displayTopics"
        :loading="topicsLoading || !selectedBoard"
        :sort="currentSort"
        :empty-text="emptyText"
        :details-for="pinRefusal?.id ?? null"
      >
        <!-- A refused pin, under the row whose pin button was pressed. -->
        <template #details>
          <ActionResult :text="pinRefusal?.text" class="pin-refusal" />
        </template>

        <!-- For an administrator and a moderator of the board the pin before
             every title is a toggle, as the heart of a like is: pale where the
             topic is not pinned, and a press pins it; the colour of the link
             where it is, and a press unpins it. The pin of a topic of the
             pinned block is also what moves it: dragged within the block, or
             with the arrow keys once it has the caret. It stands beside the
             link rather than in it, a button inside a link being two controls
             in one, and takes the place and the look the mark has in the
             link. -->
        <template #cell-title="{ row, index }">
          <span v-if="canPin" class="topic-line">
            <Tooltip
              :text="pinHint(row, index)"
              :disabled="pinnedOrder.held.value !== null"
            >
              <button
                type="button"
                class="pin-toggle"
                :class="[
                  dropMark(row, index),
                  {
                    pinned: row.isPinned,
                    'pin-handle': isMovable(row, index),
                    held:
                      isMovable(row, index) && pinnedOrder.held.value === index,
                  },
                ]"
                :data-pin-toggle="row.id"
                :data-pin-handle="isMovable(row, index) ? row.id : undefined"
                :aria-label="`Закрепить топик ${row.title}`"
                :aria-pressed="row.isPinned"
                :aria-keyshortcuts="
                  isMovable(row, index) ? 'ArrowUp ArrowDown' : undefined
                "
                @click="onPinClick(row, $event)"
                @pointerdown="onPinPointerDown(row, index, $event)"
                @pointermove="pinnedOrder.onPointerMove"
                @pointerup="pinnedOrder.onPointerUp"
                @pointercancel="pinnedOrder.onPointerCancel"
                @keydown="onPinKeydown(row, index, $event)"
              >
                <SvgIcon name="pin" class="topic-icon" />
              </button>
            </Tooltip>
            <router-link
              :to="topicLink(row)"
              :class="[
                'topic-link',
                { pinned: row.isPinned, closed: row.isClosed },
              ]"
            >
              <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
              <span
                v-if="filterState.search"
                v-html="highlightMatch(row.title, filterState.search)"
              />
              <template v-else>{{ row.title }}</template>
            </router-link>
          </span>
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
          <UserLink v-if="row.author" :user="row.author" />
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

        <!-- Who was last, then when, written in the cell as the forum index
             and DM2 write it ("Ян, 16.09.2026 в 14:17"): a name only in the
             tooltip of the date could not be read on a touch screen, where
             the tap follows the link (owner, 5 October 2026). -->
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
          <!-- A topic nobody has answered was last active when it was opened.
               The server sorts the column by that moment (last comment, else
               creation), and the forum index names a new topic as its board's
               last activity the same way, so the cell has a name and a date to
               show and no reason to print the missing-value token. -->
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

    <!-- Moderators caption, styled like the rules page "Полезные ссылки" line
         (muted label + inline links, secondary font). Rendered from the board
         payload — the board already carries its moderators, so no separate
         request is needed. Explicit space after the colon so a copied
         selection reads "Модераторы раздела: Name", not glued. -->
    <p v-if="selectedBoard?.moderators?.length" class="moderators-line">
      <span class="moderators-label">Модераторы раздела:</span>{{ " "
      }}<template
        v-for="(moderator, idx) in selectedBoard.moderators"
        :key="moderator.username"
        ><span v-if="idx > 0">, </span><UserLink :user="moderator" hide-badge
      /></template>
    </p>

    <!-- Create-topic affordance sits below the whole listing (like replying
         at the end of a thread): the toggle button with the inline form
         unfolding right here for whoever may open a topic on the board, the
         login CTA for a guest where signing in opens one, and nothing for the
         rest. -->
    <div v-if="canCreateTopic || invitesSignIn" class="create-topic-section">
      <LoginPrompt v-if="invitesSignIn" action="создать топик" />
      <button
        v-else
        type="button"
        class="create-topic-button"
        @click="toggleCreateForm"
      >
        {{ showCreateForm ? "Отмена" : "Создать топик" }}
      </button>
    </div>

    <!-- Inline create-topic form (animated tool reveal). v-show hides the
         empty closed zone: a 0-height flex item still consumes the page's
         column gap, leaving dead space under the button. -->
    <div
      v-show="
        canCreateTopic && (showCreateForm || createFormPinnedHeight !== null)
      "
      ref="createFormZoneRef"
      class="expand-zone"
      :class="{ animating: createFormPinnedHeight !== null }"
      :style="
        createFormPinnedHeight !== null
          ? { height: createFormPinnedHeight }
          : undefined
      "
      @transitionend="onCreateFormTransitionEnd"
      @transitioncancel="onCreateFormTransitionEnd"
    >
      <form
        v-if="showCreateForm && canCreateTopic"
        class="create-topic-form"
        @submit.prevent="handleCreateTopic"
      >
        <form-field
          label="Заголовок"
          name="title"
          :errors="titleError ? [titleError] : []"
        >
          <input
            id="create-topic-title"
            v-model="newTitle"
            type="text"
            class="create-topic-title-input"
            placeholder="Заголовок топика"
            :maxlength="TOPIC_TITLE_MAX_LENGTH"
          />
        </form-field>

        <form-field
          label="Текст"
          name="text"
          :errors="textError ? [textError] : []"
        >
          <BBCodeEditor
            v-model="newText"
            context="common"
            placeholder="Текст топика..."
            :min-height="150"
            :max-height="400"
            :resizable="true"
          />
        </form-field>

        <ActionResult :text="createGeneralError" class="create-topic-error" />

        <div class="create-topic-actions">
          <Button
            variant="primary"
            :loading="creating"
            :disabled="!newTitle.trim() || !newText.trim()"
          >
            Создать
          </Button>
        </div>
      </form>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.topics-page
  display: flex
  flex-direction: column
  gap: $small

// A caption, not a pinned block: the text is four to seven words in every
// board, and a dashed box around one line reads as weight the content does not
// carry. Same idiom as the moderators line at the foot of this page, so the
// listing sits between two muted captions.
.board-description
  +muted-links-line

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

// A refused pin, in the line the table opens under the row.
.pin-refusal
  --action-result-margin: 0

// The title as a moderator sees it: the pin before the link, in the place and
// the size the mark has inside the link.
.topic-line
  display: inline-flex
  align-items: center
  gap: 4px

  // The tooltip wraps the pin; laid out as a line of its own it would stand
  // the pin on the baseline instead of in the middle of the title.
  :deep(.tooltip-trigger)
    display: inline-flex

// The pin as a toggle. The same glyph as the mark, and nothing around it: no
// frame, no fill, the caret ring only. The muted text colour where the topic
// is not pinned, the colour of the mark where it is, and the hover colour of
// the link under the pointer in both.
.pin-toggle
  display: inline-flex
  padding: 0
  border: none
  background: none
  font: inherit
  color: $text-muted
  cursor: pointer

  &.pinned
    color: $link

  &:hover
    color: $link-hover

// The pin of a topic of the pinned block is the handle of a drag as well.
.pin-handle
  cursor: grab
  touch-action: none

  &.held
    cursor: grabbing

// The row held by a drag stays in its place, faded; a thin grey line between
// the rows shows where it would stand if let go.
:deep(tr:has(.pin-handle.held) > td)
  opacity: 0.5

:deep(tr:has(.pin-handle.drop-before) > td)
  box-shadow: inset 0 2px 0 $text-muted

:deep(tr:has(.pin-handle.drop-after) > td)
  box-shadow: inset 0 -2px 0 $text-muted

// No extra margin-top: the page's own $small flex gap is the whole
// table-to-caption distance — the muted line sits close to its table.
.moderators-line
  margin-top: 0
  +muted-links-line

  .moderators-label
    font-weight: 500

  // :deep — the links come from the UserLink child component
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

.create-topic-button
  +button

.create-topic-form
  display: flex
  flex-direction: column
  gap: $small
  padding: $medium
  background-color: $bg-element
  border: 1px dashed $border

.create-topic-title-input
  width: 100%
  +input()

// The refusal line of the site's forms, over the buttons; the form spaces
// its rows with a gap of its own.
.create-topic-error
  --action-result-margin: 0

.create-topic-actions
  display: flex
</style>
