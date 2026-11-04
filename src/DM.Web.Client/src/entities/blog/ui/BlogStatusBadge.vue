<script setup lang="ts">
import { computed } from "vue";
import type { BlogStatus } from "../model/types";

const props = defineProps<{
  status: BlogStatus;
}>();

const LABELS: Record<BlogStatus, string> = {
  Draft: "Оформляется",
  Active: "Открыт",
  Closed: "Закрыт",
};

// A status the server added before this client knew it is shown as it came.
const statusDisplay = computed(() => LABELS[props.status] ?? props.status);
</script>

<template>
  <span class="blog-status">{{ statusDisplay }}</span>
</template>

<style scoped lang="sass">
.blog-status
  overflow-wrap: break-word
</style>
