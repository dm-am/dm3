<script setup lang="ts">
/**
 * Skeleton for a collapsed ExpandableList (accordion rows).
 *
 * Layout contract — matches ExpandableList.vue with every row collapsed:
 *   1. Container: 1px solid $border frame (the +table border override),
 *      transparent background, no padding.
 *   2. Row: $table-cell-padding-v/-h padding (8px 12px), flex with $small
 *      gap, 12px expand-icon column, single title line. Real title line
 *      box is 16px font x 1.3 line-height = ~21px, so the row content is
 *      pinned to 21px -> ~37px outer row height.
 *   3. Rows separated by the 1px .details-grid border (none after last).
 *   4. Optional header twin (`with-header`) for lists in multi-column mode:
 *      same +table-header padding and one bold-line-height text bar, so the
 *      loaded list's .expandable-header does not shift rows down.
 */
withDefaults(
  defineProps<{
    /** Number of skeleton rows to render */
    count?: number;
    /** Render a header row twin (for ExpandableList in `columns` mode) */
    withHeader?: boolean;
  }>(),
  { count: 3, withHeader: false },
);
</script>

<template>
  <div class="expandable-list-skeleton" aria-hidden="true">
    <div v-if="withHeader" class="skeleton-header">
      <div class="skeleton-title" />
    </div>
    <div v-for="i in count" :key="i" class="skeleton-row">
      <div class="skeleton-icon" />
      <div class="skeleton-title" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/Tables" as *

.expandable-list-skeleton
  border: $table-gap solid $border

// Twin of ExpandableList's .expandable-header (+table-header): same cell
// padding and the same ~21px single text line box as the rows below.
.skeleton-header
  display: flex
  align-items: center
  padding: $table-cell-padding-v $table-cell-padding-h
  min-height: 21px
  background-color: $bg-element-accent
  border-bottom: $table-gap solid $border

.skeleton-row
  display: flex
  align-items: center
  gap: $small
  padding: $table-cell-padding-v $table-cell-padding-h
  // Real row content is one 16px x 1.3 title line box (~21px).
  min-height: 21px
  background-color: $bg-element
  border-bottom: $table-gap solid $border

  &:last-child
    border-bottom: none

// Twin of the +expand-icon column (12px wide, 10px glyph).
.skeleton-icon
  width: $expand-icon-width
  height: 10px
  +skeleton-shimmer

.skeleton-title
  width: 220px
  max-width: 60%
  height: 14px
  +skeleton-shimmer
</style>
