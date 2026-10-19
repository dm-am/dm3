<script setup lang="ts">
/**
 * Loading twin of the tag picker (TagSelector): a heading over rows of tag
 * bubbles per group, as far apart as the picker's own. A row never wraps, so
 * the height is the rows asked for at any width.
 */
withDefaults(
  defineProps<{
    /** The rows of bubbles in each group, group by group */
    groups?: number[];
  }>(),
  { groups: () => [2, 1, 1] },
);

// Bubble widths in grid steps, as tag titles run; a row clips what it cannot hold.
const WIDTHS = [16, 24, 13, 30, 18, 22, 27, 14, 20, 25];

/** The bubbles of one row; the last row of a group stops short, as a wrap does. */
function bubbles(group: number, row: number, last: boolean): number[] {
  const start = (group * 3 + row) % WIDTHS.length;
  const count = last ? 2 + ((group + row) % 4) : WIDTHS.length;
  return Array.from(
    { length: count },
    (_, i) => WIDTHS[(start + i) % WIDTHS.length],
  );
}
</script>

<template>
  <div class="tags-skeleton" aria-hidden="true">
    <div v-for="(rows, group) in groups" :key="group" class="skeleton-group">
      <!-- A blank of the heading's font: the line is as tall as the heading's -->
      <div
        class="skeleton-title"
        :style="{ width: `${WIDTHS[group % WIDTHS.length] * 4 + 24}px` }"
      >
        &nbsp;
      </div>
      <div v-for="row in rows" :key="row" class="skeleton-row">
        <div
          v-for="(width, i) in bubbles(group, row, row === rows)"
          :key="i"
          class="skeleton-bubble"
          :style="{ width: `${width * 4}px` }"
        />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/FilterBubbles" as bubbles

.tags-skeleton
  display: flex
  flex-direction: column
  gap: $medium

.skeleton-group
  display: flex
  flex-direction: column
  gap: $small

.skeleton-title
  font-size: $secondary-font-size
  +skeleton-shimmer

.skeleton-row
  display: flex
  gap: $small
  overflow: hidden

.skeleton-bubble
  flex: none
  height: bubbles.$bubble-height
  +skeleton-shimmer
</style>
