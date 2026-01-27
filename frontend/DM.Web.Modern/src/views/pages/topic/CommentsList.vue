<script setup lang="ts">
import { useBoardsStore } from "@/stores";
import { storeToRefs } from "pinia";
import ThePaging from "@/components/ThePaging.vue";
import { useRoute } from "vue-router";
import TheComment from "@/components/comments/TheComment.vue";

const route = useRoute();
const boardsStore = useBoardsStore();
const { comments } = storeToRefs(boardsStore);

async function handleEdit(id: string, text: string) {
  await boardsStore.updateComment(id, text);
}

async function handleDelete(id: string) {
  await boardsStore.deleteComment(id);
}

async function handleLike(id: string) {
  await boardsStore.likeComment(id);
}

async function handleUnlike(id: string) {
  await boardsStore.unlikeComment(id);
}

function handleWarn(_id: string) {
  // TODO: Open warning modal (P5.5 - console.log removed)
}
</script>

<template>
  <the-paging
    v-if="comments"
    :paging="comments.paging!"
    :to="{ name: 'topic', params: route.params }"
  />
  <the-loader v-if="!comments" :big="true" />
  <secondary-text v-else-if="!comments.resources.length" class="comments-none">
    Комментариев пока нет...
  </secondary-text>
  <the-comment
    v-else
    v-for="comment in comments.resources"
    :key="comment.id"
    :comment="comment"
    @edit="handleEdit"
    @delete="handleDelete"
    @like="handleLike"
    @unlike="handleUnlike"
    @warn="handleWarn"
  />
</template>

<style scoped lang="sass">
.comments-none
  text-align: center
</style>
