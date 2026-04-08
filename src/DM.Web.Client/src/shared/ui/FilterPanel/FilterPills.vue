<script setup lang="ts">
import type { ActiveFilter } from "./types";

defineProps<{
  /** List of active filters */
  filters: ActiveFilter[];
}>();

const emit = defineEmits<{
  /** Emitted when filter is removed */
  remove: [key: string];
  /** Emitted when all filters are cleared */
  clear: [];
}>();
</script>

<template>
  <div v-if="filters.length > 0" class="filter-pills">
    <span class="pills-label">Фильтры:</span>
    <span
      v-for="filter in filters"
      :key="filter.key"
      class="pill"
      :class="filter.state"
    >
      <span class="state-prefix">{{
        filter.state === "include" ? "+" : "-"
      }}</span>
      {{ filter.label }}
      <button
        type="button"
        class="remove-btn"
        @click="emit('remove', filter.key)"
        aria-label="Удалить фильтр"
      >
        ×
      </button>
    </span>
    <span class="clear-link" @click="emit('clear')">Сбросить</span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.filter-pills
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  padding: $small 0

.pills-label
  color: $text-muted
  font-size: $secondary-font-size

.pill
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $minor $small
  font-size: $secondary-font-size
  border-radius: $border-radius
  background-color: $bg-element-overlay

  &.include
    .state-prefix
      color: $accent-green

  &.exclude
    .state-prefix
      color: $accent-red

.state-prefix
  font-weight: bold

.remove-btn
  display: inline-flex
  align-items: center
  justify-content: center
  width: 16px
  height: 16px
  margin-left: $tiny
  padding: 0
  font-size: 14px
  line-height: 1
  cursor: pointer
  border: none
  background: none
  color: $text-muted
  border-radius: 50%

  &:hover
    background-color: $accent-red-muted
    color: $accent-red

.clear-link
  font-size: $secondary-font-size
  cursor: pointer
  color: $link

  &:hover
    color: $link-hover
    text-decoration: underline
</style>
