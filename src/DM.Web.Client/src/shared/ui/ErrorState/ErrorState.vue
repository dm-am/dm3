<script setup lang="ts">
/**
 * Unified block-level load-error box.
 *
 * Matches the red error boxes duplicated across the app (e.g.
 * widgets/games-table GamesDataTable .error-message,
 * pages/global-chat GlobalChatPage .globalChat-error / .globalChat-retry).
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
@import "src/assets/styles/Inputs"

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
