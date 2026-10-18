<script setup lang="ts">
import { ref, computed, reactive, inject, onBeforeUnmount, watch } from "vue";
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

// Filter setup - get search params from URL
const { searchParams } = useCommentsFilter();

// Topic load state: skeleton until first fetch settles, ErrorPage on failure.
const loading = ref(true);
const errorCode = ref<number | null>(null);

useDocumentTitle(() => topic.value?.title);

// Comment creation state
const newComment = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const isModerator = computed(() => userIsModerator(user.value));

// Who may write is the topic's answer about the viewer, asked by the code that
// refuses the comment (TopicIntentionResolver.CreateComment): the topic open
// and no ban. The page used to ask only the first, and a banned reader was
// given the form and a refusal under it. The answer is for whoever held the
// session when the topic was read: a viewer whose session has just ended is
// offered nothing, and the topic is read again below when the viewer changes.
const canComment = computed(
  () => !!user.value && !!topic.value?.viewer?.canComment,
);

// Under the comments stands the form; under a closed topic, closed to
// everybody's comments, a guest's included, one grey line that says so, ahead
// of the way in for a guest, which would open nothing there. What was written
// under a closed topic stays to read, empty or not. A signed-in viewer the
// open topic does not take a comment from (a ban) gets nothing in that place.
const hasComposerPlace = computed(
  () =>
    !!topic.value && (canComment.value || topic.value.isClosed || !user.value),
);

/** A refused comment, under the "Отправить" that sent it. */
const sendResult = useActionResult();

async function handleSend() {
  if (!newComment.value.trim() || sending.value) return;
  const text = newComment.value;
  newComment.value = "";
  sending.value = true;
  sendResult.clear();
  const { error } = await createComment(text);
  sending.value = false;
  // Give the text back on failure. Emptying the field before the request is
  // what makes sending feel instant; losing what was written when it fails is
  // not part of that bargain. The editor's own clear() waits for the send to
  // land — it also drops the saved draft, and that copy is the one that
  // outlives the tab.
  if (error) {
    newComment.value = text;
    // A 400 is the refusal a comment gets most: the server names what is
    // wrong with the text. Without this line the text came back into the
    // field and nothing said why; it stands under the button, not in a plate.
    sendResult.refuse(error, "Не удалось отправить комментарий");
  } else {
    editorRef.value?.clear();
  }
}

async function markAsReadIfNeeded() {
  if (!user.value) return;
  if (!topic.value?.unreadCommentsCount) return;

  await forumApi.markTopicAsRead(topic.value.id!);
  // Update local state
  if (topic.value) {
    (topic.value as any).unreadCommentsCount = 0;
  }
}

// A missing/private/deleted topic is a page-level failure: report it to the
// persistent forum shell, which swaps its whole header stack for the
// full-screen ErrorPage (an error must never render squeezed under the
// forum title and board strip). Cleared on every new load and on unmount.
const reportShellError = inject(reportForumShellError, () => {});
onBeforeUnmount(() => reportShellError(null));

// The guest who signs in from the prompt under the topic is a viewer the topic
// was not read for, and so is one whose session ends. Read it again in place,
// the way the game shell reads its game: a failed read keeps the topic on
// screen, and the form follows the new answer.
watch(
  () => user.value?.username ?? null,
  () => {
    const id = topic.value?.id;
    if (id) void trySelectTopic(id);
  },
);

async function fetchData() {
  const alias = route.params.alias as string;
  const num = parseInt(route.params.num as string);

  loading.value = true;
  errorCode.value = null;
  reportShellError(null);

  const { ok, status } = await trySelectTopicByNumber(alias, num);
  if (!ok) {
    // The topic endpoint is the one that spends 410 on its literal meaning:
    // TopicService answers Gone for a topic that was deleted and 404 for one
    // that never existed, so this page keeps the "Страница удалена" branch.
    errorCode.value = errorCodeForStatus(status, { goneMeansRemoved: true });
    reportShellError(errorCode.value);
    // Drop comments from the previously viewed topic so they never leak
    // onto an error page.
    boardsStore.comments = null;
    loading.value = false;
    return;
  }

  await searchComments(searchParams.value);
  loading.value = false;
  // Mark as read after loading
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
  // Query changes are handled by CommentsList via useCommentsFilter
);

// A refused like changes nothing on the heart, so a line under the card says
// it, where the press was. It used to go unsaid, or as an unnamed plate.
const likeResult = useActionResult();

async function handleLike(id: string) {
  likeResult.clear();
  const { error } = await boardsStore.likeTopic(id);
  if (error) likeResult.refuse(error, "Не удалось поставить лайк");
}

async function handleUnlike(id: string) {
  likeResult.clear();
  const { error } = await boardsStore.unlikeTopic(id);
  if (error) likeResult.refuse(error, "Не удалось убрать лайк");
}

// --- Topic lifecycle: edit (closing and pinning included) / delete (doc 4.2.2.17 / 4.2.3.7.5) ---
// The edit form waits for this answer: it closes on success and stays open,
// with the plate, on a refusal.
async function submitTopicEdit(id: string, patch: TopicEditPatch) {
  const { error } = await boardsStore.updateTopicContent(id, patch);
  return { error: error ?? null };
}

const showDeleteConfirm = ref(false);

function handleDelete() {
  showDeleteConfirm.value = true;
}

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmDeleteTopic() {
  const id = topic.value?.id;
  if (!id) return;
  const result = await boardsStore.deleteTopic(id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (result.error) return result;
  router.push({ name: "forum", params: { alias: route.params.alias } });
  return { error: null };
}

// --- Moderator warning (doc 4.2.4.1) ---
// The warn button itself is gated by rank inside <Topic> (can-warn).
// The dialog is prefilled with the topic author and a permalink to the
// topic as the violation reference.
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
  // Deleted author accounts arrive as null — nothing to warn.
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
  <!-- Error state lives on the ForumPage shell (reportForumShellError):
       the shell hides its header stack and shows the full-screen ErrorPage,
       so this leaf renders nothing at all while errorCode is set. -->

  <!-- The shared forum header stack (h1 + board strip) is rendered by the
       persistent ForumPage shell above this leaf. The strip's active item
       doubles as the way back to the board — no separate back-link.
       document.title still reflects the topic (see useDocumentTitle). -->
  <template v-if="!errorCode">
    <!-- Loading state: skeleton before the first paint -->
    <CommentSkeleton v-if="loading && !topic" :count="3" />

    <template v-else-if="topic">
      <!-- The topic name lives INSIDE the card as its heading (same shape
           as the news cards on the home page). Plain text, not a link: the
           card already sits on the topic's own page. -->
      <TopicDisplay
        :topic="topic"
        heading-level="h2"
        :title-link="false"
        digest-expanded
        :submit-edit="submitTopicEdit"
        @like="handleLike"
        @unlike="handleUnlike"
        @warn="handleWarn"
        @delete="handleDelete"
      />
      <ActionResult :result="likeResult" />

      <!-- Comments filter bar (below topic bubble, above pagination) -->
      <CommentsFilter class="topic-filter" />

      <router-view class="topic-comments" />
    </template>
  </template>

  <!-- Comment input area -->
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

      <!-- A refusal, so the plaque of one and not a grey line. No margin of
           its own: it stands flush with the comments, spaced by the
           wrapper. -->
      <RefusalNote v-else-if="topic?.isClosed" class="comment-closed-hint">
        Топик закрыт для комментариев
      </RefusalNote>
      <LoginPrompt v-else action="оставить комментарий" />
    </div>
  </div>

  <!-- Topic delete confirmation -->
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
