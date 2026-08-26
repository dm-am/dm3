<script setup lang="ts">
/**
 * DateInput — text field with a calendar popover for picking a single date.
 *
 * Site-styled replacement for native <input type="date"> in filter panels:
 * manual typing keeps working (DD.MM.YYYY or YYYY-MM-DD, parsed as you
 * type), and the calendar button opens the shared CalendarGrid. Emits
 * YYYY-MM-DD, or null when cleared.
 *
 * The popover uses position:fixed so it escapes overflow-clipped containers
 * (filter dropdowns scroll internally). Coordinates are computed from the
 * field rect, flipped above when there is no room below, clamped to the
 * viewport, and refreshed on scroll/resize while open.
 */
import { ref, watch, onMounted, onUnmounted, nextTick } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import CalendarGrid from "./CalendarGrid.vue";
import { formatDate } from "@/shared/lib/utils/datetime";
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
  }>(),
  { placeholder: "дд.мм.гггг" },
);

const emit = defineEmits<{ "update:modelValue": [value: string | null] }>();

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

const text = ref(formatForDisplay(props.modelValue));

watch(
  () => props.modelValue,
  (val) => {
    // Don't clobber in-progress typing that already parses to this value
    // (keeps the caret position; the text is normalized on blur anyway)
    if (parseText(text.value) !== (val || null)) {
      text.value = formatForDisplay(val);
    }
  },
);

function onInput() {
  const t = text.value.trim();
  if (!t) {
    if (props.modelValue !== null) emit("update:modelValue", null);
    return;
  }
  const parsed = parseText(t);
  if (parsed && parsed !== props.modelValue) {
    emit("update:modelValue", parsed);
  }
}

/** Snap the display text back to the committed value (reverts partial input). */
function normalizeText() {
  text.value = formatForDisplay(props.modelValue);
}

// ---------------------------------------------------------------------------
// Popover open/close + fixed positioning
// ---------------------------------------------------------------------------

const popoverStyle = ref<Record<string, string>>({});

// Fallback size for the first frame, before the popover is measurable.
// CalendarGrid is 248px wide (content-box) + padding/border ≈ 266×290.
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
  if (isOpen.value) return;
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

function onPick(value: string) {
  if (value !== props.modelValue) emit("update:modelValue", value);
  text.value = formatForDisplay(value);
  close();
  inputRef.value?.focus();
}

function onRootKeydown(e: KeyboardEvent) {
  if (e.key === "Escape" && isOpen.value) {
    // Swallow the event: Esc with an open calendar must not also close
    // the surrounding filter dropdown
    e.stopPropagation();
    close();
  } else if (e.key === "Enter") {
    e.preventDefault();
    normalizeText();
    close();
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
});
onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  document.removeEventListener("scroll", updatePosition, true);
  window.removeEventListener("resize", updatePosition);
});
</script>

<template>
  <div ref="rootRef" class="date-input-control" @keydown="onRootKeydown">
    <div ref="fieldRef" class="di-field">
      <input
        ref="inputRef"
        v-model="text"
        type="text"
        class="di-input"
        inputmode="numeric"
        autocomplete="off"
        :placeholder="placeholder"
        :aria-label="ariaLabel"
        @input="onInput"
        @blur="normalizeText"
        @click="open"
      />
      <button
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
      @update:model-value="onPick"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

.date-input-control
  position: relative

.di-field
  display: flex
  align-items: center
  box-sizing: border-box
  border: 1px solid $border
  border-radius: $border-radius
  // Relative fill (not solid $bg-element): the field usually sits ON the
  // $bg-element dropdown panel — a solid fill would blend to zero delta
  background-color: $input-bg-overlay

  &:focus-within
    border-color: $border-focus

.di-input
  flex: 1
  min-width: 0
  width: 100%
  padding: $small
  padding-right: $tiny
  font-size: $secondary-font-size
  font-family: inherit
  border: none
  background: none
  color: $text
  outline: none

  &::placeholder
    color: $text-muted

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
</style>
