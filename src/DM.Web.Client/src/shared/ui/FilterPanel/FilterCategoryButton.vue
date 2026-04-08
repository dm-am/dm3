<script setup lang="ts">
defineProps<{
  /** Button label */
  label: string;
  /** Whether the panel is expanded */
  expanded?: boolean;
  /** Count of included filters */
  includeCount?: number;
  /** Count of excluded filters */
  excludeCount?: number;
}>();

const emit = defineEmits<{
  toggle: [];
}>();
</script>

<template>
  <button
    type="button"
    class="filter-category-button"
    :class="{ expanded }"
    @click="emit('toggle')"
  >
    {{ label }}
    <span v-if="includeCount && includeCount > 0" class="count-badge include"
      >+{{ includeCount }}</span
    >
    <span v-if="excludeCount && excludeCount > 0" class="count-badge exclude"
      >-{{ excludeCount }}</span
    >
  </button>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.filter-category-button
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $minor $small
  font-size: $secondary-font-size
  font-family: "PT Sans", sans-serif
  cursor: pointer
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  color: $text
  transition: all 0.15s

  &:hover
    background-color: $bg-element-accent

  &.expanded
    background-color: $bg-element-accent

.count-badge
  display: inline-flex
  align-items: center
  justify-content: center
  min-width: 18px
  height: 16px
  padding: 0 $minor
  font-size: 11px
  font-weight: bold
  border-radius: 8px

  &.include
    background-color: $accent-green
    color: $bg-element

  &.exclude
    background-color: $accent-red
    color: $bg-element
</style>
