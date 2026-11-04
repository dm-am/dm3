<script setup lang="ts">
/**
 * NumericRangePicker — integer range input with +/− stepper buttons.
 *
 * Used for filtering by integer ranges (rating, counts, etc).
 * - Accepts only digits (and optional leading "-")
 * - Native number spinners hidden, replaced by explicit stepper buttons
 * - Enter applies, blur does not (avoids accidental apply)
 */
import { ref, watch, computed, useId } from "vue";
import { FilterApplyButton } from "../primitives";
import type { NumericRangePickerProps } from "../types";

const props = withDefaults(defineProps<NumericRangePickerProps>(), {
  allowNegative: false,
});

const emit = defineEmits<{
  apply: [min: number | null, max: number | null];
  clear: [];
}>();

// Ids for the two captions: a page may hold two range pickers.
const uid = `numeric-range-${useId()}`;
const minId = `${uid}-min`;
const maxId = `${uid}-max`;

// Strings, to keep empty and half-typed states.
const minInput = ref(props.minValue?.toString() ?? "");
const maxInput = ref(props.maxValue?.toString() ?? "");

const sides = [
  { name: "min", id: minId, label: "От", model: minInput },
  { name: "max", id: maxId, label: "До", model: maxInput },
] as const;

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

/** The parsed integer, null for empty or invalid; negatives clamped where not allowed. */
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
  // Guarded here too: Enter in a field does not go through the disabled button.
  if (!canApply.value) return;
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

/** Digits, and with allowNegative one "-" at the start. */
const allowed = computed(() => (props.allowNegative ? /^-?\d*$/ : /^\d*$/));

/** The field's value with its selection replaced by what is typed or pasted. */
function edited(input: HTMLInputElement, text: string): string {
  const start = input.selectionStart ?? input.value.length;
  const end = input.selectionEnd ?? start;
  return input.value.slice(0, start) + text + input.value.slice(end);
}

/**
 * What would leave the field more than a number does not land in it; a paste
 * keeps the digits it holds. Before the edit, on beforeinput: a phone's
 * keyboard sends no keypress, and the field took anything there.
 */
function handleBeforeInput(event: InputEvent, target: "min" | "max") {
  const text = event.data;
  if (!text) return;
  const input = event.target as HTMLInputElement;
  const next = edited(input, text);
  if (allowed.value.test(next)) return;
  event.preventDefault();
  if (event.inputType !== "insertFromPaste") return;
  const digits = props.allowNegative
    ? next.replace(/[^\d-]/g, "").replace(/(?!^)-/g, "")
    : next.replace(/\D/g, "");
  if (target === "min") minInput.value = digits;
  else maxInput.value = digits;
}

/** An empty field counts as 0. */
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
      <template v-for="side in sides" :key="side.name">
        <label class="range-label" :for="side.id">{{ side.label }}</label>
        <div class="stepper">
          <button
            type="button"
            class="stepper-btn"
            :disabled="!canDecrement(side.name)"
            aria-label="Уменьшить"
            @click="adjust(side.name, -1)"
          >
            −
          </button>
          <input
            :id="side.id"
            v-model="side.model.value"
            type="text"
            inputmode="numeric"
            class="stepper-input"
            @keydown="handleKeydown"
            @beforeinput="handleBeforeInput($event, side.name)"
          />
          <button
            type="button"
            class="stepper-btn"
            aria-label="Увеличить"
            @click="adjust(side.name, 1)"
          >
            +
          </button>
        </div>
      </template>
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

// A field of the page: $control-height high, level with "Применить" under it.
.stepper
  display: flex
  flex: 1
  min-width: 0
  align-items: stretch
  box-sizing: border-box
  height: $control-height
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $input-bg-overlay
  overflow: hidden

  &:focus-within
    border-color: $link
    box-shadow: 0 0 0 1px $link

.stepper-btn
  flex-shrink: 0
  width: $tap-target-min
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
  height: 100%
  padding: 0
  font-size: $control-font-size
  font-family: inherit
  text-align: center
  border: none
  background: transparent
  color: $text
  outline: none

  // The wrapper draws the frame and its :focus-within ring; a second ring would sit inside it.
  &:focus
    box-shadow: none

.range-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
