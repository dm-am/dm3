<script setup lang="ts" generic="T extends ExpandableItem">
// Borders on children, not the gap-and-background trick: a 0-height row still takes a flex gap and leaves
// a grey trail in the animation.

import { computed } from "vue";
import { useExpandable } from "@/shared/lib/composables/useExpandable";
import type { ColumnAlign } from "@/shared/ui/DataTable";

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
  align?: ColumnAlign;
  bold?: boolean;
  /** As wide as its longest line and no wider: the columns without it share the room it leaves. */
  fit?: boolean;
}

const props = withDefaults(
  defineProps<{
    items: T[];
    columns?: ExpandableListColumn<T>[];
    allowMultiple?: boolean;
    /** baseline: cells of several lines line up on their first line. */
    rowAlign?: "middle" | "baseline";
    /** The rows hold links: a row opens only by its marker, and only with details of its own. */
    plainRows?: boolean;
  }>(),
  {
    columns: undefined,
    allowMultiple: false,
    rowAlign: "middle",
    plainRows: false,
  },
);

const slots = defineSlots<{
  content?: (props: { item: T; index: number }) => void;
  [key: `item-${string}`]: (props: { item: T }) => void;
  // A per-column cell override, as DataTable's #cell-${key}.
  [key: `cell-${string}`]: (props: { item: T }) => void;
}>();

const itemIds = computed(() => props.items.map((i) => i.id));

const { toggle, isExpanded } = useExpandable({
  multiple: props.allowMultiple,
  ids: itemIds,
});

function hasOwnDetails(item: T): boolean {
  if (slots[`item-${item.id}`] || slots.content) return true;
  return Array.isArray(item.content) ? item.content.length > 0 : !!item.content;
}

// A marker that opened an empty panel would promise what is not there.
function opens(item: T): boolean {
  return !props.plainRows || hasOwnDetails(item);
}

// "Развернуть все" counts every row; a row that cannot open shows nothing.
function isOpen(item: T): boolean {
  return opens(item) && isExpanded(item.id);
}

/** The marker column stands while any row opens. */
const hasMarker = computed(() => !props.plainRows || props.items.some(opens));

/** The cells of a row, the marker's included: what a panel of details spans. */
const span = computed(
  () => (props.columns?.length ?? 0) + (hasMarker.value ? 1 : 0),
);

// A table, as DataTable: it sizes the columns by what they hold (table-layout: auto), copies a row
// tab-separated, and is never narrower than its longest words, so where the page column is narrower the list
// scrolls. A column with a width keeps it, a fitting one takes its longest line (.cell--fit), and the others
// share the rest: one alone takes it whole, several in equal parts.
const sharedWidth = computed(() => {
  const sharing = (props.columns ?? []).filter((c) => !c.width && !c.fit);
  return sharing.length > 1 ? `${100 / sharing.length}%` : undefined;
});

function cellStyle(column: ExpandableListColumn<T>) {
  return {
    width: column.width ?? (column.fit ? undefined : sharedWidth.value),
    textAlign: column.align ?? "left",
  };
}

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}

// In a list of plain rows a press on a link or a control follows it and leaves the row as it is.
function onRowClick(event: MouseEvent, item: T) {
  if (!props.plainRows) {
    toggle(item.id);
    return;
  }
  const target = event.target as Element | null;
  if (target?.closest("a, button, input, select, textarea")) return;
  if (opens(item)) toggle(item.id);
}

function onRowKeydown(event: KeyboardEvent, item: T) {
  if (!props.plainRows) handleKeydown(event, item.id);
}

// Idempotent open, for a deep link to an item by the route's hash.
function expandItem(id: string) {
  if (!isExpanded(id)) toggle(id);
}

defineExpose({ expandItem });
</script>

<template>
  <!-- No list role: children are toggle buttons + detail panels, not listitems. Without columns the table, its
       body and the cell of a panel are boxes with no layout of their own (display: contents). -->
  <div
    class="expandable-list"
    :class="{
      'expandable-list--columns': columns,
      'expandable-list--baseline': columns && rowAlign === 'baseline',
    }"
  >
    <component :is="columns ? 'table' : 'div'" class="expandable-table">
      <thead v-if="columns">
        <tr class="expandable-header">
          <th v-if="hasMarker" class="icon-cell" />
          <th
            v-for="column in columns"
            :key="column.key"
            scope="col"
            class="header-cell"
            :class="{ 'cell--fit': column.fit }"
            :style="cellStyle(column)"
          >
            {{ column.label }}
          </th>
        </tr>
      </thead>

      <component :is="columns ? 'tbody' : 'div'" class="expandable-items">
        <template v-for="(item, index) in items" :key="item.id">
          <tr
            v-if="columns"
            :id="`expandable-toggle-${item.id}`"
            class="expandable-row expandable-row--grid"
            :class="{
              expanded: isOpen(item),
              'expandable-row--static': !opens(item),
            }"
            :role="plainRows ? undefined : 'button'"
            :tabindex="plainRows ? undefined : 0"
            :aria-expanded="plainRows ? undefined : isExpanded(item.id)"
            :aria-controls="
              plainRows ? undefined : `expandable-details-${item.id}`
            "
            @click="onRowClick($event, item)"
            @keydown="onRowKeydown($event, item)"
          >
            <td v-if="!plainRows" class="expand-icon">
              <span
                class="expand-marker"
                :class="{ expanded: isExpanded(item.id) }"
                aria-hidden="true"
              />
            </td>
            <td v-else-if="hasMarker" class="expand-icon">
              <button
                v-if="opens(item)"
                type="button"
                class="expand-toggle expand-marker"
                :class="{ expanded: isOpen(item) }"
                :aria-expanded="isOpen(item)"
                :aria-controls="`expandable-details-${item.id}`"
                :aria-labelledby="`expandable-name-${item.id}`"
                @click="toggle(item.id)"
              />
            </td>
            <td
              v-for="(column, columnIndex) in columns"
              :id="
                plainRows && columnIndex === 0
                  ? `expandable-name-${item.id}`
                  : undefined
              "
              :key="column.key"
              class="row-cell"
              :class="{
                'row-cell--bold': column.bold,
                'cell--fit': column.fit,
              }"
              :style="cellStyle(column)"
            >
              <slot :name="`cell-${column.key}`" :item="item">{{
                item[column.key]
              }}</slot>
            </td>
          </tr>

          <div
            v-else
            :id="`expandable-toggle-${item.id}`"
            class="expandable-row expandable-row--title"
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

          <component :is="columns ? 'tr' : 'div'" class="details-row">
            <component
              :is="columns ? 'td' : 'div'"
              class="details-cell"
              :colspan="columns ? span : undefined"
            >
              <div
                class="details-grid"
                :class="{ open: isOpen(item) }"
                :inert="!isOpen(item)"
              >
                <div class="details-clip">
                  <div
                    :id="`expandable-details-${item.id}`"
                    class="expandable-details"
                  >
                    <slot :name="`item-${item.id}`" :item="item">
                      <slot name="content" :item="item" :index="index">
                        <template v-if="Array.isArray(item.content)">
                          <ul>
                            <li
                              v-for="(line, lineIdx) in item.content"
                              :key="lineIdx"
                            >
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
            </component>
          </component>
        </template>
      </component>
    </component>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Animations" as *
@use "@/assets/styles/Tables" as *

// Explicit borders: gap-based ones break the accordion animation.
.expandable-list
  +table
  gap: 0
  padding: 0
  background-color: transparent
  border: $table-gap solid $border

// Without columns the rows and the panels stand straight in the list's column.
.expandable-list:not(.expandable-list--columns)
  .expandable-table,
  .expandable-items,
  .details-row,
  .details-cell
    display: contents

// Scrolls sideways in its own size-container box; the details stick to its left edge at the box's width.
.expandable-list--columns
  display: block
  overflow-x: auto
  container-type: inline-size

  .details-clip
    position: sticky
    left: 0
    width: 100cqi

.expandable-table
  width: 100%
  table-layout: auto
  border-collapse: collapse

// A column as wide as its longest line: the table gives a cell of 1px the least its content takes.
.cell--fit
  width: 1px
  white-space: nowrap

// The row's padding on its outer cells, the gap between columns on the left of the rest.
.expandable-header > th,
.expandable-row--grid > td
  padding: $table-cell-padding-v 0 $table-cell-padding-v $small
  vertical-align: middle

  &:first-child
    padding-left: $table-cell-padding-h

  &:last-child
    padding-right: $table-cell-padding-h

.expandable-header
  +table-header

  > th
    border-bottom: $table-gap solid $border

  > .icon-cell
    width: 20px

.expandable-row
  +expandable-row
  &
    user-select: text

// Inline flow, not flex: a flex row copies the empty marker as a line of its own.
.expandable-row--title
  display: block

  > .expand-icon
    margin-right: $small

.expandable-row--grid > .expand-icon
  display: table-cell
  width: 20px

// Cells of several lines line up on their first line, and the marker with it.
.expandable-list--baseline .expandable-row--grid > td
  vertical-align: baseline

// Text in a cell breaks between words only: the list scrolls before a word is
// cut.
.header-cell,
.row-cell
  hyphens: none
  overflow-wrap: normal

.row-cell
  user-select: text
  &--bold
    font-weight: 700

// Nothing to open: no pointer and no hover that would promise a press.
.expandable-row--static
  cursor: auto
  &:hover
    background-image: none

// A button: a link inside role="button" is no link to a screen reader, and gives its Enter to the row.
.expand-toggle
  padding: 0
  border: none
  background: none
  font: inherit
  line-height: inherit
  color: inherit
  cursor: pointer
  &:focus-visible
    outline: none

.expandable-row:has(.expand-toggle:focus-visible)
  +focus-ring($inward: true)

.expand-icon
  +expand-icon

.item-title
  font-weight: 400

.details-cell
  padding: 0

// The bottom border is the row separator, open or shut: no flex gap, no grey trail.
.details-grid
  display: grid
  grid-template-rows: 0fr
  border-bottom: $table-gap solid $border
  // The reveal tempo of TruncatedContent and the spoilers.
  transition: grid-template-rows $expand-duration $expand-easing
  &.open
    grid-template-rows: 1fr

.details-row:last-child .details-grid
  border-bottom: none

// Overflow clip — MUST NOT have padding (padding goes on level 3)
.details-clip
  overflow: hidden
  min-height: 0

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
</style>
