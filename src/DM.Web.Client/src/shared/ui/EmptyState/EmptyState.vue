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
import type { IconName } from "@/shared/lib/utils/icons";

defineProps<{
  /** Icon name to display (optional) */
  icon?: IconName;
  /** Main title text */
  title: string;
  /** Secondary hint text (optional) */
  hint?: string;
}>();
</script>

<style scoped lang="sass">
.empty-state
  display: flex
  flex-direction: column
  align-items: flex-start
  justify-content: center
  padding: $big * 2

.empty-icon
  width: 64px
  height: 64px
  color: $text-muted
  opacity: 0.5
  margin-bottom: $medium

.empty-title
  font-size: $font-size
  color: $text
  margin-bottom: $small

.empty-hint
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $medium
</style>
