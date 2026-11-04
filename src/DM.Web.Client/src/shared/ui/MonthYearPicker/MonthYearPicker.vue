<script setup lang="ts">
// The trigger, the stepper pill and the popover around MonthYearGrid. The popover stands in a fixed layer:
// inside the picker it was cut by any box that clips, a sideways-scrolling row among them.
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

// Capitalized: a trigger label stands alone, as every control label does.

const isOpen = ref(false);
const rootRef = ref<HTMLElement | null>(null);
const triggerRef = ref<HTMLButtonElement | null>(null);

const maxY = computed(() => props.maxYear ?? props.year);
const maxM = computed(() => props.maxMonth ?? 12);
const minY = computed(() => props.minYear ?? maxY.value - 11);

const label = computed(() =>
  props.mode === "year"
    ? `${props.year}`
    : `${RU_MONTHS_CAPITALIZED[(props.month ?? 1) - 1]} ${props.year}`,
);

// The widest month label reserves the pill's middle, so the › arrow never moves under a clicking cursor.
const LONGEST_MONTH = RU_MONTHS_CAPITALIZED.reduce((a, b) =>
  b.length > a.length ? b : a,
);
const sizerLabel = computed(() =>
  props.mode === "year" ? label.value : `${LONGEST_MONTH} ${props.year}`,
);

const popoverRef = ref<HTMLElement | null>(null);
const popoverStyle = ref<Record<string, string>>({});

// For the first frame, before the panel can be measured.
const FALLBACK_WIDTH = 220;
const FALLBACK_HEIGHT = 200;

/** Under the picker, above it without room below, pushed back inside the window at either side. */
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

// Placed above the picker, a panel that shrank would float off it.
let resizeObserver: ResizeObserver | null = null;

watch(isOpen, (opened) => {
  if (opened) {
    // Passive: the listener only reads geometry, and a blocking one stutters the scroll.
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
  // Into the dialog, or Tab from the trigger lands behind the popover.
  await nextTick();
  updatePosition(); // the panel measured
  const target =
    rootRef.value?.querySelector<HTMLButtonElement>(".myp-cell.selected") ??
    rootRef.value?.querySelector<HTMLButtonElement>(".myp-cell:enabled");
  target?.focus();
}
/** @param restoreFocus On Esc and on pick, where focus would drop on a detached element. */
function close(restoreFocus = false) {
  isOpen.value = false;
  if (restoreFocus) triggerRef.value?.focus();
}
function toggle() {
  if (isOpen.value) close();
  else open();
}

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
  <!-- Inline flow and zero-width spaces: a selection copies "‹ Июль 2026 ›" on one line. -->
  <div
    ref="rootRef"
    class="month-year-picker"
    :class="{ 'with-stepper': stepper }"
  >
    <!-- The bordered box is this inner span, never the root: the pill clips, and it sliced the popover. -->
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
        <!-- The width is reserved by ::before reading data-sizer: pseudo-content never enters a copy. -->
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
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/Filters" as *

// Inline-block, not flex: a selection copies in one line.
.month-year-picker
  display: inline-block
  white-space: nowrap
  vertical-align: middle

  // One bordered pill of three sections, the anatomy of the segmented control.
  &.with-stepper .myp-field
    display: inline-block
    // Top-aligned: the pill's baseline is its bottom edge, and the strut's descender would grow the strip.
    vertical-align: top
    height: $control-height
    box-sizing: border-box
    border: 1px solid $border
    border-radius: $border-radius
    overflow: hidden
    background-color: $bg-element

// Without the stepper: the regular button idiom.
.myp-trigger
  vertical-align: middle
  +button

// The middle of the pill, reserved by the hidden sizer.
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

  // Inward: the pill's overflow: hidden would clip a ring on its edge.
  &:focus-visible
    +focus-ring($inward: true)

// A zero-height ::before reserves the width: an out-of-flow box would copy as a line of its own.
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
    +focus-ring($inward: true)

// At the coordinates updatePosition reads off the picker (top and left inline).
.myp-popover
  position: fixed
  z-index: $z-dropdown
  width: 220px
  padding: $small
  // The site's one floating panel: ground, frame, corners and shadow.
  +dropdown-surface
</style>
