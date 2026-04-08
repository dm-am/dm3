<script setup lang="ts">
/**
 * Skeleton loading for DataTable.
 * Matches DataTable row height (40px) and padding ($small 12px).
 */
withDefaults(
  defineProps<{
    /** Number of skeleton rows to show */
    rows?: number;
    /** Number of columns */
    columns?: number;
  }>(),
  {
    rows: 10,
    columns: 4,
  },
);

// Varying widths for realistic appearance
const cellWidths = ["60%", "40%", "50%", "30%"];

function getCellWidth(columnIndex: number): string {
  return cellWidths[columnIndex % cellWidths.length];
}
</script>

<template>
  <div class="table-skeleton">
    <div v-for="rowIndex in rows" :key="rowIndex" class="skeleton-row">
      <div
        v-for="colIndex in columns"
        :key="colIndex"
        class="skeleton-cell"
      >
        <div class="skeleton-content" :style="{ width: getCellWidth(colIndex - 1) }" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

.table-skeleton
  display: flex
  flex-direction: column
  gap: 1px  // matches DataTable border-spacing
  background-color: $border

.skeleton-row
  display: flex
  gap: 1px  // matches DataTable border-spacing
  height: 40px  // matches ROW_HEIGHT

.skeleton-cell
  flex: 1
  display: flex
  align-items: center
  padding: $small 12px  // matches DataTable td padding
  background-color: $bg-element

.skeleton-content
  height: 16px  // text line height
  +skeleton-shimmer
</style>
