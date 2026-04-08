<template>
  <div class="sidebar-skeleton">
    <div v-for="i in lines" :key="i" class="skeleton-line" :style="{ width: getWidth(i) }" />
  </div>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    lines?: number;
  }>(),
  { lines: 3 },
);

// Vary line widths for natural appearance
function getWidth(index: number): string {
  const widths = ["85%", "70%", "90%", "60%", "75%"];
  return widths[(index - 1) % widths.length];
}
</script>

<script lang="ts">
export default {
  inheritAttrs: false,
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.sidebar-skeleton
  display: flex
  flex-direction: column
  gap: $small

.skeleton-line
  height: 16px
  background: linear-gradient(90deg, $bg-element-hover 25%, $bg-element 50%, $bg-element-hover 75%)
  background-size: 200% 100%
  animation: shimmer 1.5s infinite
  border-radius: 4px

@keyframes shimmer
  0%
    background-position: 200% 0
  100%
    background-position: -200% 0
</style>
