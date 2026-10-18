<script setup lang="ts">
/**
 * MonthYearPicker — a compact month/year selector popover in the site's
 * calendar style (no day grid). Two modes:
 *  - "month": year navigation (‹ year ›) + a 12-month grid.
 *  - "year": block navigation (‹ range ›) + a 12-year grid — years are
 *    paged in fixed 12-year blocks anchored at maxYear (mirrors the
 *    12-cell month grid), the oldest block may be partial.
 * Emits the chosen year (and month, in month mode) and closes on pick,
 * outside-click, or Esc. Future months/years past max* are disabled;
 * both modes bottom out at minYear.
 *
 * The panel itself is MonthYearGrid — shared with the day calendar, which
 * shows the same two grids above its day grid. What stays here is the part a
 * grid has no business knowing: the trigger, the stepper pill, the popover
 * and when it is open.
 *
 * The popover stands in a fixed layer, as the date field's calendar and the
 * panel of a subscription do: under the picker with the gap every dropdown
 * keeps, above it when there is no room below, pushed back inside the window
 * at either side, and read again on every scroll and resize while it is open
 * and whenever it changes height. Positioned inside the picker it was cut by
 * any box around the picker that clips, and a row that scrolls sideways on a
 * phone (the period of the statistics page) clips what it scrolls.
 */
import { ref, computed, nextTick, onMounted, onUnmounted, watch } from "vue";
import { RU_MONTHS_CAPITALIZED } from "@/shared/lib/utils/months";
import { POPOVER_GAP, VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";
import MonthYearGrid from "./MonthYearGrid.vue";

const props = withDefaults(
  defineProps<{
    /** Selected year. */
    year: number;
    /** Selected month, 1-12 (month mode). */
    month?: number;
    mode?: "month" | "year";
    /** Earliest selectable year (e.g. the site's founding year). Bounds
     * both the year grid and the month mode's year navigation. Defaults
     * to 11 years before maxYear (a 12-year grid). */
    minYear?: number;
    /** Latest selectable year. */
    maxYear?: number;
    /** Latest selectable month within maxYear (1-12). */
    maxMonth?: number;
    /**
     * Render ‹ › step buttons around the trigger for one-click sequential
     * period browsing ("и что было месяцем раньше?") — the dominant archive
     * flow, much faster than reopening the popover per step. Steps clamp to
     * [minYear-01 … maxYear-maxMonth] in month mode and [minYear … maxYear]
     * in year mode.
     */
    stepper?: boolean;
  }>(),
  { mode: "month", month: 1, stepper: false },
);

const emit = defineEmits<{
  "update:year": [value: number];
  "update:month": [value: number];
}>();

// Capitalized ("Июль 2026"): a trigger label is a standalone label, not
// mid-sentence Russian — same convention as every other control label and
// the sibling CalendarGrid header.
const MONTHS_FULL = RU_MONTHS_CAPITALIZED;

const isOpen = ref(false);
const rootRef = ref<HTMLElement | null>(null);
const triggerRef = ref<HTMLButtonElement | null>(null);

const maxY = computed(() => props.maxYear ?? props.year);
const maxM = computed(() => props.maxMonth ?? 12);
const minY = computed(() => props.minYear ?? maxY.value - 11);

const label = computed(() =>
  props.mode === "year"
    ? `${props.year}`
    : `${MONTHS_FULL[(props.month ?? 1) - 1]} ${props.year}`,
);

// Width reservation for the stepper pill's middle section: month names vary
// ("Май 2026" vs "Сентябрь 2026"), and a resizing middle would shift the
// › arrow under a rapidly clicking cursor. An invisible sizer holding the
// longest month label keeps the section (and both arrows) stationary while
// stepping. Year labels are always four digits, so year mode needs no
// reservation.
const LONGEST_MONTH = MONTHS_FULL.reduce((a, b) =>
  b.length > a.length ? b : a,
);
const sizerLabel = computed(() =>
  props.mode === "year" ? label.value : `${LONGEST_MONTH} ${props.year}`,
);

// --- The popover's place, as fixed coordinates ---
const popoverRef = ref<HTMLElement | null>(null);
const popoverStyle = ref<Record<string, string>>({});

// For the first frame, before the panel can be measured: its width (the
// width of .myp-popover below) and about the height of the grid of months.
const FALLBACK_WIDTH = 220;
const FALLBACK_HEIGHT = 200;

/**
 * Under the picker, left edges together; above it when the window has no
 * room below; pushed back inside the window at either side. The arithmetic of
 * the date field's calendar (DateInput).
 */
function updatePosition() {
  const anchor = rootRef.value;
  if (!anchor) return;
  const rect = anchor.getBoundingClientRect();
  const panel = popoverRef.value;
  const width = panel?.offsetWidth || FALLBACK_WIDTH;
  const height = panel?.offsetHeight || FALLBACK_HEIGHT;
  const vw = document.documentElement.clientWidth;
  const vh = document.documentElement.clientHeight;

  let top = rect.bottom + POPOVER_GAP;
  if (top + height > vh - VIEWPORT_EDGE) {
    const above = rect.top - POPOVER_GAP - height;
    top =
      above >= VIEWPORT_EDGE
        ? above
        : Math.max(VIEWPORT_EDGE, vh - VIEWPORT_EDGE - height);
  }

  let left = rect.left;
  if (left + width > vw - VIEWPORT_EDGE) left = vw - VIEWPORT_EDGE - width;
  if (left < VIEWPORT_EDGE) left = VIEWPORT_EDGE;

  popoverStyle.value = { top: `${top}px`, left: `${left}px` };
}

// Paging the grid of years to the oldest block shortens it: placed above the
// picker, a panel that shrank would float off it.
let resizeObserver: ResizeObserver | null = null;

watch(isOpen, (opened) => {
  if (opened) {
    // Passive: the listener only reads geometry, and a blocking one on the
    // scroll path makes the browser wait for it before every frame.
    document.addEventListener("scroll", updatePosition, {
      passive: true,
      capture: true,
    });
    window.addEventListener("resize", updatePosition);
  } else {
    document.removeEventListener("scroll", updatePosition, { capture: true });
    window.removeEventListener("resize", updatePosition);
  }
});

watch(
  popoverRef,
  (panel) => {
    resizeObserver?.disconnect();
    resizeObserver = null;
    if (panel && typeof ResizeObserver !== "undefined") {
      resizeObserver = new ResizeObserver(() => updatePosition());
      resizeObserver.observe(panel);
    }
  },
  { flush: "post" },
);

async function open() {
  isOpen.value = true;
  updatePosition(); // an estimate before the first paint
  // Move keyboard focus into the dialog: the selected cell, or the first
  // enabled one. Without this, Tab from the trigger lands behind the popover.
  await nextTick();
  updatePosition(); // the panel measured
  const target =
    rootRef.value?.querySelector<HTMLButtonElement>(".myp-cell.selected") ??
    rootRef.value?.querySelector<HTMLButtonElement>(".myp-cell:enabled");
  target?.focus();
}
/**
 * @param restoreFocus Return focus to the trigger — on Esc and on pick,
 *   where focus would otherwise be dropped on a detached element. Outside
 *   clicks keep the user's chosen focus target.
 */
function close(restoreFocus = false) {
  isOpen.value = false;
  if (restoreFocus) triggerRef.value?.focus();
}
function toggle() {
  if (isOpen.value) close();
  else open();
}

// --- Trigger-side ‹ › stepper (sequential period browsing) ---
const canStepPrev = computed(() =>
  props.mode === "year"
    ? props.year > minY.value
    : props.year > minY.value || (props.month ?? 1) > 1,
);
const canStepNext = computed(() =>
  props.mode === "year"
    ? props.year < maxY.value
    : props.year < maxY.value || (props.month ?? 1) < maxM.value,
);
function stepPrev() {
  if (!canStepPrev.value) return;
  if (props.mode === "year") {
    emit("update:year", props.year - 1);
    return;
  }
  const m = props.month ?? 1;
  if (m > 1) {
    emit("update:month", m - 1);
  } else {
    emit("update:year", props.year - 1);
    emit("update:month", 12);
  }
}
function stepNext() {
  if (!canStepNext.value) return;
  if (props.mode === "year") {
    emit("update:year", props.year + 1);
    return;
  }
  const m = props.month ?? 1;
  if (m < 12) {
    emit("update:month", m + 1);
  } else {
    emit("update:year", props.year + 1);
    emit("update:month", 1);
  }
}

function onPickMonth(year: number, month: number) {
  if (year !== props.year) emit("update:year", year);
  emit("update:month", month);
  close(true);
}
function onPickYear(year: number) {
  emit("update:year", year);
  close(true);
}

function onDocClick(e: MouseEvent) {
  if (
    isOpen.value &&
    rootRef.value &&
    !rootRef.value.contains(e.target as Node)
  )
    close();
}
function onKeydown(e: KeyboardEvent) {
  if (e.key === "Escape" && isOpen.value) close(true);
}
onMounted(() => {
  document.addEventListener("click", onDocClick);
  document.addEventListener("keydown", onKeydown);
});
onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  document.removeEventListener("keydown", onKeydown);
  document.removeEventListener("scroll", updatePosition, { capture: true });
  window.removeEventListener("resize", updatePosition);
  resizeObserver?.disconnect();
});
</script>

<template>
  <!-- Inline flow (not flex) + zero-width spaces so a selection copies as
       "‹ Июль 2026 ›" on one line (Chrome serializes flex items with
       newlines). -->
  <div
    ref="rootRef"
    class="month-year-picker"
    :class="{ 'with-stepper': stepper }"
  >
    <!-- The bordered box is this inner span, never the root: when the
         popover stood inside the root, clipping it (the stepper pill clips
         its sections by radius) sliced the popover down to the height of the
         field. Same split as DateInput: root places, field draws. -->
    <span class="myp-field">
      <template v-if="stepper"
        ><button
          type="button"
          class="myp-step"
          :aria-label="mode === 'year' ? 'Предыдущий год' : 'Предыдущий месяц'"
          :disabled="!canStepPrev"
          @click.stop="stepPrev"
        >
          ‹</button
        ><span class="copy-space">{{ " " }}</span></template
      ><button
        ref="triggerRef"
        type="button"
        :class="stepper ? 'myp-trigger-section' : 'myp-trigger'"
        :data-sizer="sizerLabel"
        aria-haspopup="dialog"
        :aria-expanded="isOpen"
        @click.stop="toggle"
      >
        <!-- Width is reserved by an invisible ::before reading data-sizer
             (pseudo-content never enters a selection copy); the label
             itself is an ordinary word in the button's flow, because a box
             taken out of the flow copies as a line of its own. -->
        <span class="myp-label">{{ label }}</span></button
      ><template v-if="stepper"
        ><span class="copy-space">{{ " " }}</span
        ><button
          type="button"
          class="myp-step"
          :aria-label="mode === 'year' ? 'Следующий год' : 'Следующий месяц'"
          :disabled="!canStepNext"
          @click.stop="stepNext"
        >
          ›
        </button></template
      >
    </span>

    <div
      v-if="isOpen"
      ref="popoverRef"
      class="myp-popover"
      :style="popoverStyle"
      role="dialog"
      aria-label="Выбор периода"
    >
      <MonthYearGrid
        :mode="mode"
        :year="year"
        :month="month"
        :min-year="minY"
        :max-year="maxY"
        :max-month="maxM"
        @pick-month="onPickMonth"
        @pick-year="onPickYear"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Filters" as *

// Inline-block (not flex): a selection then copies in one line. The popover
// is placed off this box's rectangle, in a fixed layer.
.month-year-picker
  display: inline-block
  white-space: nowrap
  vertical-align: middle

  // Stepper mode: ONE bordered pill of three sections — ‹ | label | › —
  // the same pill anatomy as the segmented control ($control-height,
  // hairline dividers, rounded corners clipping the sections), so the
  // arrows read as parts of the control, not floating beside it. The pill
  // is an inner box (like DateInput's .di-field under .date-input-control)
  // because its overflow: hidden used to cut the popover, and focus moving
  // into a cell then scrolled the clipped box, showing a strip of the month
  // grid inside the field. Without the stepper there is no pill: the span
  // stays an unstyled inline box and the trigger keeps its own button look.
  &.with-stepper .myp-field
    display: inline-block
    // Top-aligned, not baseline-aligned: the pill's baseline is its bottom
    // edge (overflow: hidden), so the line strut's descender would add a
    // few px under it and the control strip would grow.
    vertical-align: top
    height: $control-height
    box-sizing: border-box
    border: 1px solid $border
    border-radius: $button-border-radius
    overflow: hidden
    background-color: $bg-element

// Standalone trigger (no stepper): the regular button idiom, which
// centers the label itself — no sizer here, nothing steps beside it.
.myp-trigger
  vertical-align: middle
  +button

// Middle section of the stepper pill: flat, hairline-divided from the
// arrow sections. Its width is reserved by the hidden sizer (widest month
// label), so stepping never resizes it and the › arrow never shifts under
// a rapidly clicking cursor.
.myp-trigger-section
  display: inline-block
  height: 100%
  vertical-align: top
  padding: 0 $medium
  text-align: center
  background-color: $bg-element
  border: none
  border-left: 1px solid $border
  border-right: 1px solid $border
  font: inherit
  font-size: $secondary-font-size
  color: $text
  cursor: pointer
  transition: background-color $transition-fast

  &:hover
    background-image: linear-gradient($hover-overlay, $hover-overlay)

  // Inward: the section fills the pill, whose overflow: hidden would clip a
  // ring drawn on its edge, and the arrow sections sit flush against it.
  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

// Width reservation: an invisible ::before with the widest label occupies
// layout (pseudo-content is excluded from selection copies, unlike a
// visibility-hidden span, whose text Range.toString still emits). It is a
// zero-height block: it reserves the width and gives the line back to the
// visible label, which stays an ordinary word in the button's flow and is
// centered by the button itself. The label used to be centered over the
// sizer out of flow, and a browser serializes an out-of-flow box as a line
// of its own — the strip copied as "‹ \nИюль 2026\n ›".
.myp-trigger-section::before
  content: attr(data-sizer)
  display: block
  height: 0
  overflow: hidden
  visibility: hidden

// Arrow sections of the stepper pill.
.myp-step
  display: inline-block
  height: 100%
  width: 32px
  vertical-align: top
  padding: 0
  background-color: $bg-element
  border: none
  font: inherit
  font-size: $font-size
  color: $link
  cursor: pointer
  line-height: 1
  transition: background-color $transition-fast

  &:hover:not(:disabled)
    background-image: linear-gradient($hover-overlay, $hover-overlay)

  &:disabled
    color: $text-muted
    cursor: default

  // Inward, for the same reason as the middle section.
  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

// At the coordinates updatePosition reads off the picker (top and left inline).
.myp-popover
  position: fixed
  z-index: $z-dropdown
  width: 220px
  padding: $small
  // The site's one floating panel: ground, frame, corners and shadow.
  +dropdown-surface
</style>
