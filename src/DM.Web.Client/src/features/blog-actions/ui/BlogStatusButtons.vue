<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import { StatusButtons } from "@/shared/ui/StatusButtons";
import {
  availableStatusTransitions,
  BLOG_STATUS_WORDS,
} from "@/features/game-actions/@x/blog-actions";

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const transitions = computed(() =>
  availableStatusTransitions(
    BLOG_STATUS_WORDS,
    blog.value?.status,
    blog.value?.closedReason,
  ),
);
</script>

<template>
  <StatusButtons
    :transitions="transitions"
    :apply="store.transitionStatus"
    subject="блога"
  />
</template>
