<script setup lang="ts" generic="T extends { id: string | number }">
import { computed, onMounted, onUnmounted, ref } from "vue";
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
     * The row that has a line of its own opened under it: a question or a
     * result about that row (the `details` slot), across the whole table so a
     * sentence and its buttons are not squeezed into one narrow cell.
     */
    detailsFor?: string | number | null;
    /**
     * A class of the caller's for a row it sets apart (a hidden contest
     * series, dimmed), styled by the caller; none for the others.
     */
    rowClass?: (row: T) => string | undefined;
  }>(),
  {
    loading: false,
    emptyText: NOTHING_TO_SHOW,
    showRowNumbers: false,
    startRowNumber: 1,
    ariaLabel: undefined,
    detailsFor: null,
    rowClass: undefined,
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
  /** The line opened under the row `detailsFor` names. */
  details: (props: { row: T; index: number }) => void;
}>();

// The width of the box the table stands in, for the footer (see the styles).
// Unknown until laid out, and in a test: the footer then spans the table.
const scrollBox = ref<HTMLElement | null>(null);
const boxWidth = ref<number | null>(null);
let boxObserver: ResizeObserver | null = null;
onMounted(() => {
  if (typeof ResizeObserver === "undefined" || !scrollBox.value) return;
  boxObserver = new ResizeObserver(([entry]) => {
    boxWidth.value = entry.contentRect.width;
  });
  boxObserver.observe(scrollBox.value);
});
onUnmounted(() => boxObserver?.disconnect());

/** The cells a line across the whole table spans: every column. */
const span = computed(
  () => (props.showRowNumbers ? 1 : 0) + props.columns.length,
);

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
  <!-- The box the table stands in (see the styles). -->
  <div
    ref="scrollBox"
    class="data-table-scroll"
    :style="boxWidth ? { '--data-table-box': `${boxWidth}px` } : undefined"
  >
    <table
      class="data-table"
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
            :class="[`col-${column.key}`, `align-${column.align || 'left'}`]"
            :style="column.width ? { width: column.width } : undefined"
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
          <td :colspan="span">
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
          <template v-for="(row, index) in data" :key="row.id">
            <tr class="table-row" :class="rowClass?.(row)">
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
                ]"
              >
                <slot :name="`cell-${column.key}`" :row="row" :index="index">
                  {{ cellValue(row, column.key) }}
                </slot>
              </td>
            </tr>
            <tr
              v-if="detailsFor !== null && row.id === detailsFor"
              class="table-details-row"
            >
              <td :colspan="span">
                <slot name="details" :row="row" :index="index" />
              </td>
            </tr>
          </template>
        </template>
      </tbody>

      <!-- Footer (pagination). Use $slots directly (re-evaluated each render) so
         a footer whose v-if flips true after async paging loads still shows —
         a cached computed(() => !!slots.footer) would not react to that. -->
      <tfoot v-if="$slots.footer">
        <tr class="table-footer">
          <td :colspan="span">
            <div class="table-footer-line"><slot name="footer" /></div>
          </td>
        </tr>
      </tfoot>
    </table>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

// Table metrics are the module's, not this component's: _Tables.sass declares
// $table-cell-padding-v/-h and $table-gap, and the mixins that size a desktop
// cell (+table-header, +table-row, +expandable-row, +expandable-details) read
// them. A copy of the numbers here parts from those mixins the moment the
// declarations move.
//
// Every table lays out "auto", as the tables of DM2 do: a column starts from
// the width it declares (Column.width, whose shares and px leave the main
// column the rest) and is never narrower than its longest word. Every column
// stands at every width of the window: no column goes, and the text of a cell
// breaks between its words. Where the column of the page has the room, the
// table fits it. Where it has not, the table keeps every word whole and stays
// wider than the column, and the box around it scrolls sideways: the page
// does not. Words broken inside to fit would cut a title of /games at 1366
// into "Королевс" and "тв".
// Inline-size containment keeps the width of the table from reaching the
// ancestors of the box.
.data-table-scroll
  overflow-x: auto
  contain: inline-size

.data-table
  width: 100%
  table-layout: auto
  border-collapse: separate
  border-spacing: $table-gap
  background-color: $border

// Text in a table breaks between words only: a header of two words takes two
// lines rather than running into the next header, a word is never cut into
// syllables with a hyphen, and none is broken inside, a long login included.
:where(.data-table) th,
:where(.data-table) td
  hyphens: none
  overflow-wrap: normal

// And none of what a cell holds breaks a word either, whatever the component
// that draws it says for itself (UserLink breaks a long login). :deep - the
// content comes from the slots of the caller.
.data-table :deep(:is(th, td) *)
  hyphens: none
  overflow-wrap: normal

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

// The line opened under a row: a cell of the row's own padding and fill across
// every column, read from the left like the rest of a table's text.
.table-details-row td
  padding: $table-cell-padding-v $table-cell-padding-h
  background-color: $bg-element
  text-align: left

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
    &:not(:has(> .table-footer-line:empty))
      padding: $table-cell-padding-v $table-cell-padding-h
      background-color: $bg-element-accent

// The footer (the paging of a list) stands under the part of the table the
// reader sees. A table wider than its column scrolls in its box, and a footer
// as wide as the table centred its strip under the whole of it: on a phone the
// strip measured the table, laid out numbers for it and left ">>" out of sight
// until the table was scrolled. Held at the left edge of the box and as wide
// as the box, less the cell's own sides, it is where it was whenever the table
// fits. Without a measured box (before layout, in a test) it spans the table.
.table-footer-line
  position: sticky
  left: $table-gap + $table-cell-padding-h
  width: calc(var(--data-table-box) - #{2 * ($table-gap + $table-cell-padding-h)})
</style>
