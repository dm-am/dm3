<script setup lang="ts">
// The game's part of the shared discussion: who writes, where comments live.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  CommentariesAccessMode,
  discussionApi,
  useGameDetailsStore,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { DiscussionSection } from "@/widgets/discussion";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import type { CommentsQuery } from "@/shared/api";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { user } = storeToRefs(useAuthStore());
const {
  game,
  comments,
  commentsPaging,
  commentsLoading,
  commentsError,
  isPlayer,
  isAssistant,
  isSubscribed,
} = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

// The read-only mode admits any role (GameIntentionResolver.CreateComment).
const hasRole = computed(
  () =>
    !!user.value && (isPlayer.value || isAssistant.value || isSubscribed.value),
);

// The server's answer: it alone sees the ban.
const canComment = computed(
  () => !!user.value && !!game.value?.viewer?.canComment,
);

// The blacklist also refuses editing one's own comments; the mode does not.
const viewerBlacklisted = computed(
  () => !!user.value && !!game.value?.viewerBlacklisted,
);

// Not off canComment, which is false for every guest: a guest is told too.
const closedHint = computed(() => {
  if (
    game.value?.privacySettings?.commentariesAccess ===
      CommentariesAccessMode.Readonly &&
    !hasRole.value
  )
    return "Писать в обсуждении могут только участники игры";
  if (viewerBlacklisted.value)
    return "Вы в черном списке этой игры: писать в обсуждении нельзя";
  return undefined;
});

const load = (query: CommentsQuery) =>
  gameStore.loadComments(gameId.value, query);

const create = (text: string) =>
  discussionApi.createGameComment(game.value?.id ?? gameId.value, { text });

const fetchEditSource = (id: string) => discussionApi.getGameCommentForEdit(id);

async function markAsRead() {
  if (!user.value) return;
  await discussionApi.markCommentsAsRead(gameId.value);
}
</script>

<template>
  <div class="game-comments">
    <DiscussionSection
      :comments="comments"
      :paging="commentsPaging"
      :loading="commentsLoading"
      :failed="commentsError"
      :record-id="gameId"
      :load="load"
      :create="create"
      :submit-edit="gameStore.updateComment"
      :submit-delete="gameStore.deleteComment"
      :like="gameStore.likeComment"
      :unlike="gameStore.unlikeComment"
      :fetch-edit-source="fetchEditSource"
      :paging-to="{
        name: 'game-comments',
        params: { id: game?.publicId || gameId },
      }"
      :draft-key="composerDraftKey('game', 'comment', game?.id)"
      :can-comment="canComment"
      :closed-hint="closedHint"
      :viewer-blacklisted="viewerBlacklisted"
      @loaded="markAsRead"
    />
  </div>
</template>

<style scoped lang="sass">
.game-comments
  min-height: $grid-step * 50
</style>
