<template>
  <div class="empty-state">
    <SvgIcon v-if="icon" :name="icon" class="empty-icon" />
    <div class="empty-title">{{ title }}</div>
    <div v-if="hint" class="empty-hint">{{ hint }}</div>
    <slot name="action" />
  </div>
</template>

<script setup lang="ts">
import { SvgIcon } from "@/shared/ui/Icon";

defineProps<{
  /** Icon name to display (optional) */
  icon?: string;
  /** Main title text */
  title: string;
  /** Secondary hint text (optional) */
  hint?: string;
}>();
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.empty-state
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  padding: $big * 2
  text-align: center

.empty-icon
  width: 64px
  height: 64px
  color: $text-muted
  opacity: 0.5
  margin-bottom: $medium

.empty-title
  font-size: $title-font-size
  font-weight: 500
  color: $text
  margin-bottom: $small

.empty-hint
  color: $text-muted
  margin-bottom: $medium
</style>
