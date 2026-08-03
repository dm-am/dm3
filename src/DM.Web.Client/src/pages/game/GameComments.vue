<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useScrollToElement } from "@/shared/lib/composables/useScrollToElement";
import Paging from "@/shared/ui/Paging/Paging.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { CommentItem, useCommentWarnDialog } from "@/features/comment";
import { LoginPrompt } from "@/features/auth";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";
import { gameApi } from "@/entities/game";
import { CommentariesAccessMode, GameParticipation } from "@/entities/game";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { user } = storeToRefs(useAuthStore());
const { isCompactLayout } = storeToRefs(useUiStore());
const { game, comments, commentsPaging, commentsLoading, commentsError } =
  storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);
// The shared Paging widget writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
function getPage(): number {
  const page = route.query.number;
  return page ? parseInt(page as string) || 1 : 1;
}

// Calculate comment number based on paging
function getCommentNumber(index: number): number {
  if (!commentsPaging.value) return index + 1;
  const offset = (commentsPaging.value.current - 1) * commentsPaging.value.size;
  return offset + index + 1;
}

// --- Single-comment actions (edit / delete / likes / warn) ---
// The item shows the refusal and keeps its editor open, so these hand the
// store's answer straight back to it.
function handleEdit(id: string, text: string) {
  return gameStore.updateComment(id, text);
}

function handleDelete(id: string) {
  return gameStore.deleteComment(id);
}

async function handleLike(id: string) {
  await gameStore.likeComment(id);
}

async function handleUnlike(id: string) {
  await gameStore.unlikeComment(id);
}

// Moderator warning (doc 4.2.4.1) — shared dialog wiring.
const { warnComment: handleWarn } = useCommentWarnDialog((id) =>
  comments.value.find((c) => c.id === id),
);

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) => gameApi.getGameCommentForEdit(id);

// Paging scrolls the comments block back into view (not the page top)
const commentsSectionRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return commentsSectionRef.value;
}

// Comment creation state
const newComment = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const isModerator = computed(() => userIsModerator(user.value));

const isParticipant = computed(() => {
  if (!game.value?.participation) return false;
  return (
    game.value.participation.includes(GameParticipation.Player) ||
    game.value.participation.includes(GameParticipation.Moderator) ||
    game.value.participation.includes(GameParticipation.Owner) ||
    game.value.participation.includes(GameParticipation.Reader)
  );
});

const commentsAccessMode = computed(
  () => game.value?.privacySettings?.commentariesAccess,
);

const canComment = computed(() => {
  if (!user.value) return false;
  if (commentsAccessMode.value === CommentariesAccessMode.Readonly)
    return false;
  if (
    commentsAccessMode.value === CommentariesAccessMode.Private &&
    !isParticipant.value
  )
    return false;
  return true;
});

const canViewComments = computed(() => {
  if (commentsAccessMode.value === CommentariesAccessMode.Public) return true;
  if (commentsAccessMode.value === CommentariesAccessMode.Readonly) return true;
  if (commentsAccessMode.value === CommentariesAccessMode.Private)
    return isParticipant.value;
  return true;
});

// Scroll to target element when comments are loaded
const commentsLoaded = computed(
  () => comments.value.length > 0 && !commentsLoading.value,
);
useScrollToElement(commentsLoaded);

// Mark comments as read when loaded (for authenticated users)
watch(
  commentsLoaded,
  async (loaded) => {
    if (loaded && user.value && gameId.value) {
      try {
        await gameApi.markCommentsAsRead(gameId.value);
      } catch {
        // Silently ignore - non-critical operation
      }
    }
  },
  { once: true },
);

async function handleSend() {
  if (!newComment.value.trim() || sending.value || !game.value) return;

  const text = newComment.value;
  newComment.value = "";
  sending.value = true;

  let failed = false;
  try {
    const { error } = await gameApi.createGameComment(game.value.id, { text });
    failed = Boolean(error);
    if (!failed) {
      // Reload comments
      await gameStore.loadComments(game.value.id, getPage());
    }
  } catch {
    failed = true;
  } finally {
    sending.value = false;
  }
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

useFetchData(
  () => gameStore.loadComments(gameId.value, getPage()),
  [
    {
      param: (p) => p.id,
      callback: (id) => gameStore.loadComments(id as string, 1),
    },
  ],
  [
    {
      query: (q) => q.number,
      callback: () => gameStore.loadComments(gameId.value, getPage()),
    },
  ],
);
</script>

<template>
  <div class="game-comments">
    <!-- Access denied -->
    <div v-if="!canViewComments" class="comments-private">
      <secondary-text
        >Комментарии доступны только участникам игры</secondary-text
      >
    </div>

    <!-- Error -->
    <div v-else-if="commentsError" class="comments-error">
      {{ commentsError }}
    </div>

    <!-- Comments list -->
    <template v-else>
      <!-- Loading -->
      <CommentSkeleton v-if="commentsLoading && comments.length === 0" />

      <div v-else-if="comments.length === 0" class="comments-empty">
        <secondary-text>Пока нет комментариев</secondary-text>
      </div>

      <div v-else ref="commentsSectionRef" class="comments-section">
        <div class="comments-list">
          <CommentItem
            v-for="(comment, index) in comments"
            :key="comment.id"
            :comment="comment"
            :compact="isCompactLayout"
            :number="getCommentNumber(index)"
            :data-id="comment.id"
            :fetch-edit-source="fetchEditSource"
            :submit-edit="handleEdit"
            :submit-delete="handleDelete"
            @like="handleLike"
            @unlike="handleUnlike"
            @warn="handleWarn"
          />
        </div>
      </div>

      <!-- Paging -->
      <Paging
        v-if="commentsPaging"
        :paging="commentsPaging"
        :to="{
          name: 'game-comments',
          params: { id: game?.publicId || game?.id },
        }"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />

      <!-- Comment input -->
      <div class="comment-input-wrapper">
        <div class="comment-input-container">
          <template v-if="canComment">
            <BBCodeEditor
              ref="editorRef"
              v-model="newComment"
              context="common"
              placeholder="Написать комментарий..."
              :draft-key="`game_${game?.id}_comment`"
              :disabled="sending"
              :min-height="100"
              :max-height="300"
              :resizable="true"
              :is-moderator="isModerator"
              @submit="handleSend"
            />
            <Button
              :loading="sending"
              :disabled="!newComment.trim()"
              @click="handleSend"
            >
              Отправить
            </Button>
          </template>

          <secondary-text
            v-else-if="commentsAccessMode === 'Readonly'"
            class="comment-hint"
          >
            Комментарии в этой игре доступны только для чтения
          </secondary-text>
          <LoginPrompt v-else-if="!user" action="оставить комментарий" />
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
.game-comments
  min-height: $grid-step * 50

.comments-private,
.comments-error,
.comments-empty
  padding: $big

.comments-error
  color: $accent-red

.comments-section
  display: flex
  flex-direction: column
  gap: $small

.comments-list
  display: flex
  flex-direction: column

.comment-input-wrapper
  margin-top: $medium

.comment-input-container
  display: flex
  flex-direction: column
  gap: $small

  :deep(.bbcode-editor-wrapper)
    width: 100%

.comment-hint
  padding: $small
</style>
