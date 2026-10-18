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
 *
 * A list whose rows hold links (`plainRows`) differs from the others in two
 * ways: a row with nothing to open is a plain table row, and the toggle of a
 * row that opens is its marker. Every column stands in the row at every width.
 */

import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useExpandable } from "@/shared/lib/composables/useExpandable";
import { shareRoom, type FlexibleColumn } from "./shareRoom";

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
    /**
     * How the cells of a row line up: on their middle, or on their first
     * line, for cells of several lines (a role and its nickname next to a
     * list of names), where the middle of one cell would stand against the
     * second line of another.
     */
    rowAlign?: "middle" | "baseline";
    /**
     * The rows hold links (the staff table of the rules page): a row opens
     * only where it has details of its own, by its marker, and a row with
     * nothing to open is a plain table row.
     */
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

/** The width the rows stand in: the list's, without its frame. */
const listInner = ref(0);

const shownColumns = computed(() => props.columns);

function hasOwnDetails(item: T): boolean {
  if (slots[`item-${item.id}`] || slots.content) return true;
  return Array.isArray(item.content) ? item.content.length > 0 : !!item.content;
}

// Every row of a list opens, as before. A row of a list of plain rows opens
// only when it has details of its own: the staff table shows everything in the
// row, and a marker that opened an empty panel would promise what is not
// there.
function opens(item: T): boolean {
  return !props.plainRows || hasOwnDetails(item);
}

// "Развернуть все" counts every row; a row that cannot open shows nothing.
function isOpen(item: T): boolean {
  return opens(item) && isExpanded(item.id);
}

/** The marker column stands while any row opens. */
const hasMarker = computed(() => !props.plainRows || props.items.some(opens));

// Multi-column rows render as a CSS table (display: table/table-cell),
// NOT a grid: Chrome's selection serializer emits a newline between
// element grid items (a row copied as "Нарушение\nБаллы"), while table
// cells copy tab-separated — the same behavior as the site's real data
// tables. Fixed table layout + explicit per-cell widths reproduce the
// former "20px icon + column widths + gap" grid geometry: the $small
// inter-column gap lives as padding-left inside every non-first cell,
// which content-box table columns add on top of the specified width.
function cellStyle(column: ExpandableListColumn<T>) {
  const shared = sharedWidths.value[column.key];
  return {
    width: shared === undefined ? column.width : `${shared}px`,
    textAlign: cellAlign(column),
  };
}

// A column without a width in px is never narrower than its longest word, as
// in a data table. The row tables lay out "fixed" so that the header and
// every row share their tracks, and a fixed table does not look at what its
// cells hold: on a phone the columns with a width took the whole row and the
// one without got none, a subject standing one letter per line. So the list
// measures that word itself, in a hidden copy of each such cell laid out at
// its min-content width, and is never narrower than its columns; where the
// page column is narrower, the list scrolls sideways in its own box.
// Two such columns would share the room equally, and the one with the longer
// word lost it: the list gives them their widths itself (shareRoom.ts).
// The marker column, the row's padding on both sides and the gap each cell
// after the first keeps on its left (see the style below).
const MARKER_COLUMN = 20;
const ROW_PADDING = 2 * 12;
const CELL_GAP = 8;

const listEl = ref<HTMLElement | null>(null);
/** Per column key: its longest word, which is the least it keeps. */
const measured = ref<Record<string, FlexibleColumn>>({});

function isFlexible(column: ExpandableListColumn<T>) {
  return !column.width?.endsWith("px");
}

/** The gap in front of a shown column: none in front of the first cell of a row without a marker. */
function gapOf(index: number) {
  return index === 0 && !hasMarker.value ? 0 : CELL_GAP;
}

function measureFlexible() {
  const list = listEl.value;
  if (!list || !props.columns) return;
  const next: Record<string, FlexibleColumn> = {};
  for (const cell of list.querySelectorAll<HTMLElement>("[data-flexible]")) {
    const key = cell.dataset.flexible!;
    const copy = cell.cloneNode(true) as HTMLElement;
    copy.setAttribute("aria-hidden", "true");
    copy.removeAttribute("id");
    // The width the list gave the cell would otherwise be measured back.
    copy.style.removeProperty("width");
    copy.classList.add("measure-copy");
    // In front of the cell, so the copy takes the padding the cell has: the
    // first cell of a row without a marker has none, and a copy at the end
    // of the row would get the gap of a cell after the first.
    cell.before(copy);
    const width = Math.ceil(copy.getBoundingClientRect().width);
    copy.remove();
    const column = (next[key] ??= { want: 0, hold: 0 });
    column.want = Math.max(column.want, width);
    column.hold = column.want;
  }
  measured.value = next;
}

/**
 * Px widths for the flexible columns of a row, by key, when there are two or
 * more of them: each holds its longest word and the rest is shared. The last
 * one takes what the others leave, so a rounding never pushes a row past the
 * list. With one flexible column the table layout already gives it the rest.
 */
const sharedWidths = computed<Record<string, number>>(() => {
  const shown = shownColumns.value;
  if (!shown || !listInner.value) return {};
  const flexible = shown.filter(isFlexible);
  if (flexible.length < 2) return {};
  const columns = flexible.map((c) => measured.value[c.key]);
  if (columns.some((c) => !c)) return {};
  let room =
    listInner.value - ROW_PADDING - (hasMarker.value ? MARKER_COLUMN : 0);
  shown.forEach((column, index) => {
    if (!isFlexible(column)) room -= gapOf(index) + parseFloat(column.width!);
  });
  const widths = shareRoom(room, columns as FlexibleColumn[]);
  const result: Record<string, number> = {};
  flexible.slice(0, -1).forEach((column, index) => {
    result[column.key] = Math.floor(
      widths[index]! - gapOf(shown.indexOf(column)),
    );
  });
  return result;
});

// Only a list that shares room between columns needs its own width.
const needsWidth = computed(
  () => (props.columns?.filter(isFlexible).length ?? 0) >= 2,
);

function measureList() {
  const list = listEl.value;
  if (!list) return;
  listInner.value = list.clientWidth;
}

let resizeObserver: ResizeObserver | null = null;

onMounted(() => {
  if (needsWidth.value) {
    measureList();
    if (listEl.value && typeof ResizeObserver !== "undefined") {
      resizeObserver = new ResizeObserver(measureList);
      resizeObserver.observe(listEl.value);
    }
  }
  measureFlexible();
  // The first measure may run on the fallback font.
  void document.fonts?.ready.then(measureFlexible);
});
onUnmounted(() => resizeObserver?.disconnect());
watch(() => [props.items, props.columns], measureFlexible, {
  flush: "post",
});

const minWidth = computed(() => {
  const shown = shownColumns.value;
  if (!shown) return undefined;
  let total = (hasMarker.value ? MARKER_COLUMN : 0) + ROW_PADDING;
  shown.forEach((column, index) => {
    total += isFlexible(column)
      ? (measured.value[column.key]?.hold ?? 0)
      : gapOf(index) + parseFloat(column.width ?? "0");
  });
  return `${total}px`;
});

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}

// The row of a list is the toggle, as before. In a list of plain rows the
// marker is (see the template), and the row still opens by a press anywhere
// but on a control: a press on a role or a name follows the link and leaves
// the row as it is, and the marker toggles by itself.
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
  <div
    ref="listEl"
    class="expandable-list"
    :class="{
      'expandable-list--columns': columns,
      'expandable-list--baseline': columns && rowAlign === 'baseline',
    }"
    :style="minWidth ? { '--expandable-min-width': minWidth } : undefined"
  >
    <div v-if="shownColumns" class="expandable-header">
      <span v-if="hasMarker" class="icon-cell" aria-hidden="true" />
      <span
        v-for="column in shownColumns"
        :key="column.key"
        class="header-cell"
        :style="cellStyle(column)"
        :data-flexible="isFlexible(column) ? column.key : undefined"
      >
        {{ column.label }}
      </span>
    </div>

    <template v-for="(item, index) in items" :key="item.id">
      <!-- Row: multi-column. The row is the toggle; in a list of plain rows
           the marker is, and a row with nothing to open is a plain row. -->
      <div
        v-if="shownColumns"
        :id="`expandable-toggle-${item.id}`"
        class="expandable-row expandable-row--grid"
        :class="{
          expanded: isOpen(item),
          'expandable-row--static': !opens(item),
        }"
        :role="plainRows ? undefined : 'button'"
        :tabindex="plainRows ? undefined : 0"
        :aria-expanded="plainRows ? undefined : isExpanded(item.id)"
        :aria-controls="plainRows ? undefined : `expandable-details-${item.id}`"
        @click="onRowClick($event, item)"
        @keydown="onRowKeydown($event, item)"
      >
        <span
          v-if="!plainRows"
          class="expand-icon expand-marker"
          :class="{ expanded: isExpanded(item.id) }"
          aria-hidden="true"
        />
        <span v-else-if="hasMarker" class="expand-icon">
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
        </span>
        <span
          v-for="(column, columnIndex) in shownColumns"
          :id="
            plainRows && columnIndex === 0
              ? `expandable-name-${item.id}`
              : undefined
          "
          :key="column.key"
          class="row-cell"
          :class="{ 'row-cell--bold': column.bold }"
          :style="cellStyle(column)"
          :data-flexible="isFlexible(column) ? column.key : undefined"
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

      <!-- Details: 3-level grid animation -->
      <div
        class="details-grid"
        :class="{ open: isOpen(item) }"
        :inert="!isOpen(item)"
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
@use "@/assets/styles/Animations" as *
@use "@/assets/styles/Tables" as *

// --- Container ---
// Override +table's gap/background border trick with explicit borders.
// Gap-based borders are incompatible with accordion animation.
.expandable-list
  +table
  gap: 0
  padding: 0
  background-color: transparent
  border: $table-gap solid $border

// A list with columns keeps every column at any width (UI_STANDARDS, "Table
// UX Standards"), in the row. Only where the page column is narrower than the
// columns need does the list scroll sideways in its own box, the last resort,
// as a data table does; the page does not move.
// The box is a size container, so the width of the rows does not reach its
// ancestors. The details of a row are read, not scanned across: they stay the
// width of the box and stick to its left edge while the row above them
// scrolls.
.expandable-list--columns
  overflow-x: auto
  container-type: inline-size

  > *
    min-width: var(--expandable-min-width)

  .details-clip
    position: sticky
    left: 0
    width: 100cqi

// The hidden copy of a cell the list measures its longest word by: out of
// the flow and laid out as narrow as its words allow.
.measure-copy
  position: absolute
  visibility: hidden
  width: min-content

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

// Inline flow, not flex: the marker span carries no text (the triangle is
// pseudo content, see disclosureMarker.spec.ts), but a flex row still
// serializes it as a line of its own, so the row copied as a newline and then
// its title. As inline-blocks the marker and the title copy as one line, and
// the 12px marker column plus the former flex gap are the marker's own width
// and its right margin.
.expandable-row--title
  display: block

  > .expand-icon
    margin-right: $small

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

// A row without a marker starts with its first cell, flush with the row's
// padding, as a plain table row does. The measuring copy stands in front of
// the cell it copies, so it is the first child exactly when the cell is.
.expandable-header > .header-cell:first-child,
.expandable-row--grid > .row-cell:first-child
  padding-left: 0

// Cells of several lines line up on their first line, and the marker with it.
.expandable-list--baseline
  .expandable-row--grid > .expand-icon,
  .expandable-row--grid > .row-cell
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
    font-weight: 600

// A row of a list of plain rows with nothing to open: a plain table row, as
// the staff table was, with no pointer and no hover that would promise a
// press.
.expandable-row--static
  cursor: auto
  &:hover
    background-image: none

// The marker of a list of plain rows is a button: its rows hold links, and a
// link inside role="button" is no link to a screen reader (a button's
// children are presentational) and gives its Enter away to the row. The
// button is the user agent's box taken off, the glyph is .expand-marker's,
// and the ring is the row's own ring, drawn when the marker has the focus.
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
  outline: 2px solid $link
  outline-offset: -2px

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
</style>
