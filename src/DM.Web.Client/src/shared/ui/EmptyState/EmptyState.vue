<script setup lang="ts">
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
  </div>
</template>

<style scoped lang="sass">
.empty-state
  display: flex
  flex-direction: column
  align-items: flex-start
  justify-content: center
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
