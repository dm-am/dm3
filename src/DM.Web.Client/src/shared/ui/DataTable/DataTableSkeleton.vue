<script setup lang="ts">
/**
 * Skeleton loading rows for DataTable.
 *
 * Renders actual <tr>/<td> elements (Vue 3 fragment) that slot directly
 * into a <tbody>, inheriting the parent table's column widths via
 * table-layout: fixed. Each column gets a shimmer bar whose width varies
 * per row for a realistic ragged appearance.
 *
 * Props mirror DataTable's column model so the skeleton matches the real
 * table pixel-for-pixel — no layout shift on data arrival.
 */
import type { Column } from "./types";

withDefaults(
  defineProps<{
    /** Number of skeleton rows to show */
    rows?: number;
    /** Column definitions (same as DataTable) */
    columns: Column[];
    /** Whether the table shows row numbers */
    showRowNumbers?: boolean;
  }>(),
  {
    rows: 10,
    showRowNumbers: false,
  },
);

// Varying width factors for realistic ragged-right appearance.
// The factor multiplies per-column to produce different bar widths
// across rows. Narrow columns (≤10%) get a fixed short bar instead.
const widthFactors = [0.6, 0.45, 0.7, 0.5, 0.55, 0.65, 0.4, 0.75];

function getBarWidth(rowIndex: number, colIndex: number, column: Column): string {
  // Narrow columns (counters, percentages) — short fixed bar
  const widthNum = column.width ? parseInt(column.width) : 0;
  if (widthNum > 0 && widthNum <= 10) {
    return "60%";
  }
  // rowIndex is 1-based (v-for), normalize to 0-based for even distribution
  const factor = widthFactors[(rowIndex - 1 + colIndex) % widthFactors.length];
  return `${Math.round(factor * 100)}%`;
}
</script>

<template>
  <tr
    v-for="rowIndex in rows"
    :key="rowIndex"
    class="skeleton-row"
  >
    <td v-if="showRowNumbers" class="skeleton-td col-number">
      <div class="skeleton-bar skeleton-bar-number" />
    </td>
    <td
      v-for="(column, colIndex) in columns"
      :key="column.key"
      class="skeleton-td"
      :class="[
        `col-${column.key}`,
        `align-${column.align || 'left'}`,
        { 'hide-mobile': column.hideOnMobile },
      ]"
    >
      <div
        class="skeleton-bar"
        :style="{ width: getBarWidth(rowIndex, colIndex, column) }"
      />
    </td>
  </tr>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Skeleton"

// Rows inherit table-layout: fixed column widths from the parent <table>.
// No need to set widths here — the <thead> defines them.
.skeleton-row
  td
    background-color: $bg-element

.skeleton-td
  padding: $small 12px  // matches DataTable td padding
  vertical-align: middle

  &.col-number
    text-align: center
    width: 36px

  &.align-left
    text-align: left

  &.align-center
    text-align: center

  &.align-right
    text-align: right

.skeleton-bar
  height: 16px  // approximate text line height
  +skeleton-shimmer

.skeleton-bar-number
  width: 20px
  margin: 0 auto

// Mobile responsiveness (matches DataTable)
@media (max-width: 768px)
  .hide-mobile
    display: none
</style>
