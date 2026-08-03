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
import { useExpandable } from "@/shared/lib/composables";

export interface ExpandableItem {
  id: string;
  title?: string;
  content?: string | string[];
  [key: string]: unknown;
}

export interface ExpandableListColumn<
  Row extends ExpandableItem = ExpandableItem,
> {
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
  // Optional per-column cell override for multi-column mode (mirrors
  // DataTable's #cell-${key} pattern) — falls back to the plain mustache
  // value when the consumer doesn't provide it for a given column.
  [key: `cell-${string}`]: (props: { item: T }) => void;
}>();

const itemIds = computed(() => props.items.map((i) => i.id));

const { toggle, isExpanded } = useExpandable({
  multiple: props.allowMultiple,
  ids: itemIds,
});

// Multi-column rows render as a CSS table (display: table/table-cell),
// NOT a grid: Chrome's selection serializer emits a newline between
// element grid items (a row copied as "Нарушение\nБаллы"), while table
// cells copy tab-separated — the same behavior as the site's real data
// tables. Fixed table layout + explicit per-cell widths reproduce the
// former "20px icon + column widths + gap" grid geometry: the $small
// inter-column gap lives as padding-left inside every non-first cell,
// which content-box table columns add on top of the specified width.
function cellStyle(column: ExpandableListColumn<T>) {
  return {
    width: column.width,
    textAlign: cellAlign(column),
  };
}

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}

function cellAlign(
  column: ExpandableListColumn<T>,
): "left" | "center" | "right" {
  return column.align ?? "left";
}

// Idempotent open — used by consumers deep-linking to a specific item via
// route.hash (e.g. RulesPage auto-expanding the section matching #id).
// Does nothing if the item is already expanded or unknown.
function expandItem(id: string) {
  if (!isExpanded(id)) toggle(id);
}

defineExpose({ expandItem });
</script>

<template>
  <!-- No list role: children are toggle buttons + detail panels, not listitems -->
  <div class="expandable-list">
    <div v-if="columns" class="expandable-header">
      <span class="icon-cell" aria-hidden="true" />
      <span
        v-for="column in columns"
        :key="column.key"
        class="header-cell"
        :style="cellStyle(column)"
      >
        {{ column.label }}
      </span>
    </div>

    <template v-for="(item, index) in items" :key="item.id">
      <!-- Row: multi-column -->
      <div
        v-if="columns"
        :id="`expandable-toggle-${item.id}`"
        class="expandable-row expandable-row--grid"
        :class="{ expanded: isExpanded(item.id) }"
        role="button"
        tabindex="0"
        :aria-expanded="isExpanded(item.id)"
        :aria-controls="`expandable-details-${item.id}`"
        @click="toggle(item.id)"
        @keydown="handleKeydown($event, item.id)"
      >
        <span
          class="expand-icon expand-marker"
          :class="{ expanded: isExpanded(item.id) }"
          aria-hidden="true"
        />
        <span
          v-for="column in columns"
          :key="column.key"
          class="row-cell"
          :class="{ 'row-cell--bold': column.bold }"
          :style="cellStyle(column)"
        >
          <slot :name="`cell-${column.key}`" :item="item">{{
            item[column.key]
          }}</slot>
        </span>
      </div>

      <!-- Row: single-title -->
      <div
        v-else
        :id="`expandable-toggle-${item.id}`"
        class="expandable-row"
        :class="{ expanded: isExpanded(item.id) }"
        role="button"
        tabindex="0"
        :aria-expanded="isExpanded(item.id)"
        :aria-controls="`expandable-details-${item.id}`"
        @click="toggle(item.id)"
        @keydown="handleKeydown($event, item.id)"
      >
        <span
          class="expand-icon expand-marker"
          :class="{ expanded: isExpanded(item.id) }"
          aria-hidden="true"
        />
        <span class="item-title">{{ item.title }}</span>
      </div>

      <!-- Details: 3-level grid animation -->
      <div
        class="details-grid"
        :class="{ open: isExpanded(item.id) }"
        :inert="!isExpanded(item.id)"
      >
        <div class="details-clip">
          <div :id="`expandable-details-${item.id}`" class="expandable-details">
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
@import "@/assets/styles/Animations"
@import "@/assets/styles/Tables"

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
// CSS table, not grid: grid items copy with newlines between cells, table
// cells copy tab-separated (matching the site's real data tables). Fixed
// layout + explicit cell widths reproduce the former grid tracks; the
// former grid gap becomes padding-left inside every non-first cell
// (content-box table columns = specified width + padding, so a "80px"
// column occupies the same 8px-gap + 80px-track band as before).
.expandable-header
  display: table
  width: 100%
  box-sizing: border-box
  table-layout: fixed
  +table-header
  border-bottom: $table-gap solid $border

  > span
    display: table-cell
    vertical-align: middle

  > .icon-cell
    width: 20px

  > .header-cell
    padding-left: $small

// --- Rows ---
.expandable-row
  +expandable-row
  &
    user-select: text

.expandable-row--grid
  display: table
  width: 100%
  box-sizing: border-box
  table-layout: fixed

  > .expand-icon
    display: table-cell
    vertical-align: middle
    // Override the +expand-icon 12px flex width: the icon column of the
    // former "20px <tracks>" grid template.
    width: 20px

  > .row-cell
    display: table-cell
    vertical-align: middle
    padding-left: $small

.row-cell
  user-select: text
  &--bold
    font-weight: 600

.expand-icon
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
  // Unified reveal tokens — same tempo as TruncatedContent and the
  // BBCode spoiler/NSFW blocks. Reduced-motion is handled in Reset.sass.
  transition: grid-template-rows $expand-duration $expand-easing
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
