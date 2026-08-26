<script setup lang="ts">
/**
 * Unified block-level load-error box.
 *
 * It replaces the red error boxes that pages used to each carry a copy of.
 * A page that still has its own has not moved over yet: this component is the
 * destination, not a list of who uses it today.
 */
import { ref } from "vue";

const props = defineProps<{
  /** Error text, e.g. "Не удалось загрузить игры" */
  message: string;
  /** Optional retry handler — shows a "Повторить" button when provided */
  retry?: () => unknown;
}>();

const isRetrying = ref(false);

async function handleRetry() {
  if (!props.retry || isRetrying.value) return;
  isRetrying.value = true;
  try {
    await props.retry();
  } finally {
    isRetrying.value = false;
  }
}
</script>

<template>
  <div class="error-state" role="alert">
    <span class="error-message">{{ message }}</span>
    <button
      v-if="retry"
      type="button"
      class="error-retry"
      :disabled="isRetrying"
      @click="handleRetry"
    >
      Повторить
    </button>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.error-state
  display: flex
  align-items: center
  gap: $small
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

.error-message
  flex: 1

.error-retry
  +button
</style>
