<script setup lang="ts">
/**
 * NumericRangePicker — integer range input with +/− stepper buttons.
 *
 * Used for filtering by integer ranges (rating, counts, etc).
 * - Accepts only digits (and optional leading "-")
 * - Native number spinners hidden, replaced by explicit stepper buttons
 * - Enter applies, blur does not (avoids accidental apply)
 */
import { ref, watch, computed } from "vue";
import { FilterApplyButton } from "../primitives";
import type { NumericRangePickerProps } from "../types";

defineOptions({ name: "NumericRangePicker" });

const props = withDefaults(defineProps<NumericRangePickerProps>(), {
  minLabel: "От",
  maxLabel: "До",
  minPlaceholder: "",
  maxPlaceholder: "",
  allowNegative: false,
  step: 1,
});

const emit = defineEmits<{
  apply: [min: number | null, max: number | null];
  clear: [];
}>();

// The ids the two captions point at. Random suffix, because nothing stops a
// page from holding two range pickers and an id has to be unique on it. Same
// shape as FormField generates.
const uid = `numeric-range-${Math.random().toString(36).slice(2, 9)}`;
const minId = `${uid}-min`;
const maxId = `${uid}-max`;

// Local state for inputs (strings to preserve empty/intermediate states)
const minInput = ref(props.minValue?.toString() ?? "");
const maxInput = ref(props.maxValue?.toString() ?? "");

watch(
  () => props.minValue,
  (newVal) => {
    minInput.value = newVal?.toString() ?? "";
  },
);

watch(
  () => props.maxValue,
  (newVal) => {
    maxInput.value = newVal?.toString() ?? "";
  },
);

/** Parse input string to integer (null for empty/invalid), clamping negatives when disallowed */
function parseInput(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed || trimmed === "-") return null;
  const num = parseInt(trimmed, 10);
  if (isNaN(num)) return null;
  if (!props.allowNegative && num < 0) return 0;
  return num;
}

const parsedMin = computed(() => parseInput(minInput.value));
const parsedMax = computed(() => parseInput(maxInput.value));

const isInvalidRange = computed(() => {
  const min = parsedMin.value;
  const max = parsedMax.value;
  return min !== null && max !== null && min > max;
});

const canApply = computed(
  () =>
    !isInvalidRange.value &&
    (parsedMin.value !== props.minValue || parsedMax.value !== props.maxValue),
);

const showClear = computed(
  () => props.minValue !== null || props.maxValue !== null,
);

function handleApply() {
  emit("apply", parsedMin.value, parsedMax.value);
}

function handleClear() {
  minInput.value = "";
  maxInput.value = "";
  emit("clear");
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === "Enter") {
    event.preventDefault();
    handleApply();
  }
}

/** Block keypress for non-digit characters (allow "-" only when allowNegative & at start) */
function handleKeypress(event: KeyboardEvent, target: "min" | "max") {
  const ch = event.key;
  // Allow control keys handled by handleKeydown / browser defaults
  if (ch.length !== 1) return;
  if (/\d/.test(ch)) return;
  if (props.allowNegative && ch === "-") {
    const input = event.target as HTMLInputElement;
    const value = target === "min" ? minInput.value : maxInput.value;
    // Allow "-" only at position 0 and only once
    if (input.selectionStart === 0 && !value.startsWith("-")) return;
  }
  event.preventDefault();
}

/** Sanitize pasted content */
function handlePaste(event: ClipboardEvent, target: "min" | "max") {
  const text = event.clipboardData?.getData("text") ?? "";
  const cleaned = props.allowNegative
    ? text.replace(/[^\d-]/g, "").replace(/(?!^)-/g, "")
    : text.replace(/[^\d]/g, "");
  if (cleaned !== text) {
    event.preventDefault();
    if (target === "min") minInput.value = cleaned;
    else maxInput.value = cleaned;
  }
}

/** Increment/decrement helpers. Empty input treated as 0. */
function adjust(target: "min" | "max", delta: number) {
  const current = target === "min" ? parsedMin.value : parsedMax.value;
  const base = current ?? 0;
  let next = base + delta;
  if (!props.allowNegative && next < 0) next = 0;
  const str = next.toString();
  if (target === "min") minInput.value = str;
  else maxInput.value = str;
}

function canDecrement(target: "min" | "max"): boolean {
  if (props.allowNegative) return true;
  const current = target === "min" ? parsedMin.value : parsedMax.value;
  return (current ?? 0) > 0;
}
</script>

<template>
  <div class="numeric-range-picker">
    <div class="range-row">
      <label class="range-label" :for="minId">{{ minLabel }}</label>
      <div class="stepper">
        <button
          type="button"
          class="stepper-btn"
          :disabled="!canDecrement('min')"
          aria-label="Уменьшить"
          @click="adjust('min', -step)"
        >
          −
        </button>
        <input
          :id="minId"
          v-model="minInput"
          type="text"
          inputmode="numeric"
          class="stepper-input"
          :placeholder="minPlaceholder"
          @keydown="handleKeydown"
          @keypress="handleKeypress($event, 'min')"
          @paste="handlePaste($event, 'min')"
        />
        <button
          type="button"
          class="stepper-btn"
          aria-label="Увеличить"
          @click="adjust('min', step)"
        >
          +
        </button>
      </div>
      <label class="range-label" :for="maxId">{{ maxLabel }}</label>
      <div class="stepper">
        <button
          type="button"
          class="stepper-btn"
          :disabled="!canDecrement('max')"
          aria-label="Уменьшить"
          @click="adjust('max', -step)"
        >
          −
        </button>
        <input
          :id="maxId"
          v-model="maxInput"
          type="text"
          inputmode="numeric"
          class="stepper-input"
          :placeholder="maxPlaceholder"
          @keydown="handleKeydown"
          @keypress="handleKeypress($event, 'max')"
          @paste="handlePaste($event, 'max')"
        />
        <button
          type="button"
          class="stepper-btn"
          aria-label="Увеличить"
          @click="adjust('max', step)"
        >
          +
        </button>
      </div>
    </div>
    <div class="range-actions">
      <FilterApplyButton
        :disabled="!canApply"
        :disabled-reason="
          isInvalidRange ? 'Минимум не может быть больше максимума' : undefined
        "
        @click="handleApply"
      />
      <FilterApplyButton
        v-if="showClear"
        label="Сбросить"
        variant="clear"
        @click="handleClear"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
.numeric-range-picker
  padding: $small
  border-bottom: 1px solid $border

.range-row
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

.range-label
  flex-shrink: 0
  font-size: $secondary-font-size
  color: $text-muted
  white-space: nowrap

.stepper
  display: flex
  flex: 1
  min-width: 0
  align-items: stretch
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  overflow: hidden

  &:focus-within
    border-color: $border-focus

.stepper-btn
  flex-shrink: 0
  width: 24px
  padding: 0
  font-size: $font-size
  font-family: inherit
  line-height: 1
  color: $text-muted
  background: transparent
  border: none
  cursor: pointer

  &:hover:not(:disabled)
    background-color: $hover-overlay
    color: $text

  &:disabled
    opacity: $disabled-opacity
    cursor: default

  &:first-child
    border-right: 1px solid $border

  &:last-child
    border-left: 1px solid $border

.stepper-input
  flex: 1
  min-width: 0
  width: 100%
  padding: $small 0
  font-size: $secondary-font-size
  font-family: inherit
  text-align: center
  border: none
  background: transparent
  color: $text
  outline: none

.range-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
