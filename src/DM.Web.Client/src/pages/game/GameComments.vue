<script setup lang="ts">
// Game discussion sub-page (dev doc 4.2.3.5.7 "Обсуждение игры"). The section
// itself is the site-wide one (widgets/discussion); what stays here is the
// game's own part of it: who may write in it and where the comments come from.
// Who may read it is everybody who sees the game: no mode closes the reading.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  CommentariesAccessMode,
  gameApi,
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

// Any role in the game, which is what the server reads the read-only mode by
// (GameIntentionResolver.CreateComment: roles.HasAnyRole): the master, an
// assistant, the mentor, a player, a reader. The roles are the answer for
// whoever held the session when the game was read, so a viewer whose session
// has just ended has none, as in the game menu.
const hasRole = computed(
  () =>
    !!user.value && (isPlayer.value || isAssistant.value || isSubscribed.value),
);

const commentsAccessMode = computed(
  () => game.value?.privacySettings?.commentariesAccess,
);

// Who may write is the server's answer, asked by the code that refuses the
// comment (GameIntentionResolver.CreateComment): the mode with the roles, the
// game's blacklist and the ban at once. The page used to work it out from the
// first two and could not see the ban, so a banned reader was given the form
// and a refusal under it. The answer is for whoever held the session when the
// game was read; the shell reads the game again when that changes, and until
// then a viewer whose session has just ended is offered nothing.
const canComment = computed(
  () => !!user.value && !!game.value?.viewer?.canComment,
);

// The game's blacklist reaches the comments themselves, where it takes
// "Редактировать" off the viewer's own: the server refuses that edit by the
// list and the ban, not by the mode, so the flag above cannot stand for it.
const viewerBlacklisted = computed(
  () => !!user.value && !!game.value?.viewerBlacklisted,
);

// The plaque in place of the form when the discussion is closed to the
// viewer's comments; a guest gets it before the sign-in prompt. "Только для
// чтения" is read-only to those outside the game: everybody reads, the game's
// own people write, as the server takes a comment from anybody with a role in
// the game. Said in DM2's sense ("Для добавления сообщения Вы должны
// участвовать в этой игре"): who writes here. Read off the mode and the roles
// and not off canComment, which is false for every guest and until the game
// has answered. The mode goes first: it closes writing to everybody outside
// the game, blacklist or not.
const closedHint = computed(() => {
  if (
    commentsAccessMode.value === CommentariesAccessMode.Readonly &&
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
  gameApi.createGameComment(game.value?.id ?? gameId.value, { text });

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) => gameApi.getGameCommentForEdit(id);

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
