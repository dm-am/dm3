<script setup lang="ts">
/** The placeholder of a sidebar list: a row as tall as a real one, so nothing shifts. */
withDefaults(
  defineProps<{
    /** The rows the real list is expected to have. */
    lines?: number;
  }>(),
  { lines: 3 },
);

defineOptions({ inheritAttrs: false });

// Ragged widths, as real titles are.
function getWidth(index: number): string {
  const widths = ["85%", "70%", "90%", "60%", "75%", "80%", "65%"];
  return widths[(index - 1) % widths.length];
}
</script>

<template>
  <li class="sidebar-skeleton" aria-hidden="true">
    <div v-for="i in lines" :key="i" class="skeleton-row">
      <div class="skeleton-line" :style="{ width: getWidth(i) }" />
    </div>
  </li>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.sidebar-skeleton
  display: block

// One row of the real list, at the body's line height.
.skeleton-row
  line-height: 1.3
  font-size: $font-size
  height: 1.3em
  display: flex
  align-items: center

.skeleton-line
  height: 12px
  max-width: 100%
  +skeleton-shimmer
</style>
