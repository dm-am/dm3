<script setup lang="ts">
/**
 * The placeholder of PollCards of the sidebar, row for row and on their tokens:
 * a list of polls is far taller than a list of titles, and a short placeholder
 * would drop the blocks below it when the polls arrive.
 */
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";

withDefaults(
  defineProps<{
    /** The cards the list is expected to have, each in its own item. */
    count?: number;
  }>(),
  { count: 1 },
);

// A typical poll of the column; a poll with more or fewer shifts by the difference.
const OPTION_WIDTHS = ["65%", "45%", "75%", "55%"];
</script>

<template>
  <li v-for="n in count" :key="n" class="poll-skeleton" aria-hidden="true">
    <div class="skeleton-row title">
      <div class="skeleton-line" style="width: 80%" />
    </div>
    <div class="skeleton-row status">
      <div class="skeleton-line" style="width: 55%" />
    </div>
    <!-- The bar itself, so its margins, padding and overlay are the real ones. -->
    <ProgressBar
      v-for="(width, index) in OPTION_WIDTHS"
      :key="index"
      :current="0"
      :goal="1"
    >
      <div class="skeleton-row">
        <div class="skeleton-line" :style="{ width }" />
      </div>
    </ProgressBar>
    <div class="skeleton-row type">
      <div class="skeleton-line" style="width: 35%" />
    </div>
  </li>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

// .poll of PollCard: its first-child rule always holds, as each card is alone in its <li>.
.poll-skeleton
  display: block
  margin: 0 0 $small

// One line of text at the body's line height, as SidebarSkeleton draws it.
.skeleton-row
  line-height: 1.3
  font-size: $font-size
  height: 1.3em
  display: flex
  align-items: center

  // .poll-status-inline
  &.status
    margin-top: $tiny
    font-size: $secondary-font-size

  // .poll-type-indicator
  &.type
    margin-top: $tiny
    font-size: $tertiary-font-size

.skeleton-line
  height: 0.75em
  max-width: 100%
  +skeleton-shimmer
</style>
