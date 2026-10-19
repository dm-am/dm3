<script setup lang="ts">
// Typed text that is not a date the field takes stays, and the field says why (the consumer shows it).
// The popover is fixed, to escape overflow-clipped containers, and follows the field on scroll, resize and its own height.
import { ref, watch, onMounted, onUnmounted, nextTick } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";
import CalendarGrid from "./CalendarGrid.vue";
import { boundReason, reasonSayer, shapeInput } from "./typedField";
import { formatDate } from "@/shared/lib/utils/datetime";
import { symbols } from "@/shared/lib/utils/icons";
import { POPOVER_GAP, VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";

const props = withDefaults(
  defineProps<{
    /** Selected date, YYYY-MM-DD (null for none). */
    modelValue: string | null;
    /** Earliest selectable date, YYYY-MM-DD. Earlier days are disabled. */
    min?: string;
    /** Latest selectable date, YYYY-MM-DD. Later days are disabled. */
    max?: string;
    /** Input placeholder. */
    placeholder?: string;
    /** Accessible label for the text input. */
    ariaLabel?: string;
    /** The id of the text input, for a caption that points at it. */
    inputId?: string;
    /** A cross clears the date; off where a date must be given. */
    clearable?: boolean;
    /** No calendar of its own: a field of a range drawn by DateRangePicker. */
    bare?: boolean;
    /** As wide as the calendar it opens, not the whole column of a form. */
    narrow?: boolean;
    /** The grid an empty field opens its calendar on. */
    emptyLevel?: "days" | "years";
    /** The day an empty field opens its calendar on, YYYY-MM-DD; today if absent. */
    openOn?: string;
  }>(),
  {
    min: undefined,
    max: undefined,
    placeholder: "дд.мм.гггг",
    ariaLabel: undefined,
    inputId: undefined,
    clearable: true,
    emptyLevel: "days",
    openOn: undefined,
  },
);

const emit = defineEmits<{
  "update:modelValue": [value: string | null];
  /** The down arrow in a `bare` field: the calendar beside it is asked for. */
  "focus-calendar": [];
  /** Why the text in the field is not a date it takes; null once it is. */
  reason: [value: string | null];
}>();

/** A field in a window takes the window's measure (see the styles). */
const inDialog = useInDialog();

const rootRef = ref<HTMLElement | null>(null);
const fieldRef = ref<HTMLElement | null>(null);
const inputRef = ref<HTMLInputElement | null>(null);
const popoverRef = ref<InstanceType<typeof CalendarGrid> | null>(null);

const isOpen = ref(false);

/** YYYY-MM-DD → DD.MM.YYYY for display. */
function formatForDisplay(value: string | null | undefined): string {
  return formatDate(value, "");
}

/**
 * Parse manual input: DD.MM.YYYY (day/month may be 1 digit) or YYYY-MM-DD.
 * Returns normalized YYYY-MM-DD, or null when the text is not a real date
 * (including overflow dates like 31.02).
 */
function parseText(raw: string): string | null {
  const t = raw.trim();
  let day: number;
  let month: number;
  let year: number;
  let m = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(t);
  if (m) {
    day = +m[1];
    month = +m[2];
    year = +m[3];
  } else {
    m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(t);
    if (!m) return null;
    year = +m[1];
    month = +m[2];
    day = +m[3];
  }
  const d = new Date(year, month - 1, day);
  if (
    d.getFullYear() !== year ||
    d.getMonth() !== month - 1 ||
    d.getDate() !== day
  ) {
    return null;
  }
  const mm = String(month).padStart(2, "0");
  const dd = String(day).padStart(2, "0");
  return `${year}-${mm}-${dd}`;
}

/** A date the calendar would let the reader pick. */
function inBounds(value: string): boolean {
  if (props.min && value < props.min) return false;
  if (props.max && value > props.max) return false;
  return true;
}

/** Where the dots of "дд.мм.гггг" stand. */
const DOT_AT = [2, 5];

/** Dots put into a run of digits ("140319" is "14.03.19"); text the reader shaped is left alone. */
function withDots(raw: string): string {
  if (!/^[\d.]*$/.test(raw)) return raw;
  const digits = raw.replace(/\./g, "");
  if (digits.length > 8) return raw;
  for (let i = 0; i < raw.length; i++) {
    if (raw[i] === "." && !DOT_AT.includes(i)) return raw;
  }
  return [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4)]
    .filter(Boolean)
    .join(".");
}

const text = ref(formatForDisplay(props.modelValue));

/** Why the text is not a date the field takes, or null when it is one. */
function reasonFor(t: string): string | null {
  const trimmed = t.trim();
  if (!trimmed) return null;
  const parsed = parseText(trimmed);
  if (!parsed) return "Такой даты нет";
  return boundReason(parsed, props.min, props.max, formatForDisplay);
}

const setReason = reasonSayer((next) => emit("reason", next));

watch(
  () => props.modelValue,
  (val) => {
    // Typing that already parses to this value keeps its caret.
    if (parseText(text.value) !== (val || null)) {
      text.value = formatForDisplay(val);
      setReason(reasonFor(text.value));
    }
  },
);

// Bounds moving under a date already in the field are said at once; half-typed text waits for blur.
watch([() => props.min, () => props.max], () => {
  const next = reasonFor(text.value);
  if (next === null || document.activeElement !== inputRef.value)
    setReason(next);
});

function commit(t: string) {
  const trimmed = t.trim();
  if (!trimmed) {
    if (props.modelValue !== null) emit("update:modelValue", null);
    return;
  }
  const parsed = parseText(trimmed);
  if (parsed && inBounds(parsed) && parsed !== props.modelValue) {
    emit("update:modelValue", parsed);
  }
}

function onInput(e: Event) {
  const shaped = shapeInput(e.target as HTMLInputElement, withDots);
  text.value = shaped;
  commit(shaped);
  // A reason goes as soon as the text is right; a new one waits for the reader to leave.
  if (reasonFor(shaped) === null) setReason(null);
}

/** On leaving: a taken date is written out in full, a refused text stays with its reason. */
function settleText() {
  const next = reasonFor(text.value);
  setReason(next);
  if (next === null) text.value = formatForDisplay(props.modelValue);
}

function clear() {
  text.value = "";
  setReason(null);
  if (props.modelValue !== null) emit("update:modelValue", null);
  inputRef.value?.focus();
}

const popoverStyle = ref<Record<string, string>>({});

// The first frame's size, before the popover is measurable; the width is $calendar-width (_calendar.sass).
const FALLBACK_WIDTH = 266;
const FALLBACK_HEIGHT = 290;

function popoverEl(): HTMLElement | null {
  return (popoverRef.value?.$el as HTMLElement | undefined) ?? null;
}

function updatePosition() {
  const field = fieldRef.value;
  if (!field) return;
  const rect = field.getBoundingClientRect();
  const el = popoverEl();
  const width = el?.offsetWidth || FALLBACK_WIDTH;
  const height = el?.offsetHeight || FALLBACK_HEIGHT;
  const gap = POPOVER_GAP;
  const edge = VIEWPORT_EDGE;
  const vw = document.documentElement.clientWidth;
  const vh = document.documentElement.clientHeight;

  let top = rect.bottom + gap;
  if (top + height > vh - edge) {
    const above = rect.top - gap - height;
    top = above >= edge ? above : Math.max(edge, vh - edge - height);
  }

  let left = rect.left;
  if (left + width > vw - edge) left = vw - edge - width;
  if (left < edge) left = edge;

  popoverStyle.value = { top: `${top}px`, left: `${left}px` };
}

function open() {
  if (isOpen.value || props.bare) return;
  isOpen.value = true;
  updatePosition(); // estimate before first paint
  nextTick(updatePosition); // refine with the real popover size
}

function close() {
  isOpen.value = false;
}

function toggle() {
  if (isOpen.value) close();
  else open();
}

function stopTracking() {
  document.removeEventListener("scroll", updatePosition, { capture: true });
  window.removeEventListener("resize", updatePosition);
}

watch(isOpen, (opened) => {
  if (!opened) return stopTracking();
  // Passive: updatePosition only reads geometry, and a blocking scroll listener stutters.
  document.addEventListener("scroll", updatePosition, {
    passive: true,
    capture: true,
  });
  window.addEventListener("resize", updatePosition);
});

// Placed above the field, the panel has to follow its own height.
let resizeObserver: ResizeObserver | null = null;

watch(
  popoverRef,
  (grid) => {
    resizeObserver?.disconnect();
    resizeObserver = null;
    const el = (grid?.$el as HTMLElement | undefined) ?? null;
    if (el && typeof ResizeObserver !== "undefined") {
      resizeObserver = new ResizeObserver(() => updatePosition());
      resizeObserver.observe(el);
    }
  },
  { flush: "post" },
);

function onPick(value: string) {
  if (value !== props.modelValue) emit("update:modelValue", value);
  text.value = formatForDisplay(value);
  setReason(null);
  close();
  inputRef.value?.focus();
}

function onInputClick() {
  open();
}

function onInputKeydown(e: KeyboardEvent) {
  if (e.key === "ArrowDown") {
    e.preventDefault();
    if (props.bare) {
      emit("focus-calendar");
      return;
    }
    open();
    nextTick(() => popoverRef.value?.focusGrid());
  } else if (e.key === "Enter") {
    // Only here, not on the root: on a day of the grid Enter is the pick.
    e.preventDefault();
    settleText();
    close();
  }
}

function onRootKeydown(e: KeyboardEvent) {
  if (e.key === "Escape" && isOpen.value) {
    // Esc with an open calendar must not also close the filter or window around it.
    e.stopPropagation();
    const fromGrid = !!popoverEl()?.contains(document.activeElement);
    close();
    if (fromGrid) inputRef.value?.focus();
  }
}

function onDocClick(e: MouseEvent) {
  if (
    isOpen.value &&
    rootRef.value &&
    !rootRef.value.contains(e.target as Node)
  ) {
    close();
  }
}

onMounted(() => {
  document.addEventListener("click", onDocClick);
  // A date handed in out of bounds is said at once, as one moved out later.
  setReason(reasonFor(text.value));
});
onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  stopTracking();
  resizeObserver?.disconnect();
});
</script>

<template>
  <div
    ref="rootRef"
    class="date-input-control"
    :class="{
      'date-input-control--bare': bare,
      'date-input-control--narrow': narrow,
      'date-input-control--dialog': inDialog,
    }"
    @keydown="onRootKeydown"
  >
    <div ref="fieldRef" class="di-field">
      <input
        :id="inputId"
        ref="inputRef"
        :value="text"
        type="text"
        class="di-input"
        inputmode="numeric"
        autocomplete="off"
        :placeholder="placeholder"
        :aria-label="ariaLabel"
        @input="onInput"
        @keydown="onInputKeydown"
        @blur="settleText"
        @click="onInputClick"
      />
      <button
        v-if="clearable && !bare && text"
        type="button"
        class="di-clear"
        aria-label="Очистить дату"
        @click.stop="clear"
      >
        {{ symbols.close }}
      </button>
      <button
        v-if="!bare"
        type="button"
        class="di-toggle"
        :class="{ active: isOpen }"
        aria-label="Открыть календарь"
        :aria-expanded="isOpen"
        @click.stop="toggle"
      >
        <SvgIcon name="calendar" />
      </button>
    </div>

    <CalendarGrid
      v-if="isOpen"
      ref="popoverRef"
      class="di-popover"
      :style="popoverStyle"
      :model-value="modelValue"
      :min="min"
      :max="max"
      :initial-level="modelValue ? 'days' : emptyLevel"
      :open-on="openOn"
      @update:model-value="onPick"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/shared/ui/DatePicker/calendar" as *
@use "@/assets/styles/DialogSurface" as *

.date-input-control
  position: relative

// A date of its own on a page (a birthday) is ten characters long: a field as
// wide as the column left the icon at one end and the calendar at the other.
// As wide as the calendar, the icon stands over the panel's right edge.
.date-input-control--narrow
  width: $calendar-width
  max-width: 100%

// On a page: the height of every control of the page, frame included, and the
// type and inset of every field of one line.
.di-field
  display: flex
  align-items: center
  box-sizing: border-box
  height: $control-height
  border: 1px solid $border
  border-radius: $border-radius
  // Relative fill (not solid $bg-element): the field usually sits ON the
  // $bg-element dropdown panel — a solid fill would blend to zero delta
  background-color: $input-bg-overlay

  &:focus-within
    border-color: $link
    box-shadow: 0 0 0 1px $link

.di-input
  flex: 1
  min-width: 0
  width: 100%
  height: 100%
  padding: 0 $tiny 0 $field-inset
  font-size: $control-font-size
  font-family: inherit
  border: none
  background: none
  color: $text
  outline: none

  // The frame is drawn by the wrapper, which takes the ring on :focus-within;
  // a second ring here would sit inside the first.
  &:focus
    box-shadow: none

  &::placeholder
    color: $text-muted

.di-clear,
.di-toggle
  display: flex
  align-items: center
  justify-content: center
  align-self: stretch
  width: 26px
  padding: 0
  flex-shrink: 0
  border: none
  background: none
  color: $text-muted
  cursor: pointer

// The cross of the search field, in the box of the calendar icon beside it.
.di-clear
  font-family: inherit
  font-size: $font-size

  &:hover
    color: $text

.di-toggle
  opacity: $muted-opacity

  &:hover,
  &.active
    opacity: 1

  svg
    width: 15px
    height: 15px

.di-popover
  position: fixed
  z-index: $z-dropdown

// In a window the field is a field of the window: the 52 of its controls and
// the 15 of its type, 14 in from the frame, as FormField sets every field of
// one line there (_DialogSurface.sass). The frame is this box and not the
// input, so FormField's rule for the input it holds does not reach it.
.date-input-control--dialog
  .di-field
    height: $dialog-control-height

  .di-input
    padding-left: $dialog-field-inset
    font-size: $dialog-field-font-size

// Nothing stands after the text of a field of a range: it keeps the inset of
// the frame on both sides.
.date-input-control--bare .di-input
  padding-right: $small
</style>
