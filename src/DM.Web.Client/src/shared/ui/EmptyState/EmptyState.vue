<script setup lang="ts">
/**
 * EmptyState — "there is nothing here", written the way every other paragraph
 * of the site is written: from the left edge, in words.
 *
 * No icon. It used to take one and draw it at 64px above the sentence, and a
 * 64px glyph over "Нет активных сессий" reports nothing the sentence does not
 * already say while taking the height of three lines to say it.
 */
defineProps<{
  /** Main title text */
  title: string;
  /** Secondary hint text (optional) */
  hint?: string;
}>();
</script>

<template>
  <div class="empty-state">
    <div class="empty-title">{{ title }}</div>
    <div v-if="hint" class="empty-hint">{{ hint }}</div>
    <slot name="action" />
  </div>
</template>

<style scoped lang="sass">
.empty-state
  display: flex
  flex-direction: column
  align-items: flex-start
  justify-content: center
  // Sized for text alone: the 64px icon this room was made for is gone.
  padding: $big

.empty-title
  font-size: $font-size
  color: $text
  margin-bottom: $small

.empty-hint
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $medium
</style>
