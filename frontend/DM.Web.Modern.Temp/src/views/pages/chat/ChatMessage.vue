<script setup lang="ts">
import type { ChatMessage as ChatMessageType } from "@/api/models/chat";
import ContentMessage from "@/components/content/ContentMessage.vue";

const props = defineProps<{
  message: ChatMessageType;
}>();

const emit = defineEmits<{
  edit: [id: string, text: string];
  delete: [id: string];
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
}>();

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
    :message="message"
    :is-public="true"
    :compact="true"
    @edit="handleEdit"
    @delete="handleDelete"
    @like="handleLike"
    @unlike="handleUnlike"
    @warn="handleWarn"
  />
</template>
