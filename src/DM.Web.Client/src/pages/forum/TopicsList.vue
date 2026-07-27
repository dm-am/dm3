<script setup lang="ts">
import { computed, watch, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { ErrorState } from "@/shared/ui/ErrorState";
import { LoginPrompt } from "@/features/auth";
import { ContentText } from "@/shared/ui";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  useBoardsStore,
  forumApi,
  type Topic,
  type BoardId,
} from "@/entities/forum";
import { UserLink, userIsModerator } from "@/entities/user";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { TopicsFilter, useTopicsFilter } from "@/features/topic-filter";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import PinnedTopicsManager from "./PinnedTopicsManager.vue";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { useAnimatedHeightToggle } from "@/shared/lib/composables";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";

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

// Moderator actions state
const pinningTopicId = ref<string | null>(null);
const showPinnedManager = ref(false);
const savingPinnedOrder = ref(false);

// Check if current user can moderate this board
const canModerate = computed(() => {
  if (!user.value) return false;

  // Global moderators/admins
  if (userIsModerator(user.value)) return true;

  // Board-specific moderators
  const boardModerators = selectedBoard.value?.moderators ?? [];
  return boardModerators.some((m) => m.username === user.value?.username);
});

// Columns for topics table. Likes column is always rendered so the
// "sort by likes" filter has a visible target — the column doubles as
// confirmation of the active sort. Moderator actions column tacks onto
// the end only when the viewer can moderate this board.
const columns = computed<Column[]>(() => {
  // Alignment principle: identifier/text columns left, short values (counts
  // and dates) center. The table uses the default "fixed" layout with
  // hard px widths on every column except "Топик": widths are set once to
  // fit the longest real content (longest seeded username + role badge,
  // the full "01.01.2020 в 01:00" date-time, the one-line column labels)
  // and therefore NEVER change between boards or pages — only "Топик"
  // absorbs the remaining space, and that remainder is viewport-derived,
  // not content-derived, so it is identical on every page too.
  const base: Column[] = [
    {
      key: "title",
      label: "Топик",
      align: "left",
    },
    // NOTE: DataTable cells are content-box, so these px are the CONTENT
    // width — the 12px+12px cell padding sits on top of them.
    // 180px: longest seeded author cell "Робот-Администратор [A]" is
    // ~176px, kept on one line (nowrap).
    { key: "author", label: "Автор", width: "180px", align: "left" },
    // 100px: the bold "Комментарии" label (~97px) is the widest content.
    { key: "comments", label: "Комментарии", width: "100px", align: "center" },
    { key: "likes", label: "Лайки", width: "46px", align: "center" },
    // Date columns: the widest content is the header label itself, not the
    // "01.01.2020 в 01:00" value (~132px at regular weight) — the bold
    // "Дата создания" is ~103px, the bold "Последняя активность" ~158px
    // (measured at 700 16px PT Sans). Each column is sized to keep its
    // label AND value on one line; "Топик" absorbs the difference.
    {
      key: "created",
      label: "Дата создания",
      width: "134px",
      align: "center",
      hideOnMobile: true,
    },
    {
      key: "lastActivity",
      label: "Последняя активность",
      width: "160px",
      align: "center",
      hideOnMobile: true,
    },
  ];

  if (canModerate.value) {
    base.push({ key: "actions", label: "", width: "56px", align: "center" });
  }

  return base;
});

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

  // No filters: pinned first (from attachedTopics, page 1 only), then regular
  const pinned: DisplayTopic[] = showPinnedBlock.value
    ? (attachedTopics.value ?? []).map((t) => ({ ...t, isPinned: true }))
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

// =============================================================================
// CREATE TOPIC
// =============================================================================

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
    const { data, error } = await forumApi.createTopic(
      selectedBoard.value.alias as BoardId,
      { title: newTitle.value.trim(), text: newText.value },
    );

    if (error) {
      const fieldErrors = parseApiErrors(error);
      createErrors.value = fieldErrors;
      // Fall back to the general error title when the response carried no
      // field-level validation errors (e.g. a GeneralError, not a
      // BadRequestError) — this covers 403 (board policy) and 500 alike.
      if (!Object.keys(fieldErrors).length) {
        createGeneralError.value = error.title || "Не удалось создать топик";
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
    <div class="filter-row">
      <TopicsFilter class="filter-row-filter" />
    </div>

    <!-- Error state: a failed load must not be presented as an empty list.
         Shown only when there are no stale rows to keep on screen
         (stale-while-revalidate keeps already-loaded topics otherwise). -->
    <ErrorState
      v-if="topicsError && !displayTopics.length"
      message="Не удалось загрузить топики"
      :retry="fetchTopics"
    />

    <!-- Topics table. Treat "board not yet resolved" as loading so the empty
         state never flashes before topics can be fetched. The scroll wrapper
         plus the table's min-width (see styles) are a floor for narrow
         content areas: the fixed columns keep their guaranteed widths and
         the "Топик" column keeps a readable remainder — the table scrolls
         inside the wrapper instead of crushing the title to zero. -->
    <div v-else class="topics-table-scroll">
      <DataTable
        ref="tableRef"
        :columns="columns"
        :data="displayTopics"
        :loading="topicsLoading || !selectedBoard"
        :empty-text="emptyText"
      >
        <template #cell-title="{ row }">
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
        </template>

        <template #cell-author="{ row }">
          <span v-if="row.author" class="author-cell"
            ><UserLink :user="row.author"
          /></span>
          <span v-else class="muted">удаленный пользователь</span>
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
          <HumanDate :date="row.createdUtc" format="DD.MM.YYYY [в] HH:mm" />
        </template>

        <template #cell-lastActivity="{ row }">
          <template v-if="row.lastComment">
            <Tooltip
              :text="
                row.lastComment.author?.username ?? 'удаленный пользователь'
              "
            >
              <router-link
                :to="lastCommentLink(row)"
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
              :aria-label="row.isPinned ? 'Открепить топик' : 'Закрепить топик'"
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
         at the end of a thread): guests get the login CTA, members the
         toggle button with the inline form unfolding right here. -->
    <div class="create-topic-section">
      <LoginPrompt v-if="!user" action="создать топик" />
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
      v-show="showCreateForm || createFormPinnedHeight !== null"
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
        v-if="showCreateForm"
        class="create-topic-form"
        @submit.prevent="handleCreateTopic"
      >
        <form-field
          label="Заголовок"
          name="title"
          :errors="titleError ? [titleError] : []"
        >
          <input
            v-model="newTitle"
            type="text"
            class="create-topic-title-input"
            placeholder="Заголовок топика"
            maxlength="200"
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

        <div v-if="createGeneralError" class="create-topic-error" role="alert">
          {{ createGeneralError }}
        </div>

        <div class="create-topic-actions">
          <button
            type="submit"
            class="create-topic-submit"
            :disabled="creating || !newTitle.trim() || !newText.trim()"
          >
            {{ creating ? "Создание..." : "Создать" }}
          </button>
        </div>
      </form>
    </div>

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
@import "@/assets/styles/Inputs"

.topics-page
  display: flex
  flex-direction: column
  gap: $small

.board-description
  padding: $small $medium
  border: 1px dashed $border
  background-color: $bg-element
  color: $text
  font-size: $secondary-font-size

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

.moderator-actions
  display: flex
  gap: $small
  margin-bottom: $medium

.manage-pinned-button
  display: inline-flex
  align-items: center
  gap: $small
  +button

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

// Longest username + role badge must stay on one line
.author-cell
  white-space: nowrap

// Date-time values ("01.01.2020 в 01:00") must never break across lines —
// the fixed column widths are sized to fit them, nowrap is the guarantee.
// :deep — the td elements belong to the DataTable child component.
:deep(td.col-created),
:deep(td.col-lastActivity)
  white-space: nowrap

// Floor for narrow content areas: the fixed columns total ~748px, so
// without a minimum the flexible "Топик" column would collapse to zero
// when the viewport squeezes the content column. min-width keeps a
// readable title share and the wrapper scrolls the overflow; at normal
// desktop widths the table fits and the wrapper is inert. (min-width on
// individual cells is ignored by table-layout: fixed — it must live on
// the table box itself.)
.topics-table-scroll
  overflow-x: auto
  // width:0 + min-width:100% — the standard intrinsic-size containment
  // trick: the wrapper contributes ~0 to every ancestor's min-content
  // (so the wide table can NOT stretch the page's content column through
  // the flex min-width:auto chain), while min-width stretches the
  // rendered box back to the full content width. Without it the table's
  // 950px would propagate up and widen the whole page instead of
  // scrolling inside this wrapper.
  width: 0
  min-width: 100%

  :deep(.data-table)
    min-width: 950px

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
  border-radius: $border-radius

.create-topic-title-input
  width: 100%
  +input()

.create-topic-error
  padding: $small
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

.create-topic-actions
  display: flex
  justify-content: flex-end

.create-topic-submit
  +button
</style>
