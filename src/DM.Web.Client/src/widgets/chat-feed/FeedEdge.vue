<script setup lang="ts">
// The sentinel of an edge of the window, with "Повторить": watching it would retry on every scroll.
defineProps<{
  /** A page past this edge did not load. */
  error: string | null;
}>();

const emit = defineEmits<{
  /** "Повторить" was pressed. */
  retry: [];
}>();
</script>

<template>
  <div class="chat-feed__sentinel">
    <secondary-text v-if="error" class="chat-feed__sentinel-error">
      {{ error }}
      <button type="button" class="chat-feed__retry" @click="emit('retry')">
        Повторить
      </button>
    </secondary-text>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

.chat-feed__sentinel
  display: flex
  justify-content: center
  align-items: center
  width: 100%
  min-height: 1px
  // Grows to fit the retry; otherwise a hairline the observer watches.
  &:has(.chat-feed__sentinel-error)
    min-height: 30px
    padding: $small 0

.chat-feed__sentinel-error
  display: flex
  align-items: center
  gap: $small
  color: $accent-red

.chat-feed__retry
  flex-shrink: 0
  +button
</style>
