<script setup lang="ts">
/**
 * Shimmer placeholder for sidebar lists (games, blogs, forums, polls).
 *
 * Sizing contract — this skeleton must match the height of the real
 * content it stands in for, otherwise the page shifts vertically when
 * the fetch resolves:
 *
 *   - Each real row in a sidebar list is a single-line link rendered at
 *     the default sidebar font ($font-size, 16px) with the body
 *     line-height from Reset.sass (1.3) → one row is ~20.8px tall.
 *   - Rows are NOT separated by any gap in production (plain inline
 *     <div class="link"> stacking vertically with zero margin-between).
 *
 * To match this, each skeleton row has height: 1.3em and contains a
 * 12px shimmer bar centred vertically. The net height per row is
 * 20.8px, matching the real list exactly — no flex `gap` on the
 * wrapper because the real list has none.
 *
 * Shimmer uses the shared skeleton mixin (_Skeleton.sass) so the
 * animation is consistent with DataTableSkeleton and ProfileSkeleton.
 */
withDefaults(
  defineProps<{
    /** Number of lines to render. Caller sets this to the expected
     *  row count of the real list — not an arbitrary visual hint. */
    lines?: number;
  }>(),
  { lines: 3 },
);

// Vary line widths for a natural ragged-right appearance, matching the
// distribution of real game/blog titles in the sidebar.
function getWidth(index: number): string {
  const widths = ["85%", "70%", "90%", "60%", "75%", "80%", "65%"];
  return widths[(index - 1) % widths.length];
}
</script>

<script lang="ts">
export default { inheritAttrs: false };
</script>

<template>
  <li class="sidebar-skeleton" aria-hidden="true">
    <div v-for="i in lines" :key="i" class="skeleton-row">
      <div class="skeleton-line" :style="{ width: getWidth(i) }" />
    </div>
  </li>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Skeleton"

.sidebar-skeleton
  display: block

// One skeleton row = one link row in the real list. Matches the
// body line-height (1.3) set in Reset.sass so no vertical shift
// occurs when the skeleton is replaced by real content.
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
