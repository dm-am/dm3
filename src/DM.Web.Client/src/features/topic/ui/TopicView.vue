<script setup lang="ts">
import { computed, ref } from "vue";
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
import type { HeadingLevel } from "./headingLevel";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { TOPIC_TITLE_MAX_LENGTH } from "@/shared/lib/constants/forum";

/** `isClosed` and `isAttached` go only from a viewer who may change them. */
export type TopicEditPatch = {
  title: string;
  description: string;
  isClosed?: boolean;
  isAttached?: boolean;
};

const props = withDefaults(
  defineProps<{
    topic: Topic;
    truncatable?: boolean;
    maxHeight?: number;
    headingLevel?: HeadingLevel;
    /** Off on the topic page itself, where the title would link to itself. */
    titleLink?: boolean;
    previewOnly?: boolean;
    /** Digest boards start expanded on the topic page and as a teaser in lists. */
    digestExpanded?: boolean;
    /** A function, not an event: only the answer may close the form. */
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
  },
);

const emit = defineEmits<{
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
  delete: [id: string];
}>();

const { user: currentUser } = storeToRefs(useAuthStore());

const topicRoute = computed(() => ({
  name: "topic",
  params: { alias: props.topic.board?.alias, num: props.topic.topicNumber },
}));

// A guest has no read marker, so for him the counter keeps the plain link.
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

// A removed author has nobody to warn; only a rank below the reader's (reachesByRank).
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

// What a board moderator or an administrator closed is not the author's to reopen.
const authorHoldsTopic = computed(
  () =>
    isAuthor.value && (!props.topic.isClosed || !!props.topic.closedByAuthor),
);

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
// A change of the closed state refused for one field would take the whole edit down.
const canCloseTopic = computed(
  () =>
    !props.previewOnly &&
    (userIsAdmin(currentUser.value) ||
      isBoardModerator.value ||
      authorHoldsTopic.value),
);

// The board's moderators and administrators alone, for the same reason as closing.
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

const isEditing = ref(false);
const editTitle = ref("");
const editText = ref("");
const editClosed = ref(false);
const editPinned = ref(false);
const editLoading = ref(false);
const saving = ref(false);

// Random suffix: the card is also rendered in a list, and ids are unique per page.
const titleId = `topic-edit-title-${Math.random().toString(36).slice(2, 9)}`;

const EDIT_SOURCE_FAILED = "Не удалось загрузить текст топика";

// A refusal of "Редактировать" is printed under the card, where the reader looks.
const editSource = useActionResult();

async function startEdit() {
  if (editLoading.value || isEditing.value) return;
  editSource.clear();
  // The description is rendered HTML; the author_edit answer is the text as typed.
  editLoading.value = true;
  const { data, error } = await forumApi.getTopicForUpdate(props.topic.id);
  editLoading.value = false;
  if (error) {
    editSource.refuse(error, EDIT_SOURCE_FAILED);
    return;
  }
  editTitle.value = props.topic.title;
  editText.value = unwrapResource<Topic>(data)?.description ?? "";
  editClosed.value = props.topic.isClosed;
  editPinned.value = props.topic.isAttached;
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  saveResult.clear();
}

const saveResult = useActionResult();

// One PATCH carries text, closed and pinned, so a refusal leaves the topic whole.
async function saveEdit() {
  if (!props.submitEdit || !editTitle.value.trim() || saving.value) return;
  const patch: TopicEditPatch = {
    title: editTitle.value,
    description: editText.value,
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
    <label v-if="canCloseTopic" class="checkbox-label">
      <input v-model="editClosed" type="checkbox" />
      Топик закрыт
    </label>
    <label v-if="canPinTopic" class="checkbox-label">
      <input v-model="editPinned" type="checkbox" />
      Топик закреплен
    </label>
    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        :disabled="!editTitle.trim() || saving"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" @click="cancelEdit"> Отмена </Button>
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
    :is-closed="topic.isClosed"
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

  <ActionResult :result="editSource" />
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Links" as *

.topic-edit
  display: flex
  flex-direction: column
  gap: $minor
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

.edit-label
  +field-caption

  & ~ &
    margin-top: $minor

.edit-title
  width: 100%
  box-sizing: border-box
  +input()

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

.digest-stats-link
  float: right
  font-weight: normal
  font-size: $secondary-font-size
  +muted-link
</style>
