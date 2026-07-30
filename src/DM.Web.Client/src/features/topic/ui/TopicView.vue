<script setup lang="ts">
/**
 * Topic — forum topic bubble. Domain wrapper over the presentational
 * TopicCard: derives viewer-dependent permissions (like/warn/edit/delete/
 * close) from the user store, builds forum route targets, and translates
 * card events into id-carrying emits for the owning page/store. Hosts the
 * inline edit form (title + first-post BBCode) itself so TopicCard stays a
 * pure presentational shell.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { forumApi } from "@/entities/forum";
import type { Topic } from "@/entities/forum";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { unwrapResource } from "@/shared/api";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { PeriodDigestBoards } from "@/features/leaderboard/@x/topic";
import TopicCard from "./TopicCard.vue";
import { notifyFailure } from "@/shared/lib/errors";

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
  }>(),
  {
    truncatable: false,
    maxHeight: 150,
    headingLevel: "h3",
    titleLink: true,
    previewOnly: false,
    digestExpanded: false,
  },
);

const emit = defineEmits<{
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
  /** Persist an edit (title + first-post text). */
  saveEdit: [id: string, patch: { title: string; description: string }];
  /** Request deletion (parent confirms + deletes + navigates). */
  delete: [id: string];
  /** Toggle the closed/open state (moderator). */
  toggleClose: [id: string];
}>();

const { user: currentUser } = storeToRefs(useAuthStore());

const topicRoute = computed(() => ({
  name: "topic",
  params: { alias: props.topic.board?.alias, num: props.topic.topicNumber },
}));

// The unread deep link is an authenticated-only affordance: guests never
// receive unread counters, so the card falls back to the plain comments link.
const unreadRoute = computed(() =>
  currentUser.value ? { ...topicRoute.value, query: { unread: 1 } } : null,
);

const isModerator = computed(() => userIsModerator(currentUser.value));

const isAuthor = computed(
  () => currentUser.value?.username === props.topic.author?.username,
);

const isBoardModerator = computed(
  () =>
    !!currentUser.value &&
    (props.topic.board?.moderators ?? []).some(
      (m) => m.username === currentUser.value?.username,
    ),
);

const isClosed = computed(() => props.topic.isClosed);

// Lifecycle gating mirrors the backend TopicIntentionResolver + controller:
// edit = author (while open) / board-moderator / global moderator+; close &
// delete = board-moderator / global moderator+ (delete also allows author).
const canManage = computed(
  () => !props.previewOnly && (isModerator.value || isBoardModerator.value),
);
const canEditTopic = computed(
  () =>
    !props.previewOnly &&
    !isEditing.value &&
    (canManage.value || (isAuthor.value && !isClosed.value)),
);
const canDeleteTopic = computed(
  () => !props.previewOnly && (canManage.value || isAuthor.value),
);
const canCloseTopic = computed(() => canManage.value);

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
const editLoading = ref(false);
const saving = ref(false);

async function startEdit() {
  if (editLoading.value || isEditing.value) return;
  // The displayed description is server-rendered HTML; the editor needs the
  // raw BBCode source, which only the AuthorEdit audience returns (same
  // fetch-before-edit idiom as the chat message editors).
  editLoading.value = true;
  const { data, error } = await forumApi.getTopicForUpdate(props.topic.id);
  editLoading.value = false;
  if (error) {
    notifyFailure(error, "Не удалось загрузить текст темы");
    return;
  }
  editTitle.value = props.topic.title;
  editText.value = unwrapResource<Topic>(data)?.description ?? "";
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
}

// Only the title is required — the backend contract allows an empty topic
// text (the period digests are created that way: their content is the
// injected leaderboards, not text).
function saveEdit() {
  if (!editTitle.value.trim() || saving.value) return;
  emit("saveEdit", props.topic.id, {
    title: editTitle.value.trim(),
    description: editText.value.trim(),
  });
  isEditing.value = false;
}
</script>

<template>
  <div v-if="isEditing" class="topic-edit">
    <label class="edit-label">Заголовок темы</label>
    <input
      v-model="editTitle"
      type="text"
      class="edit-title"
      maxlength="130"
      placeholder="Заголовок темы"
    />
    <label class="edit-label">Текст</label>
    <BBCodeEditor
      v-model="editText"
      context="common"
      placeholder="Текст темы..."
      :min-height="120"
      :max-height="400"
      :is-moderator="isModerator"
      @submit="saveEdit"
    />
    <div class="edit-actions">
      <button
        class="edit-btn save"
        :disabled="!editTitle.trim()"
        @click="saveEdit"
      >
        Сохранить
      </button>
      <button class="edit-btn" @click="cancelEdit">Отмена</button>
    </div>
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
    :can-warn="isModerator"
    :is-closed="isClosed"
    :can-edit="canEditTopic"
    :can-delete="canDeleteTopic"
    :can-close="canCloseTopic"
    :truncatable="truncatable"
    :max-height="maxHeight"
    :heading-level="headingLevel"
    :preview-only="previewOnly"
    @toggle-like="handleToggleLike"
    @warn="emit('warn', topic.id)"
    @edit="startEdit"
    @delete="emit('delete', topic.id)"
    @toggle-close="emit('toggleClose', topic.id)"
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
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.topic-edit
  display: flex
  flex-direction: column
  gap: $tiny
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

.edit-label
  color: $text-muted
  font-size: $secondary-font-size

.edit-title
  width: 100%
  box-sizing: border-box
  +input()

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

.edit-btn
  +button
  &.save
    font-weight: bold

// Digest topics: the all-statistics link in the title-row corner — muted
// at rest, footer-quiet (secondary size, normal weight) so it never
// competes with the topic title.
.digest-stats-link
  float: right
  font-weight: normal
  font-size: $secondary-font-size
  +muted-link
</style>
