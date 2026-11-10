<script setup lang="ts">
// Real rows in the parent's <tbody>, so the skeleton takes the table's own column widths.
import type { Column } from "./types";

withDefaults(
  defineProps<{
    /** Column definitions (same as DataTable) */
    columns: Column[];
    /** Whether the table shows row numbers */
    showRowNumbers?: boolean;
  }>(),
  {
    showRowNumbers: false,
  },
);

/** Skeleton rows shown while the table loads. */
const ROWS = 10;

// Per-row factors for a ragged right edge; narrow columns get a fixed short bar.
const widthFactors = [0.6, 0.45, 0.7, 0.5, 0.55, 0.65, 0.4, 0.75];

function getBarWidth(
  rowIndex: number,
  colIndex: number,
  column: Column,
): string {
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
  <tr v-for="rowIndex in ROWS" :key="rowIndex" class="skeleton-row">
    <td v-if="showRowNumbers" class="skeleton-td col-number">
      <div class="skeleton-bar skeleton-bar-number" />
    </td>
    <td
      v-for="(column, colIndex) in columns"
      :key="column.key"
      class="skeleton-td"
      :class="[`col-${column.key}`, `align-${column.align || 'left'}`]"
    >
      <div
        class="skeleton-bar"
        :style="{ width: getBarWidth(rowIndex, colIndex, column) }"
      />
    </td>
  </tr>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/Tables" as *

.skeleton-row
  td
    background-color: $bg-element

.skeleton-td
  padding: $table-cell-padding-v $table-cell-padding-h
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
  height: 18px  // matches the td text line (16px x 1.25 line-height)
  +skeleton-shimmer

.skeleton-bar-number
  width: 20px
  margin: 0 auto
</style>
