<script setup lang="ts">
import type { GlobalChatMessage as GlobalChatMessageType } from "@/entities/global-chat";
import { ContentMessage } from "@/widgets/content-message";
import { useUiStore } from "@/shared/stores/ui";
import { storeToRefs } from "pinia";

const { isCompactMode } = storeToRefs(useUiStore());

defineProps<{
  message: GlobalChatMessageType;
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
    :compact="isCompactMode"
    @edit="handleEdit"
    @delete="handleDelete"
    @like="handleLike"
    @unlike="handleUnlike"
    @warn="handleWarn"
  />
</template>
