<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, useUserStore } from "@/stores";
import { extractNumberParam } from "@/router";
import { useFetchData } from "@/composables/useFetchData";
import TheLoader from "@/components/TheLoader.vue";
import ThePaging from "@/components/ThePaging.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import TheComment from "@/components/comments/TheComment.vue";
import BBCodeEditor from "@/components/inputs/BBCodeEditor.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import gamingApi from "@/api/requests/gamingApi";
import { AccessPolicy, UserRole } from "@/api/models/community";
import { CommentariesAccessMode, GameParticipation } from "@/api/models/gaming";

const route = useRoute();
const router = useRouter();
const gameStore = useGameDetailsStore();
const { user } = storeToRefs(useUserStore());
const {
  game,
  comments,
  commentsPaging,
  commentsLoading,
  commentsError,
} = storeToRefs(gameStore);

const gameId = computed(() => route.params.id as string);
const currentPage = computed(() => extractNumberParam(route.params.n));

// Comment creation state
const newComment = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const isBanned = computed(() => {
  if (!user.value?.accessPolicy) return false;
  const policy = user.value.accessPolicy;
  return (
    policy === AccessPolicy.DemocraticBan || policy === AccessPolicy.FullBan
  );
});

const isModerator = computed(() => {
  if (!user.value) return false;
  return (
    user.value.roles?.some((r: UserRole) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r),
    ) ?? false
  );
});

const isParticipant = computed(() => {
  if (!game.value?.participation) return false;
  return (
    game.value.participation.includes(GameParticipation.Player) ||
    game.value.participation.includes(GameParticipation.Moderator) ||
    game.value.participation.includes(GameParticipation.Owner) ||
    game.value.participation.includes(GameParticipation.Reader)
  );
});

const commentsAccessMode = computed(() => game.value?.privacySettings?.commentariesAccess);

const canComment = computed(() => {
  if (!user.value || isBanned.value) return false;
  if (commentsAccessMode.value === CommentariesAccessMode.Readonly) return false;
  if (commentsAccessMode.value === CommentariesAccessMode.Private && !isParticipant.value) return false;
  return true;
});

const canViewComments = computed(() => {
  if (commentsAccessMode.value === CommentariesAccessMode.Public) return true;
  if (commentsAccessMode.value === CommentariesAccessMode.Readonly) return true;
  if (commentsAccessMode.value === CommentariesAccessMode.Private) return isParticipant.value;
  return true;
});

function handlePageChange(page: number) {
  router.push({
    name: "game-comments",
    params: {
      id: game.value?.id,
      n: page > 1 ? page : undefined,
    },
  });
}

async function handleSend() {
  if (!newComment.value.trim() || sending.value || !game.value) return;

  const text = newComment.value;
  newComment.value = "";
  editorRef.value?.clear();
  sending.value = true;

  try {
    await gamingApi.createGameComment(game.value.id, { text });
    // Reload comments
    await gameStore.loadComments(game.value.id, currentPage.value);
  } finally {
    sending.value = false;
  }
}

useFetchData(
  () => gameStore.loadComments(gameId.value, currentPage.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => gameStore.loadComments(id as string, 1),
    },
    {
      param: (p) => p.n,
      callback: (n) => gameStore.loadComments(gameId.value, extractNumberParam(n)),
    },
  ],
);
</script>

<template>
  <div class="game-comments">
    <!-- Access denied -->
    <div v-if="!canViewComments" class="comments-private">
      <secondary-text>Комментарии доступны только участникам игры</secondary-text>
    </div>

    <!-- Loading -->
    <the-loader v-else-if="commentsLoading" />

    <!-- Error -->
    <div v-else-if="commentsError" class="comments-error">
      {{ commentsError }}
    </div>

    <!-- Comments list -->
    <template v-else>
      <div v-if="comments.length === 0" class="comments-empty">
        <secondary-text>Пока нет комментариев</secondary-text>
      </div>

      <div v-else class="comments-list">
        <the-comment
          v-for="comment in comments"
          :key="comment.id"
          :comment="comment"
        />
      </div>

      <!-- Paging -->
      <the-paging
        v-if="commentsPaging && commentsPaging.pagesCount > 1"
        :current="commentsPaging.currentPage"
        :total="commentsPaging.pagesCount"
        @change="handlePageChange"
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
            <the-button
              :loading="sending"
              :disabled="!newComment.trim()"
              @click="handleSend"
            >
              Отправить
            </the-button>
          </template>
          <secondary-text v-else-if="isBanned" class="comment-hint">
            Вы не можете отправлять комментарии из-за ограничений аккаунта
          </secondary-text>
          <secondary-text v-else-if="commentsAccessMode === 'Readonly'" class="comment-hint">
            Комментарии в этой игре доступны только для чтения
          </secondary-text>
          <secondary-text v-else-if="!user" class="comment-hint">
            <router-link to="/login">Войдите</router-link>, чтобы оставить комментарий
          </secondary-text>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-comments
  min-height: $grid-step * 50

.comments-private,
.comments-error,
.comments-empty
  padding: $big
  text-align: center

.comments-error
  color: $accent-red

.comments-list
  display: flex
  flex-direction: column
  gap: $medium

.comment-input-wrapper
  margin-top: $large

.comment-input-container
  display: flex
  flex-direction: column
  gap: $small

  :deep(.bbcode-editor-wrapper)
    width: 100%

.comment-hint
  text-align: center
  padding: $small

  a
    color: $link
    &:hover
      text-decoration: underline
</style>
