<script setup lang="ts">
import { ref, computed, reactive, inject, onBeforeUnmount } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import { useBoardsStore } from "@/entities/forum";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { storeToRefs } from "pinia";
import { TopicView as TopicDisplay } from "@/features/topic";
import { LoginPrompt } from "@/features/auth";
import { WarningDialog } from "@/features/moderation-actions";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { forumApi } from "@/entities/forum";
import { CommentsFilter, useCommentsFilter } from "@/features/comment-filter";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { errorCodeForStatus } from "@/shared/ui/ErrorPage";
import { reportForumShellError } from "./forumShell";
import { notifyFailure } from "@/shared/lib/errors";
import { permalinkOrigin } from "@/shared/config/site";

const route = useRoute();
const router = useRouter();
const boardsStore = useBoardsStore();
const { trySelectTopicByNumber, searchComments, createComment } = boardsStore;
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

const canComment = computed(
  () => user.value && topic.value && !topic.value.isClosed,
);

async function handleSend() {
  if (!newComment.value.trim() || sending.value) return;
  const text = newComment.value;
  newComment.value = "";
  sending.value = true;
  const result = await createComment(text);
  sending.value = false;
  const failed = Boolean(result?.error);
  // Give the text back on failure. Emptying the field before the request is
  // what makes sending feel instant; losing what was written when it fails is
  // not part of that bargain. The editor's own clear() waits for the send to
  // land — it also drops the saved draft, and that copy is the one that
  // outlives the tab.
  if (failed) {
    newComment.value = text;
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

async function handleLike(id: string) {
  await boardsStore.likeTopic(id);
}

async function handleUnlike(id: string) {
  await boardsStore.unlikeTopic(id);
}

const toast = useToast();

// --- Topic lifecycle: edit / close / delete (doc 4.2.2.17 / 4.2.3.7.5) ---
async function handleSaveEdit(
  id: string,
  patch: { title: string; description: string },
) {
  const { error } = await boardsStore.updateTopicContent(id, patch);
  if (error) notifyFailure(error, "Не удалось сохранить топик");
}

async function handleToggleClose(id: string) {
  const closing = !topic.value?.isClosed;
  const { error } = await boardsStore.setTopicClosed(id, closing);
  if (error) notifyFailure(error, "Не удалось изменить статус топика");
}

const showDeleteConfirm = ref(false);
const deletingTopic = ref(false);

function handleDelete() {
  showDeleteConfirm.value = true;
}

async function confirmDeleteTopic() {
  const id = topic.value?.id;
  if (!id || deletingTopic.value) return;
  deletingTopic.value = true;
  const { error } = await boardsStore.deleteTopic(id);
  deletingTopic.value = false;
  showDeleteConfirm.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить топик");
    return;
  }
  toast.success("Топик удален");
  router.push({ name: "forum", params: { alias: route.params.alias } });
}

// --- Moderator warning (doc 4.2.4.1) ---
// The warn button itself is gated to Moderator+ inside <Topic> (can-warn).
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
        @like="handleLike"
        @unlike="handleUnlike"
        @warn="handleWarn"
        @save-edit="handleSaveEdit"
        @delete="handleDelete"
        @toggle-close="handleToggleClose"
      />

      <!-- Comments filter bar (below topic bubble, above pagination) -->
      <CommentsFilter class="topic-filter" />

      <router-view class="topic-comments" />
    </template>
  </template>

  <!-- Comment input area -->
  <div v-if="topic && !errorCode" class="comment-input-wrapper">
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
          :resizable="true"
          :is-moderator="isModerator"
          @submit="handleSend"
        />
        <button
          class="comment-send-button"
          :disabled="sending || !newComment.trim()"
          @click="handleSend"
        >
          Отправить
        </button>
      </template>

      <secondary-text v-else-if="topic?.isClosed" class="comment-closed-hint">
        Топик закрыт для комментариев
      </secondary-text>
      <LoginPrompt v-else action="оставить комментарий" />
    </div>
  </div>

  <!-- Topic delete confirmation -->
  <ConfirmDialog
    :show="showDeleteConfirm"
    title="Удалить топик?"
    message="Топик и все его комментарии будут удалены. Это действие необратимо."
    confirm-label="Удалить"
    danger
    :loading="deletingTopic"
    @confirm="confirmDeleteTopic"
    @update:show="showDeleteConfirm = $event"
  />
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

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
  +button

.comment-closed-hint
  padding: $small
</style>
