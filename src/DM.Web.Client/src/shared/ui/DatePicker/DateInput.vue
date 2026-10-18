<script setup lang="ts">
/**
 * DateInput — text field with a calendar popover for picking a single date.
 *
 * Site-styled replacement for native <input type="date">: manual typing keeps
 * working (DD.MM.YYYY or YYYY-MM-DD, parsed as you type), and the calendar
 * button opens the shared CalendarGrid. Emits YYYY-MM-DD, or null when
 * cleared. A typed date outside [min, max] is not taken, exactly as a date
 * that does not exist is not: the calendar would not let it be picked either.
 *
 * What was typed and not taken stays in the field, and the field says why:
 * leaving it with "31.02.2026" in it emits the reason "Такой даты нет", and a
 * date out of bounds "Не раньше 05.10.2026" or "Не позже 05.10.2026". The
 * field used to wipe such a text without a word, and the reader saw the date
 * they had typed vanish. The reason is the consumer's to show (FormField's
 * `errors`, or a line under a pair of fields): it is a red line of 14 under
 * the field, and the frame stays as it is (the owner's standard of 27
 * September 2026). A reason clears the moment the text makes a date again,
 * and is asked anew when the bounds move under a date already in the field.
 *
 * Digits separate themselves: "14031991" reads "14.03.1991" as it is typed.
 * A phone's numeric keyboard often has no dot at all, and on a desktop the
 * dots were three keystrokes a date that nobody wanted to type. A dot the
 * reader types in a place of their own ("9.5.2025") is left alone.
 *
 * When the field holds a date, a cross before the calendar icon clears it,
 * as the cross of the search field does; a date that must be given
 * (`clearable` off, the dates of a poll) has no cross.
 *
 * The keyboard stays in the field when the calendar opens; the down arrow
 * moves it into the grid, Escape closes the calendar and gives it back.
 *
 * A touch is not a click. On a phone the field opened its calendar and, with
 * the same tap, the numeric keyboard, which rose over the calendar and hid it.
 * Now the first tap opens the calendar with the keyboard down, and a second
 * tap on the field closes the calendar and gives the keyboard, for a reader
 * who would rather type. The mouse and the keyboard are as they were.
 *
 * On a page it is a field of the page: $control-height high, the reader
 * types in the 14 of $control-font-size, as in every field of one line. In a
 * window it is a field of the window, 52 high with the text at 15, as every
 * field FormField draws there. A field of a range (`bare`) has no calendar of its own:
 * DateRangePicker draws one under both fields, and the down arrow asks it for
 * the keyboard.
 *
 * The popover uses position:fixed so it escapes overflow-clipped containers.
 * Coordinates are computed from the field rect, flipped above when there is
 * no room below, clamped to the viewport, and refreshed on scroll/resize and
 * whenever the calendar changes height (a month of four weeks after one of
 * six, the grid of months after the grid of days) while open.
 */
import { ref, watch, onMounted, onUnmounted, nextTick } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";
import CalendarGrid from "./CalendarGrid.vue";
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

/**
 * The typed text with its dots put in: "140319" is "14.03.19". Only a run of
 * digits, or one whose dots already stand where these would, is touched;
 * anything else is text the reader shaped themselves. A trailing dot is not
 * added, so backspace deletes a digit and not a dot the field puts back.
 */
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

/** The caret position just after the n-th digit of the text. */
function afterDigits(text: string, count: number): number {
  if (count === 0) return 0;
  const nth = [...text.matchAll(/\d/g)][count - 1];
  return nth?.index === undefined ? text.length : nth.index + 1;
}

const text = ref(formatForDisplay(props.modelValue));

/** Why the text is not a date the field takes, or null when it is one. */
function reasonFor(t: string): string | null {
  const trimmed = t.trim();
  if (!trimmed) return null;
  const parsed = parseText(trimmed);
  if (!parsed) return "Такой даты нет";
  if (props.min && parsed < props.min)
    return `Не раньше ${formatForDisplay(props.min)}`;
  if (props.max && parsed > props.max)
    return `Не позже ${formatForDisplay(props.max)}`;
  return null;
}

/** The reason said last, so each change is said once. */
const reason = ref<string | null>(null);

function setReason(next: string | null) {
  if (next === reason.value) return;
  reason.value = next;
  emit("reason", next);
}

watch(
  () => props.modelValue,
  (val) => {
    // Don't clobber in-progress typing that already parses to this value
    // (keeps the caret position; the text is normalized on blur anyway)
    if (parseText(text.value) !== (val || null)) {
      text.value = formatForDisplay(val);
      setReason(reasonFor(text.value));
    }
  },
);

// The bounds move under a date already in the field (the end of a period
// whose start moved past it): the field says so at once. A field being typed
// into is only ever cleared here; what is half typed is judged on leaving it.
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
  const input = e.target as HTMLInputElement;
  const raw = input.value;
  const shaped = withDots(raw);
  if (shaped !== raw) {
    // The caret keeps its place among the digits, wherever the dots moved.
    const caret = input.selectionStart ?? raw.length;
    const digitsBefore = raw.slice(0, caret).replace(/\D/g, "").length;
    input.value = shaped;
    const at = afterDigits(shaped, digitsBefore);
    input.setSelectionRange(at, at);
  }
  text.value = shaped;
  commit(shaped);
  // A reason goes the moment the text is right again; a new one waits for
  // the reader to leave the field, and is not said of a date half typed.
  if (reasonFor(shaped) === null) setReason(null);
}

/**
 * The reader is done with the text: a date it takes is written out in full
 * ("9.5.2025" reads "09.05.2025"), and a text it does not take stays as it
 * was typed, with the reason said.
 */
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

// ---------------------------------------------------------------------------
// Popover open/close + fixed positioning
// ---------------------------------------------------------------------------

const popoverStyle = ref<Record<string, string>>({});

// Fallback size for the first frame, before the popover is measurable: the
// panel's outer width ($calendar-width) and about five weeks of days.
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
  // Both from the shared scale rather than written here: the comment that used
  // to say "matches $tiny offset of other dropdowns" was true only until $tiny
  // moved, and the eight below was the same number the tooltip keeps under a
  // name of its own.
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

watch(isOpen, (opened) => {
  // Track any scrolling container (capture phase) while the popover is open
  if (opened) {
    // Passive: updatePosition only READS geometry (getBoundingClientRect,
    // offsetWidth), and a non-passive scroll listener makes the browser wait
    // for it before every frame — a forced layout on the scroll path, felt as
    // stutter while the popover is open. The same options the tooltip's
    // listener in this same layer already carries.
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

// The calendar changes height with the month and with the grid on view. Placed
// above the field, a panel that grew would cover the field and one that shrank
// would float off it, so the position follows the panel's own size.
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

// ---------------------------------------------------------------------------
// Touch: the calendar first, the keyboard on a second tap
// ---------------------------------------------------------------------------

/**
 * The keyboard is held down: the calendar was opened by a touch. "none" is
 * the input mode that keeps a phone's keyboard from rising on focus; the
 * field is still focused, the caret stands in it, and a second tap gives the
 * numeric keyboard back.
 */
const keyboardDown = ref(false);

/** The press in progress is a finger or a pen, and not a mouse. */
let touchPress = false;
/** The calendar was open when that press began. */
let openAtPress = false;

function holdKeyboard(down: boolean) {
  keyboardDown.value = down;
  // Set on the node at once and not on the next render: the focus that the
  // same tap brings comes before Vue patches, and it reads the mode it finds.
  if (inputRef.value) inputRef.value.inputMode = down ? "none" : "numeric";
}

function onInputPointerdown(e: PointerEvent) {
  touchPress =
    !props.bare && (e.pointerType === "touch" || e.pointerType === "pen");
  if (!touchPress) {
    if (keyboardDown.value) holdKeyboard(false);
    return;
  }
  openAtPress = isOpen.value;
  holdKeyboard(!openAtPress);
}

function onInputClick() {
  if (touchPress && openAtPress) {
    // The second tap: the reader wants to type, and the calendar would
    // stand under the keyboard.
    touchPress = false;
    close();
    return;
  }
  touchPress = false;
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
    // The field's own Enter: take what is typed and close. Only here, and
    // not on the root: on a day of the grid, Enter is the pick.
    e.preventDefault();
    settleText();
    close();
  }
}

function onRootKeydown(e: KeyboardEvent) {
  if (e.key === "Escape" && isOpen.value) {
    // Swallow the event: Esc with an open calendar must not also close
    // the surrounding filter dropdown or window
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
  document.removeEventListener("scroll", updatePosition, true);
  window.removeEventListener("resize", updatePosition);
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
        :inputmode="keyboardDown ? 'none' : 'numeric'"
        autocomplete="off"
        :placeholder="placeholder"
        :aria-label="ariaLabel"
        @input="onInput"
        @keydown="onInputKeydown"
        @blur="settleText"
        @pointerdown="onInputPointerdown"
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

// The cross of the search field, in the box of the calendar icon beside it.
.di-clear
  display: flex
  align-items: center
  justify-content: center
  align-self: stretch
  width: 26px
  padding: 0
  flex-shrink: 0
  border: none
  background: none
  font-family: inherit
  font-size: $font-size
  color: $text-muted
  cursor: pointer

  &:hover
    color: $text

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
  opacity: $muted-opacity
  cursor: pointer

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
