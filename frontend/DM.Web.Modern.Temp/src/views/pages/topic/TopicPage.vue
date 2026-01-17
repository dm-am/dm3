<script setup lang="ts">
import { IconType } from "@/components/icons/iconType";
import { useRoute } from "vue-router";
import { useBoardsStore, useUserStore } from "@/stores";
import { extractNumberParam } from "@/router";
import { storeToRefs } from "pinia";
import TopicOpening from "@/components/content/TopicOpening.vue";
import type { TopicId } from "@/api/models/forum";
import { useFetchData } from "@/composables/useFetchData";
import forumApi from "@/api/requests/forumApi";

const route = useRoute();
const boardsStore = useBoardsStore();
const { trySelectTopic, fetchComments } = boardsStore;
const { selectedTopic: topic } = storeToRefs(boardsStore);
const { user } = storeToRefs(useUserStore());

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

function handleWarn(id: string) {
  // TODO: Open warning modal
  console.log("Warn topic:", id);
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
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.topic-header
  display: flex
  justify-content: space-between
  align-items: baseline
</style>
