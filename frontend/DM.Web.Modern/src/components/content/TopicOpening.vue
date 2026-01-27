<script setup lang="ts">
import { computed } from "vue";
import type { Topic } from "@/api/models/forum";
import ContentMessage from "@/components/content/ContentMessage.vue";

const props = defineProps<{
  topic: Topic;
}>();

const emit = defineEmits<{
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
}>();

// Transform Topic to ContentMessage format
const messageData = computed(() => ({
  id: props.topic.id,
  createdUtc: props.topic.createdUtc,
  modifiedUtc: props.topic.editedUtc ?? null,
  author: props.topic.author,
  text: props.topic.description,
  isRemoved: false,
  likes: props.topic.likes ?? [],
}));

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
  <div class="topic-opening">
    <content-message
      :message="messageData"
      :is-public="true"
      @like="handleLike"
      @unlike="handleUnlike"
      @warn="handleWarn"
    />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.topic-opening
  margin-bottom: $big
  border-bottom: 2px solid $border
  padding-bottom: $medium
</style>
