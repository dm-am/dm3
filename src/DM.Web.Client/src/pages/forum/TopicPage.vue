<script setup lang="ts">
import { ref, computed } from "vue";
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { useUserStore } from "@/entities/user";
import { storeToRefs } from "pinia";
import { Topic as TopicDisplay } from "@/features/topic";
import { BBCodeEditor } from "@/features/editor";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { forumApi } from "@/entities/forum";
import { AccessPolicy, UserRole } from "@/shared/api/models/community";
import { CommentsFilter, useCommentsFilter } from "@/features/comment-filter";

const route = useRoute();
const boardsStore = useBoardsStore();
const { trySelectTopicByNumber, searchComments, createComment } = boardsStore;
const { selectedTopic: topic } = storeToRefs(boardsStore);
const { user } = storeToRefs(useUserStore());

// Filter setup - get search params from URL
const { searchParams } = useCommentsFilter();

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
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(
        r,
      ),
    ) ?? false
  );
});

const canComment = computed(
  () => user.value && !isBanned.value && topic.value && !topic.value.isClosed,
);

async function handleSend() {
  if (!newComment.value.trim() || sending.value) return;
  const text = newComment.value;
  newComment.value = "";
  editorRef.value?.clear();
  sending.value = true;
  await createComment(text);
  sending.value = false;
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

async function fetchData() {
  const alias = route.params.alias as string;
  const num = parseInt(route.params.num as string);
  await trySelectTopicByNumber(alias, num);
  await searchComments(searchParams.value);
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

function handleWarn(_id: string) {
  // TODO: Open warning modal (P5.5 - console.log removed)
}
</script>

<template>
  <template v-if="topic">
    <div class="topic-header">
      <router-link :to="{ name: 'forum', params: { alias: topic.board.alias } }">
        Назад к разделу "{{ topic.board.title }}"
      </router-link>
    </div>

    <TopicDisplay
      :topic="topic"
      @like="handleLike"
      @unlike="handleUnlike"
      @warn="handleWarn"
    />

    <!-- Comments filter bar (below topic bubble, above pagination) -->
    <CommentsFilter class="topic-filter" />
  </template>
  <router-view />

  <!-- Comment input area -->
  <div v-if="topic" class="comment-input-wrapper">
    <div class="comment-input-container">
      <template v-if="canComment">
        <BBCodeEditor
          ref="editorRef"
          v-model="newComment"
          context="common"
          placeholder="Написать комментарий..."
          :draft-key="`topic_${topic?.id}`"
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
      <secondary-text v-else-if="isBanned" class="comment-banned-hint">
        Вы не можете отправлять комментарии из-за ограничений аккаунта
      </secondary-text>
      <secondary-text v-else-if="topic?.isClosed" class="comment-closed-hint">
        Топик закрыт для комментариев
      </secondary-text>
      <secondary-text v-else class="comment-login-hint">
        <router-link to="/?action=login">Войдите</router-link>, чтобы оставить
        комментарий
      </secondary-text>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.topic-header
  display: flex
  justify-content: space-between
  align-items: baseline
  margin-bottom: $small

.topic-filter
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
  +primary-button

.comment-login-hint,
.comment-banned-hint,
.comment-closed-hint
  text-align: center
  padding: $small
  a
    color: $link
    &:hover
      text-decoration: underline

.comment-banned-hint
  color: $accent-red
</style>
