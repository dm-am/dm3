<script setup lang="ts">
import { computed } from "vue";
import type { Comment } from "@/api/models/forum";
import ContentMessage from "@/components/content/ContentMessage.vue";

const props = defineProps<{
  comment: Comment;
}>();

const emit = defineEmits<{
  edit: [id: string, text: string];
  delete: [id: string];
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
}>();

// Transform Comment to ContentMessage format
const messageData = computed(() => ({
  id: props.comment.id,
  createdUtc: props.comment.createdUtc,
  modifiedUtc: props.comment.updatedUtc ?? null,
  author: props.comment.author,
  text: props.comment.text,
  isRemoved: props.comment.isRemoved ?? false,
  likes: props.comment.likes ?? [],
}));

function handleEdit(id: string, text: string) {
  emit("edit", id, text);
}

function handleDelete(id: string) {
  emit("delete", id);
}

function handleLike(id: string) {
  emit("like", id);
}

function handleUnlike(id: string) {
  emit("unlike", id);
}

function handleWarn(id: string) {
  emit("warn", id);
}
</script>

<template>
  <content-message
    :message="messageData"
    :is-public="true"
    @edit="handleEdit"
    @delete="handleDelete"
    @like="handleLike"
    @unlike="handleUnlike"
    @warn="handleWarn"
  />
</template>
