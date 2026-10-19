<script setup lang="ts">
/**
 * Loading twin of the sheet of a character as it is read (CharacterForm in
 * view mode): a caption over a value per attribute, the rows as far apart as
 * the sheet's own.
 */
withDefaults(
  defineProps<{
    /** Number of attribute rows to show */
    rows?: number;
  }>(),
  { rows: 4 },
);
</script>

<template>
  <div class="sheet-skeleton" aria-hidden="true">
    <div v-for="i in rows" :key="i" class="skeleton-row">
      <div class="skeleton-label" />
      <div class="skeleton-value" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.sheet-skeleton
  display: flex
  flex-direction: column
  gap: $medium

.skeleton-row
  display: flex
  flex-direction: column
  gap: $minor

.skeleton-label
  width: $grid-step * 25
  height: $grid-step * 3
  +skeleton-shimmer

.skeleton-value
  width: 80%
  height: $grid-step * 4
  +skeleton-shimmer
</style>
