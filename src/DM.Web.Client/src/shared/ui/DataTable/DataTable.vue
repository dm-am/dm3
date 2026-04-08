<script setup lang="ts" generic="T extends { id: string | number }">
import { computed, useSlots, ref, watch } from "vue";
import { useVirtualizer } from "@tanstack/vue-virtual";
import type { Column, SortState } from "./types";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import DataTableSkeleton from "./DataTableSkeleton.vue";
import { Tooltip } from "@/shared/ui/Tooltip";

const slots = useSlots();

// Virtual scroll threshold - use virtualization for large lists
const VIRTUAL_THRESHOLD = 50;

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
  }>(),
  {
    loading: false,
    emptyText: "Нет данных",
    showRowNumbers: false,
    startRowNumber: 1,
  },
);

const emit = defineEmits<{
  /** Emitted when sort header is clicked */
  sort: [column: Column, direction: "asc" | "desc"];
}>();

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

const hasFooter = computed(() => !!slots.footer);

// Virtual scroll setup
const tableBodyRef = ref<HTMLElement | null>(null);
const useVirtual = computed(() => props.data.length > VIRTUAL_THRESHOLD);
const ROW_HEIGHT = 40; // Approximate row height in pixels

const virtualizer = useVirtualizer(
  computed(() => ({
    count: props.data.length,
    getScrollElement: () => tableBodyRef.value,
    estimateSize: () => ROW_HEIGHT,
    overscan: 5,
    enabled: useVirtual.value,
  })),
);

const virtualRows = computed(() => virtualizer.value.getVirtualItems());
const totalHeight = computed(() => virtualizer.value.getTotalSize());

// Get row by index (for both virtual and non-virtual rendering)
function getRow(index: number): T {
  return props.data[index];
}

function getToggledDirection(column: Column): "asc" | "desc" {
  if (props.sort?.key === column.key) {
    return props.sort.direction === "asc" ? "desc" : "asc";
  }
  return "asc";
}

function isSortedBy(column: Column): boolean {
  return props.sort?.key === column.key;
}

function handleSortClick(column: Column) {
  emit("sort", column, getToggledDirection(column));
}
</script>

<template>
  <table class="data-table" cellspacing="1" cellpadding="4">
    <!-- Header -->
    <thead>
      <tr class="table-header">
        <th v-if="showRowNumbers" class="col col-number">#</th>
        <th
          v-for="column in columns"
          :key="column.key"
          class="col"
          :class="[
            `col-${column.key}`,
            {
              sortable: column.sortable,
              sorted: isSortedBy(column),
              'sorted-asc': isSortedBy(column) && sort?.direction === 'asc',
              'sorted-desc': isSortedBy(column) && sort?.direction === 'desc',
              'hide-mobile': column.hideOnMobile,
            },
            `align-${column.align || 'left'}`,
          ]"
          :style="column.width ? { width: column.width } : {}"
        >
          <span class="header-content">
            <slot :name="`header-${column.key}`" :column="column">
              {{ column.label }}
            </slot>
            <!-- Sort icon: on hover when not sorted, always visible when sorted -->
            <Tooltip
              v-if="column.sortable"
              :text="isSortedBy(column) ? 'Изменить порядок сортировки' : 'Сортировать по возрастанию'"
            >
              <button
                class="sort-icon-button"
                :class="{ active: isSortedBy(column) }"
                type="button"
                aria-label="Сортировка"
                @click.stop="handleSortClick(column)"
              >
              <!-- Ascending icon (shown on hover when not sorted, or when sorted asc) -->
              <svg
                v-if="!isSortedBy(column) || sort?.direction === 'asc'"
                class="sort-icon"
                viewBox="0 0 100 100"
                fill="currentColor"
              >
                <path d="M31.953,36.663l4.714-4.713L25.69,20.977c-1.303-1.302-3.415-1.302-4.714,0L10,31.95l4.714,4.717L20,31.38V80h6.667V31.38L31.953,36.663z"/>
                <rect x="43.333" y="26.667" width="46.667" height="6.667"/>
                <rect x="43.333" y="40" width="36.667" height="6.667"/>
                <rect x="43.333" y="53.333" width="26.667" height="6.667"/>
                <rect x="43.333" y="66.667" width="16.667" height="6.666"/>
              </svg>
              <!-- Descending icon (shown when sorted desc) -->
              <svg
                v-else
                class="sort-icon"
                viewBox="0 0 100 100"
                fill="currentColor"
              >
                <path d="M14.714,63.337L10,68.05l10.977,10.974c1.302,1.302,3.415,1.302,4.714,0L36.667,68.05l-4.714-4.717l-5.286,5.287V20H20v48.62L14.714,63.337z"/>
                <rect x="43.333" y="26.667" width="46.667" height="6.667"/>
                <rect x="43.333" y="40" width="36.667" height="6.667"/>
                <rect x="43.333" y="53.333" width="26.667" height="6.667"/>
                <rect x="43.333" y="66.667" width="16.667" height="6.666"/>
              </svg>
              </button>
            </Tooltip>
          </span>
        </th>
      </tr>
    </thead>

    <!-- Skeleton loading state (shown instead of tbody) -->
    <tbody v-if="loading" class="table-loading-body">
      <tr>
        <td :colspan="showRowNumbers ? columns.length + 1 : columns.length" class="skeleton-cell">
          <DataTableSkeleton :rows="10" :columns="columns.length" />
        </td>
      </tr>
    </tbody>

    <!-- Non-virtual tbody (for small lists) -->
    <tbody v-else-if="!useVirtual">
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

      <!-- Rows with v-memo for efficient updates -->
      <template v-else>
        <tr
          v-for="(row, index) in data"
          :key="row.id"
          v-memo="[row.id]"
          class="table-row"
        >
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
              {{ (row as Record<string, unknown>)[column.key] }}
            </slot>
          </td>
        </tr>
      </template>
    </tbody>

    <!-- Virtual tbody (for large lists > 50 items) -->
    <tbody
      v-else
      ref="tableBodyRef"
      class="virtual-tbody"
      :style="{ height: '600px' }"
    >
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

      <!-- Virtual rows container -->
      <template v-else>
        <tr
          class="virtual-spacer"
          :style="{ height: `${totalHeight}px`, position: 'relative' }"
        >
          <td :colspan="showRowNumbers ? columns.length + 1 : columns.length">
            <!-- Virtual rows -->
            <table class="virtual-inner-table" cellspacing="1" cellpadding="4">
              <tr
                v-for="virtualRow in virtualRows"
                :key="getRow(virtualRow.index).id"
                v-memo="[getRow(virtualRow.index).id]"
                class="table-row"
                :style="{
                  position: 'absolute',
                  top: `${virtualRow.start}px`,
                  left: 0,
                  right: 0,
                  height: `${virtualRow.size}px`,
                }"
              >
                <td v-if="showRowNumbers" class="col col-number">
                  {{ startRowNumber + virtualRow.index }}
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
                  :style="column.width ? { width: column.width } : {}"
                >
                  <slot
                    :name="`cell-${column.key}`"
                    :row="getRow(virtualRow.index)"
                    :index="virtualRow.index"
                  >
                    {{ (getRow(virtualRow.index) as Record<string, unknown>)[column.key] }}
                  </slot>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </template>
    </tbody>

    <!-- Footer (pagination) -->
    <tfoot v-if="hasFooter">
      <tr class="table-footer">
        <td :colspan="showRowNumbers ? columns.length + 1 : columns.length">
          <slot name="footer" />
        </td>
      </tr>
    </tfoot>
  </table>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"

.data-table
  width: 100%
  table-layout: fixed
  border-collapse: separate
  border-spacing: 1px
  background-color: $border

.table-header
  th
    padding: $small 12px
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

// Sort icon button styling
.sort-icon-button
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  color: transparent
  transition: color 0.15s ease

  &.active
    color: $text-muted

  &:focus-visible
    outline: 2px solid $accent-yellow
    outline-offset: 1px
    border-radius: 2px

// When hovering th, show icon (grey) - specificity 0-4-1
th.sortable:hover .sort-icon-button:not(.active)
  color: $text-muted

// Hover on icon - text color - specificity 0-5-1 (wins over above)
th.sortable:hover .sort-icon-button:not(.active):hover,
th.sortable .sort-icon-button.active:hover
  color: $text

.sort-icon
  width: 24px
  height: 24px
  transform: translateY(2px)

.table-row
  +table-row-hover
  td
    padding: $small 12px
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

.table-loading-body
  .skeleton-cell
    padding: 0
    background-color: transparent

.table-empty-row
  td
    padding: $big
    text-align: center
    background-color: $bg-element

.table-footer
  td
    text-align: center

    // Only show padding/background when footer has content
    &:not(:empty)
      padding: $small 12px
      background-color: $bg-element-accent

// Mobile responsiveness
@media (max-width: 768px)
  .hide-mobile
    display: none

// Virtual scroll styles
.virtual-tbody
  display: block
  overflow-y: auto
  contain: strict

.virtual-spacer
  display: block

.virtual-inner-table
  width: 100%
  table-layout: fixed
  border-collapse: separate
  border-spacing: 1px
</style>
