<script setup lang="ts">
import { computed } from "vue";
import type { BlogStatus } from "../model/types";

const props = defineProps<{
  /** Blog status */
  status: BlogStatus | string;
}>();

// Status checks
const isDraft = computed(() => props.status === "Draft");
const isActive = computed(() => props.status === "Active");
const isClosed = computed(() => props.status === "Closed");

// Status display text (short form, unified with games)
const statusDisplay = computed<string>(() => {
  if (isDraft.value) return "Оформляется";
  if (isActive.value) return "Открыт";
  if (isClosed.value) return "Закрыт";
  return String(props.status);
});
</script>

<template>
  <span class="blog-status">{{ statusDisplay }}</span>
</template>

<style scoped lang="sass">
.blog-status
  display: inline
  word-wrap: break-word
</style>
