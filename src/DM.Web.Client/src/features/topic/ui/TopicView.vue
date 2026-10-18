<script setup lang="ts">
/**
 * Topic — forum topic bubble. Domain wrapper over the presentational
 * TopicCard: derives viewer-dependent permissions (like/warn/edit/delete/
 * close/pin) from the user store, builds forum route targets, and translates
 * card events into id-carrying emits for the owning page/store. Hosts the
 * inline edit form (title + first-post BBCode, and for those who may close or
 * pin the topic whether it is closed and pinned) itself so TopicCard stays a
 * pure presentational shell; the form saves through the submitEdit function
 * the page passes in, since only the answer may close it.
 */
import { computed, ref } from "vue";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { storeToRefs } from "pinia";
import { forumApi } from "@/entities/forum";
import type { Topic } from "@/entities/forum";
import { useAuthStore, userIsAdmin, userIsModerator } from "@/entities/user";
import { reachesByRank } from "@/entities/moderation";
import { unwrapResource } from "@/shared/api";
import type { GeneralError } from "@/shared/api/models/common";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { PeriodDigestBoards } from "@/features/leaderboard/@x/topic";
import TopicCard from "./TopicCard.vue";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { TOPIC_TITLE_MAX_LENGTH } from "@/shared/lib/constants/forum";

/** What the edit form saves. `isClosed` is sent only by a viewer who may
 * close the topic, and `isAttached` only by one who may pin it: the server
 * refuses a change of either from anybody else. */
export type TopicEditPatch = {
  title: string;
  description: string;
  isClosed?: boolean;
  isAttached?: boolean;
};

const props = withDefaults(
  defineProps<{
    topic: Topic;
    /** Enable content truncation (for news list) */
    truncatable?: boolean;
    /** Max height before truncation (px) */
    maxHeight?: number;
    /** Heading level for the title — "h2" when the card is the topic
     * page's section heading, "h3" (default) when embedded in a list
     * (news). */
    headingLevel?: "h1" | "h2" | "h3";
    /** Whether the in-card title links to the topic page. Disable on the
     * topic page itself: the card already sits on that page, so the title
     * renders as plain text instead of a self-link. */
    titleLink?: boolean;
    /** Preview-only: render the card without interactive footer actions
     * (like + warn + lifecycle). Used by the home news list. */
    previewOnly?: boolean;
    /** Period-digest topics ("Итоги …") render their leaderboards inside
     * the card; start them expanded (all boards, top-10) on the topic page,
     * collapsed to the teaser (3 boards, top-5 + "показать полностью")
     * in lists. */
    digestExpanded?: boolean;
    /**
     * Saves the edit form and answers whether the server took it. A function
     * rather than an event, because only the answer may close the form: an
     * event has no result to wait for, and closing on the emit threw a refused
     * edit away together with what was typed. The same shape as CommentItem's
     * submitEdit. Without it the card offers no edit at all.
     */
    submitEdit?: (
      id: string,
      patch: TopicEditPatch,
    ) => Promise<{ error: GeneralError | null }>;
  }>(),
  {
    truncatable: false,
    maxHeight: 150,
    headingLevel: "h3",
    titleLink: true,
    previewOnly: false,
    digestExpanded: false,
    submitEdit: undefined,
  },
);

const emit = defineEmits<{
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
  /** Request deletion (parent confirms + deletes + navigates). */
  delete: [id: string];
}>();

const { user: currentUser } = storeToRefs(useAuthStore());

const topicRoute = computed(() => ({
  name: "topic",
  params: { alias: props.topic.board?.alias, num: props.topic.topicNumber },
}));

// Where the reader continues: a resolver route that asks the server for the
// comment he stopped at and replaces itself with the topic route pointing at
// it. Authenticated-only, a guest has no read marker of his own, so for him
// the counter keeps the plain link to the topic.
const unreadRoute = computed(() =>
  currentUser.value
    ? {
        name: "topic-unread",
        params: {
          alias: props.topic.board?.alias,
          num: props.topic.topicNumber,
        },
      }
    : null,
);

const isModerator = computed(() => userIsModerator(currentUser.value));

// A warning goes to the author, and the author of a topic whose account was
// removed is nobody: the page would have no one to address it to. It reaches
// only a rank below the reader's and never an administrator (reachesByRank,
// the server's rank rule).
const canWarn = computed(() =>
  reachesByRank(currentUser.value, props.topic.author),
);

const isAuthor = computed(
  () =>
    !!currentUser.value &&
    currentUser.value.username === props.topic.author?.username,
);

const isBoardModerator = computed(
  () =>
    !!currentUser.value &&
    (props.topic.board?.moderators ?? []).some(
      (m) => m.username === currentUser.value?.username,
    ),
);

const isClosed = computed(() => props.topic.isClosed);

// The author's say over the topic: while it is open, or closed by the author.
// What a board moderator or an administrator closed is not the author's to
// reopen (TopicIntention.Close), and the server marks which it was.
const authorHoldsTopic = computed(
  () => isAuthor.value && (!isClosed.value || !!props.topic.closedByAuthor),
);

// Lifecycle gating mirrors the backend TopicIntentionResolver + controller:
// edit = author (while open or closed by the author) / board-moderator /
// global moderator+; delete = board-moderator / global moderator+ (delete
// also allows author).
const canManage = computed(
  () => !props.previewOnly && (isModerator.value || isBoardModerator.value),
);
const canEditTopic = computed(
  () =>
    !props.previewOnly &&
    !!props.submitEdit &&
    !isEditing.value &&
    (canManage.value || authorHoldsTopic.value),
);
const canDeleteTopic = computed(
  () => !props.previewOnly && (canManage.value || isAuthor.value),
);
// The server lets the board's moderators and administrators close any topic
// and the author close their own and reopen it if they closed it themselves
// (TopicIntention.Close). A topic the author closed themselves stays theirs to
// edit (TopicIntention.Edit), so the author meets the form with the box on it
// as on an open one. A topic closed by a board moderator or an administrator offers the
// author neither. A global moderator who does not moderate this board may edit
// the topic, but a change of its closed state is refused, and in the edit form
// that refusal would take the title and text down with it, so the setting is
// not offered to them.
const canCloseTopic = computed(
  () =>
    !props.previewOnly &&
    (userIsAdmin(currentUser.value) ||
      isBoardModerator.value ||
      authorHoldsTopic.value),
);

// Pinning is the board's own business: its moderators and administrators pin
// and unpin a topic (ForumIntention.AdministrateTopics), and nobody else, the
// author and a moderator of another board included. The box is offered to
// them alone for the reason the closed box is: a refused change of the pin
// would take the title and text of the same request down with it.
const canPinTopic = computed(
  () =>
    !props.previewOnly &&
    (userIsAdmin(currentUser.value) || isBoardModerator.value),
);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return (
    props.topic.likes?.some(
      (u) => u.username === currentUser.value?.username,
    ) ?? false
  );
});

const canLike = computed(() => {
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

function handleToggleLike() {
  if (isLikedByMe.value) {
    emit("unlike", props.topic.id);
  } else {
    emit("like", props.topic.id);
  }
}

// --- Inline edit (title + first-post text) ---
const isEditing = ref(false);
const editTitle = ref("");
const editText = ref("");
const editClosed = ref(false);
const editPinned = ref(false);
const editLoading = ref(false);
const saving = ref(false);

// The id the title's caption points at. Random suffix, because the card is
// rendered in a list as well (the home news column) and an id has to be unique
// on the page. Same shape as FormField generates.
const titleId = `topic-edit-title-${Math.random().toString(36).slice(2, 9)}`;

const EDIT_SOURCE_FAILED = "Не удалось загрузить текст топика";

/**
 * Why "Редактировать" did nothing, printed under the card it sits in.
 *
 * The reader has just pressed a button in the card's footer and is looking at
 * it; a plate in the corner of the page is the one place they are not looking.
 * The line stands under the card (the card has no slot under its footer, and
 * the footer is its last row), needs no click to dismiss and clears itself on
 * the next press, which is also the retry. The plate the interceptor held for
 * the same refusal is taken down (UI_STANDARDS, "Всплывающие сообщения"); an
 * expired session and a dead connection stay with the plate. Same shape as the
 * comment's footer line.
 */
const editSource = useActionResult();

async function startEdit() {
  if (editLoading.value || isEditing.value) return;
  editSource.clear();
  // The displayed description is server-rendered HTML; the editor needs the
  // raw BBCode source, which only the AuthorEdit audience returns (same
  // fetch-before-edit idiom as the chat message editors).
  editLoading.value = true;
  const { data, error } = await forumApi.getTopicForUpdate(props.topic.id);
  editLoading.value = false;
  if (error) {
    editSource.refuse(error, EDIT_SOURCE_FAILED);
    return;
  }
  editTitle.value = props.topic.title;
  editText.value = htmlToBbcode(unwrapResource<Topic>(data)?.description ?? "");
  editClosed.value = props.topic.isClosed;
  editPinned.value = props.topic.isAttached;
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  saveResult.clear();
}

/** A refused save, under the buttons of the edit form, which stays open. */
const saveResult = useActionResult();

// Only the title is required — the backend contract allows an empty topic
// text (the period digests are created that way: their content is the
// injected leaderboards, not text).
//
// The closed and pinned states ride in the same request as the text: the
// server's PATCH takes them all at once, so a refusal leaves the topic as it
// was in full rather than half-saved.
async function saveEdit() {
  if (!props.submitEdit || !editTitle.value.trim() || saving.value) return;
  const patch: TopicEditPatch = {
    title: editTitle.value.trim(),
    description: editText.value.trim(),
  };
  if (canCloseTopic.value) patch.isClosed = editClosed.value;
  if (canPinTopic.value) patch.isAttached = editPinned.value;
  saving.value = true;
  saveResult.clear();
  const { error } = await props.submitEdit(props.topic.id, patch);
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить топик");
    return;
  }
  isEditing.value = false;
}
</script>

<template>
  <div v-if="isEditing" class="topic-edit">
    <label class="edit-label" :for="titleId">Заголовок топика</label>
    <input
      :id="titleId"
      v-model="editTitle"
      type="text"
      class="edit-title"
      :maxlength="TOPIC_TITLE_MAX_LENGTH"
      placeholder="Заголовок топика"
    />
    <span class="edit-label">Текст</span>
    <BBCodeEditor
      v-model="editText"
      context="common"
      placeholder="Текст топика..."
      :min-height="120"
      :max-height="400"
      :is-moderator="isModerator"
      @submit="saveEdit"
    />
    <!-- Closing is a setting of the topic, so it is changed here with the rest
         of it; the card's caption line has no word for it. The site's form
         checkbox, worded as a state like the room settings' flags. -->
    <label v-if="canCloseTopic" class="checkbox-label">
      <input v-model="editClosed" type="checkbox" />
      Топик закрыт
    </label>
    <!-- Pinning, the same kind of setting, under it. -->
    <label v-if="canPinTopic" class="checkbox-label">
      <input v-model="editPinned" type="checkbox" />
      Топик закреплен
    </label>
    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        class="edit-btn save"
        :disabled="!editTitle.trim() || saving"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" class="edit-btn" @click="cancelEdit">
        Отмена
      </Button>
    </div>
    <ActionResult :result="saveResult" />
  </div>

  <TopicCard
    v-else
    :title="topic.title"
    :title-to="titleLink ? topicRoute : null"
    :content-html="topic.description"
    :author="topic.author"
    :created-utc="topic.createdUtc"
    :modified-utc="topic.modifiedUtc"
    :comments-count="topic.commentsCount"
    :comments-to="topicRoute"
    :unread-comments-count="topic.unreadCommentsCount"
    :unread-to="unreadRoute"
    :likes="topic.likes"
    :can-like="canLike"
    :is-liked-by-me="isLikedByMe"
    :can-warn="canWarn"
    :is-closed="isClosed"
    :can-edit="canEditTopic"
    :can-delete="canDeleteTopic"
    :truncatable="truncatable"
    :max-height="maxHeight"
    :heading-level="headingLevel"
    :preview-only="previewOnly"
    @toggle-like="handleToggleLike"
    @warn="emit('warn', topic.id)"
    @edit="startEdit"
    @delete="emit('delete', topic.id)"
  >
    <!-- Digest topics: the all-statistics link in the right corner of the
         title row (owner-picked placement), always visible. -->
    <template v-if="topic.periodDigest" #title-extra>
      <router-link :to="{ name: 'site-statistics' }" class="digest-stats-link"
        >Вся статистика сайта</router-link
      >
    </template>
    <template v-if="topic.periodDigest" #after-content>
      <PeriodDigestBoards
        :year="topic.periodDigest.year"
        :month="topic.periodDigest.month"
        :expanded="digestExpanded"
      />
    </template>
  </TopicCard>

  <!-- Why "Редактировать" did nothing, under the card whose footer it was
       pressed in. A plate in the corner used to say this. -->
  <ActionResult :result="editSource" />
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.topic-edit
  display: flex
  flex-direction: column
  gap: $minor
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

// A caption of a field of the page: +field-caption, as FormField draws it, 4
// over its field and one step of 8 under the field before it.
.edit-label
  +field-caption

  & ~ &
    margin-top: $minor

.edit-title
  width: 100%
  box-sizing: border-box
  +input()

// The recipe of the site's form checkboxes (the room settings, the new game and
// new blog forms): box and words on one line, the whole line a press target.
.checkbox-label
  display: flex
  align-items: center
  gap: $small
  margin-top: $small
  cursor: pointer

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

// Digest topics: the all-statistics link in the title-row corner — muted
// at rest, footer-quiet (secondary size, normal weight) so it never
// competes with the topic title.
.digest-stats-link
  float: right
  font-weight: normal
  font-size: $secondary-font-size
  +muted-link
</style>
