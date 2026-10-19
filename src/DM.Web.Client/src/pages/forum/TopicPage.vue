<script setup lang="ts">
import { ref, computed, reactive, inject, onBeforeUnmount } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import { useBoardsStore } from "@/entities/forum";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { storeToRefs } from "pinia";
import {
  TopicView as TopicDisplay,
  type TopicEditPatch,
} from "@/features/topic";
import { LoginPrompt } from "@/features/auth";
import { WarningDialog } from "@/features/moderation-actions";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import type { Served } from "@/shared/api/models";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { forumApi } from "@/entities/forum";
import { CommentsFilter, useCommentsFilter } from "@/features/comment-filter";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { errorCodeForStatus } from "@/shared/ui/ErrorPage";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { RefusalNote } from "@/shared/ui/Layout";
import { reportForumShellError } from "./forumShell";
import { permalinkOrigin } from "@/shared/config/site";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const route = useRoute();
const router = useRouter();
const boardsStore = useBoardsStore();
const {
  trySelectTopicByNumber,
  trySelectTopic,
  searchComments,
  createComment,
} = boardsStore;
const { selectedTopic: topic } = storeToRefs(boardsStore);
const { user } = storeToRefs(useAuthStore());

const { searchParams } = useCommentsFilter();

// The skeleton until the first fetch settles, the ErrorPage on failure.
const loading = ref(true);
const errorCode = ref<number | null>(null);

useDocumentTitle(() => topic.value?.title);

const newComment = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const isModerator = computed(() => userIsModerator(user.value));

// Who may write is the topic's answer about the viewer
// (TopicIntentionResolver.CreateComment): the topic open and no ban.
const canComment = computed(
  () => !!user.value && !!topic.value?.viewer?.canComment,
);

// Under a closed topic one grey line, ahead of a guest's way in, which would
// open nothing there; a signed-in viewer the open topic refuses gets nothing.
const hasComposerPlace = computed(
  () =>
    !!topic.value && (canComment.value || topic.value.isClosed || !user.value),
);

const sendResult = useActionResult();

async function handleSend() {
  if (!newComment.value.trim() || sending.value) return;
  const text = newComment.value;
  newComment.value = "";
  sending.value = true;
  sendResult.clear();
  const { error } = await createComment(text);
  sending.value = false;
  // The text comes back on failure; the editor's clear(), which drops the saved
  // draft, waits for the send to land.
  if (error) {
    newComment.value = text;
    // A 400 names what is wrong with the text, under the button.
    sendResult.refuse(error, "Не удалось отправить комментарий");
  } else {
    editorRef.value?.clear();
  }
}

async function markAsReadIfNeeded() {
  if (!user.value) return;
  if (!topic.value?.unreadCommentsCount) return;

  await forumApi.markTopicAsRead(topic.value.id!);
  if (topic.value) topic.value.unreadCommentsCount = 0 as Served<number>;
}

// A missing, private or deleted topic is reported to the shell, which draws the
// full ErrorPage; cleared on every load and on unmount.
const reportShellError = inject(reportForumShellError, () => {});
onBeforeUnmount(() => reportShellError(null));

// A sign-in or a sign-out changes whom the topic was read for: read it again in
// place; a failed read keeps the topic on screen.
useViewerChange(() => {
  const id = topic.value?.id;
  if (id) void trySelectTopic(id);
});

async function fetchData() {
  const alias = route.params.alias as string;
  const num = parseInt(route.params.num as string);

  loading.value = true;
  errorCode.value = null;
  reportShellError(null);

  const { ok, status } = await trySelectTopicByNumber(alias, num);
  if (!ok) {
    // The topic endpoint answers Gone for a deleted topic and 404 for one that never
    // existed, so "Страница удалена" stays here.
    errorCode.value = errorCodeForStatus(status, { goneMeansRemoved: true });
    reportShellError(errorCode.value);
    // The previous topic's comments must not leak onto an error page.
    boardsStore.comments = null;
    loading.value = false;
    return;
  }

  await searchComments(searchParams.value);
  loading.value = false;
  markAsReadIfNeeded();
}

useFetchData(
  () => fetchData(),
  [
    {
      param: (p) => p.alias,
      callback: () => fetchData(),
    },
    {
      param: (p) => p.num,
      callback: () => fetchData(),
    },
  ],
);

// A refused like changes nothing on "Нравится", so a line under the card says it.
const likeResult = useActionResult();

async function handleLike(id: string) {
  likeResult.clear();
  const { error } = await boardsStore.likeTopic(id);
  if (error) likeResult.refuse(error, "Не удалось отметить");
}

async function handleUnlike(id: string) {
  likeResult.clear();
  const { error } = await boardsStore.unlikeTopic(id);
  if (error) likeResult.refuse(error, "Не удалось убрать отметку");
}

// The edit form waits for this answer: it closes on success, stays open on a refusal.
async function submitTopicEdit(id: string, patch: TopicEditPatch) {
  const { error } = await boardsStore.updateTopicContent(id, patch);
  return { error: error ?? null };
}

const showDeleteConfirm = ref(false);

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmDeleteTopic() {
  const id = topic.value?.id;
  if (!id) return;
  const result = await boardsStore.deleteTopic(id, OWNS_REFUSAL);
  if (result.error) return result;
  router.push({ name: "forum", params: { alias: route.params.alias } });
  return { error: null };
}

// The warn button is gated by rank inside <Topic>; the dialog is prefilled
// with the author and a permalink to the topic.
const warnUsername = ref("");
const warnEntityId = ref<string | undefined>(undefined);
const warnEntityLink = ref<string | undefined>(undefined);

const { open: openWarnDialog, close: closeWarnDialog } = useModal({
  component: WarningDialog,
  attrs: reactive({
    username: warnUsername,
    entityId: warnEntityId,
    entityType: "Topic",
    entityLink: warnEntityLink,
    onSuccess: () => closeWarnDialog(),
    onCancel: () => closeWarnDialog(),
  }),
});

function handleWarn(id: string) {
  // A removed author arrives as null: nothing to warn.
  const username = topic.value?.author?.username;
  if (!username) return;

  warnUsername.value = username;
  warnEntityId.value = id;
  warnEntityLink.value =
    permalinkOrigin() +
    router.resolve({
      name: "topic",
      params: { alias: route.params.alias, num: route.params.num },
    }).href;
  openWarnDialog();
}
</script>

<template>
  <!-- The error page is the shell's (reportForumShellError); this leaf draws nothing then. -->

  <template v-if="!errorCode">
    <CommentSkeleton v-if="loading && !topic" :count="3" />

    <template v-else-if="topic">
      <!-- The topic name is the card's heading, plain text: the card is on the topic's own page. -->
      <TopicDisplay
        :topic="topic"
        heading-level="h2"
        :title-link="false"
        digest-expanded
        :submit-edit="submitTopicEdit"
        @like="handleLike"
        @unlike="handleUnlike"
        @warn="handleWarn"
        @delete="showDeleteConfirm = true"
      />
      <ActionResult :result="likeResult" />

      <CommentsFilter class="topic-filter" />

      <router-view class="topic-comments" />
    </template>
  </template>

  <div v-if="hasComposerPlace && !errorCode" class="comment-input-wrapper">
    <div class="comment-input-container">
      <template v-if="canComment">
        <BBCodeEditor
          ref="editorRef"
          v-model="newComment"
          context="common"
          placeholder="Написать комментарий..."
          :draft-key="composerDraftKey('topic', 'comment', topic?.id)"
          :disabled="sending"
          :min-height="100"
          :max-height="300"
          :max-length="BODY_TEXT_MAX_LENGTH"
          :resizable="true"
          :is-moderator="isModerator"
          @submit="handleSend"
        />
        <Button
          type="button"
          variant="primary"
          class="comment-send-button"
          :disabled="sending || !newComment.trim()"
          @click="handleSend"
        >
          Отправить
        </Button>
        <ActionResult :result="sendResult" class="comment-send-refusal" />
      </template>

      <!-- A refusal, so the plaque of one, flush with the comments. -->
      <RefusalNote v-else-if="topic?.isClosed" class="comment-closed-hint">
        Топик закрыт для комментариев
      </RefusalNote>
      <LoginPrompt v-else action="оставить комментарий" />
    </div>
  </div>

  <ConfirmDialog
    v-model:show="showDeleteConfirm"
    title="Удалить топик?"
    message="Топик и все его комментарии будут удалены без возможности восстановления"
    confirm-label="Удалить"
    failure="Не удалось удалить топик"
    :action="confirmDeleteTopic"
  />
</template>

<style scoped lang="sass">
.topic-filter
  margin-top: $medium

.topic-comments
  margin-top: $medium

.comment-input-wrapper
  margin-top: $medium

.comment-input-container
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

.comment-send-button
  align-self: flex-start

// Spaced by the gap of the column.
.comment-send-refusal
  --action-result-margin: 0
</style>
