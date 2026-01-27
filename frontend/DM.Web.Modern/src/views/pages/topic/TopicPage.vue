<script setup lang="ts">
import { ref, computed } from "vue";
import { IconType } from "@/components/icons/iconType";
import { useRoute } from "vue-router";
import { useBoardsStore, useUserStore } from "@/stores";
import { extractNumberParam } from "@/router";
import { storeToRefs } from "pinia";
import TopicOpening from "@/components/content/TopicOpening.vue";
import BBCodeEditor from "@/components/inputs/BBCodeEditor.vue";
import type { TopicId } from "@/api/models/forum";
import { useFetchData } from "@/composables/useFetchData";
import forumApi from "@/api/requests/forumApi";
import { AccessPolicy, UserRole } from "@/api/models/community";

const route = useRoute();
const boardsStore = useBoardsStore();
const { trySelectTopic, fetchComments, createComment } = boardsStore;
const { selectedTopic: topic } = storeToRefs(boardsStore);
const { user } = storeToRefs(useUserStore());

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

const canComment = computed(() => user.value && !isBanned.value && topic.value && !topic.value.isClosed);

async function handleSend() {
  if (!newComment.value.trim() || sending.value) return;
  const text = newComment.value;
  newComment.value = "";
  editorRef.value?.clear();
  sending.value = true;
  await createComment(text);
  sending.value = false;
}

async function markAsReadIfNeeded(topicId: TopicId) {
  if (!user.value) return;
  if (!topic.value?.unreadCommentsCount) return;

  await forumApi.markTopicAsRead(topicId);
  // Update local state
  if (topic.value) {
    (topic.value as any).unreadCommentsCount = 0;
  }
}

async function fetchData() {
  const topicId = route.params.id as TopicId;
  await trySelectTopic(topicId);
  await fetchComments(extractNumberParam(route.params.n));
  // Mark as read after loading
  markAsReadIfNeeded(topicId);
}

useFetchData(
  () => fetchData(),
  [
    {
      param: (p) => p.id,
      callback: () => fetchData(),
    },
    {
      param: (p) => p.n,
      callback: (n) => fetchComments(extractNumberParam(n)),
    },
  ],
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
      <page-title>{{ topic.title }}</page-title>
      <router-link :to="{ name: 'forum', params: { id: topic.board.id } }">
        <the-icon :font="IconType.ArrowLeft" />
        Назад на форум "{{ topic.board.id }}"
      </router-link>
    </div>
    <topic-opening
      :topic="topic"
      @like="handleLike"
      @unlike="handleUnlike"
      @warn="handleWarn"
    />
  </template>
  <the-loader v-else :big="true" />
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
        Тема закрыта для комментариев
      </secondary-text>
      <secondary-text v-else class="comment-login-hint">
        <router-link to="/login">Войдите</router-link>, чтобы оставить комментарий
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

.comment-input-wrapper
  margin-top: $large

.comment-input-container
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

.comment-send-button
  +button()
  align-self: flex-start

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
