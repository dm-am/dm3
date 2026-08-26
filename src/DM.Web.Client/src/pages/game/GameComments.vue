<script setup lang="ts">
// Game discussion sub-page (dev doc 4.2.3.5.7 "Обсуждение игры"). The section
// itself is the site-wide one (widgets/discussion); what stays here is the
// game's own part of it: who may read the discussion, who may write in it, and
// where the comments come from.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  CommentariesAccessMode,
  GameParticipation,
  gameApi,
  useGameDetailsStore,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { DiscussionSection } from "@/widgets/discussion";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import type { CommentsQuery } from "@/shared/api";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { user } = storeToRefs(useAuthStore());
const { game, comments, commentsPaging, commentsLoading, commentsError } =
  storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);

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

// Said instead of the composer when the game keeps the discussion readable but
// closed to new comments. A guest gets the sign-in prompt instead.
const closedHint = computed(() =>
  commentsAccessMode.value === CommentariesAccessMode.Readonly
    ? "Комментарии в этой игре доступны только для чтения"
    : undefined,
);

const load = (query: CommentsQuery) =>
  gameStore.loadComments(gameId.value, query);

const create = (text: string) =>
  gameApi.createGameComment(game.value?.id ?? gameId.value, { text });

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) => gameApi.getGameCommentForEdit(id);
const fetchQuoteSource = (id: string) => gameApi.getGameCommentQuote(id);

async function markAsRead() {
  if (!user.value) return;
  try {
    await gameApi.markCommentsAsRead(gameId.value);
  } catch {
    // Silently ignore - non-critical operation
  }
}
</script>

<template>
  <div class="game-comments">
    <SecondaryText v-if="!canViewComments" class="comments-private">
      Комментарии доступны только участникам игры
    </SecondaryText>

    <DiscussionSection
      v-else
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
      :fetch-quote-source="fetchQuoteSource"
      :paging-to="{
        name: 'game-comments',
        params: { id: game?.publicId || gameId },
      }"
      :draft-key="composerDraftKey('game', 'comment', game?.id)"
      :can-comment="canComment"
      :closed-hint="closedHint"
      @loaded="markAsRead"
    />
  </div>
</template>

<style scoped lang="sass">
.game-comments
  min-height: $grid-step * 50

.comments-private
  padding: $big
</style>
