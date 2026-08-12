<script setup lang="ts" generic="T extends { id: string | number }">
import type { Column, SortState } from "./types";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import DataTableSkeleton from "./DataTableSkeleton.vue";
import { NOTHING_TO_SHOW } from "@/shared/lib/constants/copy";

const props = withDefaults(
  defineProps<{
    /** Column definitions */
    columns: Column[];
    /** Row data array */
    data: T[];
    /** Loading state */
    loading?: boolean;
    /** Empty state text */
    emptyText?: string;
    /** Current sort state */
    sort?: SortState;
    /** Whether to show row numbers */
    showRowNumbers?: boolean;
    /** Starting row number (for pagination) */
    startRowNumber?: number;
    /** Accessible label for the table element */
    ariaLabel?: string;
    /**
     * Table layout algorithm. "fixed" (default) keeps columns strictly at
     * their configured widths. "auto" lets content-sized columns (nowrap
     * cells) take exactly the room they need at any viewport, with the
     * width-less columns absorbing the rest — use it when a column must
     * guarantee its content on one line without starving the others.
     */
    tableLayout?: "fixed" | "auto";
  }>(),
  {
    loading: false,
    emptyText: NOTHING_TO_SHOW,
    showRowNumbers: false,
    startRowNumber: 1,
    ariaLabel: undefined,
    tableLayout: "fixed",
  },
);

defineSlots<{
  /** Custom cell content slot */
  [key: `cell-${string}`]: (props: { row: T; index: number }) => void;
  /** Custom header slot */
  [key: `header-${string}`]: (props: { column: Column }) => void;
  /** Empty state slot */
  empty: () => void;
  /** Footer slot (for pagination) */
  footer: () => void;
}>();

// Default cell renderer value. Lives in script because generic casts with
// angle brackets inside template expressions break prettier's Vue parser.
function cellValue(row: T, key: string): unknown {
  return (row as Record<string, unknown>)[key];
}

function isSortedBy(column: Column): boolean {
  return props.sort?.key === column.key;
}

function getAriaSort(column: Column): "ascending" | "descending" | undefined {
  if (!isSortedBy(column)) return undefined;
  return props.sort?.direction === "asc" ? "ascending" : "descending";
}
</script>

<template>
  <table
    class="data-table"
    :class="{ 'layout-auto': tableLayout === 'auto' }"
    :aria-busy="loading ? 'true' : undefined"
    :aria-label="ariaLabel"
  >
    <!-- Header -->
    <thead>
      <tr class="table-header">
        <th v-if="showRowNumbers" scope="col" class="col col-number">#</th>
        <th
          v-for="column in columns"
          :key="column.key"
          scope="col"
          class="col"
          :class="[
            `col-${column.key}`,
            { 'hide-mobile': column.hideOnMobile },
            `align-${column.align || 'left'}`,
          ]"
          :style="column.width ? { width: column.width } : {}"
          :aria-sort="getAriaSort(column)"
        >
          <!-- Headers are presentational labels. Sorting is driven solely by
               the SortButton in each table's filter (per product decision);
               aria-sort still announces the active sort column to screen
               readers. -->
          <span class="header-content">
            <slot :name="`header-${column.key}`" :column="column">
              {{ column.label }}
            </slot>
          </span>
        </th>
      </tr>
    </thead>

    <!-- Skeleton loading state (rendered as real table rows for correct column widths).
         Only shown on initial load — while reloading with stale rows present,
         the rows stay visible (table carries aria-busy). -->
    <tbody v-if="loading && !data?.length" aria-hidden="true">
      <DataTableSkeleton
        :rows="10"
        :columns="columns"
        :show-row-numbers="showRowNumbers"
      />
    </tbody>

    <tbody v-else>
      <!-- Empty state -->
      <tr v-if="!data?.length" class="table-empty-row">
        <td :colspan="showRowNumbers ? columns.length + 1 : columns.length">
          <slot name="empty">
            <secondary-text class="table-empty">
              {{ emptyText }}
            </secondary-text>
          </slot>
        </td>
      </tr>

      <!-- Rows (no v-memo: row content can change while id stays the same,
           e.g. online indicators or refreshed counters) -->
      <template v-else>
        <tr v-for="(row, index) in data" :key="row.id" class="table-row">
          <td v-if="showRowNumbers" class="col col-number">
            {{ startRowNumber + index }}
          </td>
          <td
            v-for="column in columns"
            :key="column.key"
            class="col"
            :class="[
              `col-${column.key}`,
              `align-${column.align || 'left'}`,
              { 'hide-mobile': column.hideOnMobile },
            ]"
          >
            <slot :name="`cell-${column.key}`" :row="row" :index="index">
              {{ cellValue(row, column.key) }}
            </slot>
          </td>
        </tr>
      </template>
    </tbody>

    <!-- Footer (pagination). Use $slots directly (re-evaluated each render) so
         a footer whose v-if flips true after async paging loads still shows —
         a cached computed(() => !!slots.footer) would not react to that. -->
    <tfoot v-if="$slots.footer">
      <tr class="table-footer">
        <td :colspan="showRowNumbers ? columns.length + 1 : columns.length">
          <slot name="footer" />
        </td>
      </tr>
    </tfoot>
  </table>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Tables"

// Table metrics are the module's, not this component's: _Tables.sass declares
// $table-cell-padding-v/-h and $table-gap, and the mixins that size a desktop
// cell (+table-header, +table-row, +expandable-row, +expandable-details) read
// them. A copy of the numbers here parts from those mixins the moment the
// declarations move.
.data-table
  width: 100%
  table-layout: fixed
  border-collapse: separate
  border-spacing: $table-gap
  background-color: $border

  &.layout-auto
    table-layout: auto

.table-header
  th
    padding: $table-cell-padding-v $table-cell-padding-h
    background-color: $bg-element-accent
    color: $text
    font-weight: bold
    text-align: center
    vertical-align: middle

.header-content
  display: inline-flex
  align-items: center
  justify-content: center
  gap: $tiny

.table-row
  +table-row-hover
  td
    padding: $table-cell-padding-v $table-cell-padding-h
    background-color: $bg-element
    vertical-align: middle
    word-wrap: break-word
    overflow-wrap: break-word

.col
  &.align-left
    text-align: left

  &.align-center
    text-align: center

  &.align-right
    text-align: right

  &.col-number
    text-align: center
    width: 36px

  &.col-title
    text-align: left

  &.col-tags
    text-align: left

.table-empty-row
  td
    padding: $big
    background-color: $bg-element

.table-footer
  td
    text-align: center

    // Only show padding/background when footer has content
    &:not(:empty)
      padding: $table-cell-padding-v $table-cell-padding-h
      background-color: $bg-element-accent

// Mobile responsiveness
@media (max-width: $bp-tablet)
  .hide-mobile
    display: none
</style>
