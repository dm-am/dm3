<script setup lang="ts" generic="T extends ExpandableItem">
/**
 * ExpandableList — unified accordion component.
 *
 * Two layout modes:
 *   1. Single title (default): one "title" column per row.
 *   2. Multi-column (pass `columns` prop): header + cells per column.
 *
 * Animation: pure CSS grid-template-rows 0fr→1fr, three-level structure:
 *   .details-grid  — grid container, animates grid-template-rows
 *   .details-clip  — overflow:hidden + min-height:0, clips to 0
 *   .expandable-details — content with padding
 *
 * Borders: explicit border-bottom on children (NOT gap+background trick).
 * The gap trick is incompatible with accordion animation because
 * 0-height elements still participate in flex gap, causing gray trails.
 */

import { computed } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useExpandable } from "@/shared/lib/composables";

export interface ExpandableItem {
  id: string;
  title?: string;
  content?: string | string[];
  [key: string]: unknown;
}

export interface ExpandableListColumn<Row extends ExpandableItem = ExpandableItem> {
  key: keyof Row & string;
  label: string;
  width?: string;
  align?: "left" | "center" | "right";
  bold?: boolean;
}

const props = withDefaults(
  defineProps<{
    items: T[];
    columns?: ExpandableListColumn<T>[];
    allowMultiple?: boolean;
  }>(),
  {
    columns: undefined,
    allowMultiple: false,
  },
);

defineSlots<{
  content: (props: { item: T; index: number }) => void;
  [key: `item-${string}`]: (props: { item: T }) => void;
}>();

const itemIds = computed(() => props.items.map((i) => i.id));

const { toggle, isExpanded } = useExpandable({
  multiple: props.allowMultiple,
  ids: itemIds.value,
});

const gridTemplate = computed(() => {
  if (!props.columns) return undefined;
  const tracks = props.columns.map((c) => c.width ?? "1fr").join(" ");
  return `20px ${tracks}`;
});

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}

function cellAlign(column: ExpandableListColumn<T>): string {
  return column.align ?? "left";
}
</script>

<template>
  <div class="expandable-list" role="list">
    <div
      v-if="columns"
      class="expandable-header"
      :style="{ gridTemplateColumns: gridTemplate }"
      aria-hidden="true"
    >
      <span aria-hidden="true" />
      <span
        v-for="column in columns"
        :key="column.key"
        class="header-cell"
        :style="{ textAlign: cellAlign(column) }"
      >
        {{ column.label }}
      </span>
    </div>

    <template v-for="(item, index) in items" :key="item.id">
      <!-- Row: multi-column -->
      <div
        v-if="columns"
        class="expandable-row expandable-row--grid"
        :class="{ expanded: isExpanded(item.id) }"
        :style="{ gridTemplateColumns: gridTemplate }"
        role="button"
        tabindex="0"
        :aria-expanded="isExpanded(item.id)"
        :aria-controls="`expandable-details-${item.id}`"
        @click="toggle(item.id)"
        @keydown="handleKeydown($event, item.id)"
      >
        <span class="expand-icon" aria-hidden="true">{{
          isExpanded(item.id) ? symbols.triangleDown : symbols.triangleRight
        }}</span>
        <span
          v-for="column in columns"
          :key="column.key"
          class="row-cell"
          :class="{ 'row-cell--bold': column.bold }"
          :style="{ textAlign: cellAlign(column) }"
        >
          {{ item[column.key] }}
        </span>
      </div>

      <!-- Row: single-title -->
      <div
        v-else
        class="expandable-row"
        :class="{ expanded: isExpanded(item.id) }"
        role="button"
        tabindex="0"
        :aria-expanded="isExpanded(item.id)"
        :aria-controls="`expandable-details-${item.id}`"
        @click="toggle(item.id)"
        @keydown="handleKeydown($event, item.id)"
      >
        <span class="expand-icon" aria-hidden="true">{{
          isExpanded(item.id) ? symbols.triangleDown : symbols.triangleRight
        }}</span>
        <span class="item-title">{{ item.title }}</span>
      </div>

      <!-- Details: 3-level grid animation -->
      <div
        class="details-grid"
        :class="{ open: isExpanded(item.id) }"
      >
        <div class="details-clip">
          <div
            :id="`expandable-details-${item.id}`"
            class="expandable-details"
            :aria-hidden="!isExpanded(item.id)"
          >
            <slot :name="`item-${item.id}`" :item="item">
              <slot name="content" :item="item" :index="index">
                <template v-if="Array.isArray(item.content)">
                  <ul>
                    <li v-for="(line, lineIdx) in item.content" :key="lineIdx">
                      {{ line }}
                    </li>
                  </ul>
                </template>
                <template v-else-if="item.content">
                  {{ item.content }}
                </template>
              </slot>
            </slot>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Animations"
@import "src/assets/styles/Tables"

// --- Container ---
// Override +table's gap/background border trick with explicit borders.
// Gap-based borders are incompatible with accordion animation.
.expandable-list
  +table
  gap: 0
  padding: 0
  background-color: transparent
  border: $table-gap solid $border

// --- Header ---
.expandable-header
  display: grid
  align-items: center
  gap: $small
  +table-header
  border-bottom: $table-gap solid $border

.header-cell
  padding: 0

// --- Rows ---
.expandable-row
  +expandable-row
  &
    user-select: text

.expandable-row--grid
  display: grid
  align-items: center
  gap: $small

.row-cell
  user-select: text
  &--bold
    font-weight: 600

.expand-icon
  user-select: none
  +expand-icon

.item-title
  font-weight: 500

// --- Accordion animation ---
// Border-bottom on .details-grid serves as row separator:
// - Collapsed (0fr): 1px border directly below the row = separator
// - Expanded (1fr): 1px border below details content = separator
// No flex gap involved → no gray trail during animation.
.details-grid
  display: grid
  grid-template-rows: 0fr
  border-bottom: $table-gap solid $border
  +transition-safe(grid-template-rows, $transition-slow)
  &:last-child
    border-bottom: none
  &.open
    grid-template-rows: 1fr

// Overflow clip — MUST NOT have padding (padding goes on level 3)
.details-clip
  overflow: hidden
  min-height: 0

// Content with padding
.expandable-details
  +expandable-details
  border-top: $table-gap solid $border
  user-select: text
  cursor: text

  :deep(ul)
    list-style: disc
    padding-left: $medium + $small
    margin: 0

    li
      margin-bottom: $minor

      &:last-child
        margin-bottom: 0

@media (max-width: $mobile-breakpoint)
  .expandable-row
    +expandable-row-mobile

  .expandable-details
    +expandable-details-mobile
</style>
