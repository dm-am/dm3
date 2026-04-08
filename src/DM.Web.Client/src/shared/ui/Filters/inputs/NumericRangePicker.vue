<script setup lang="ts">
/**
 * NumericRangePicker - Numeric range input with Min/Max fields.
 *
 * Used for filtering by numeric ranges (rating, game counts, etc.)
 */
import { ref, watch, computed } from "vue";
import { FilterApplyButton } from "../primitives";

defineOptions({ name: "NumericRangePicker" });

const props = withDefaults(
  defineProps<{
    /** Minimum value */
    minValue: number | null;
    /** Maximum value */
    maxValue: number | null;
    /** Label for min input */
    minLabel?: string;
    /** Label for max input */
    maxLabel?: string;
    /** Placeholder for min input */
    minPlaceholder?: string;
    /** Placeholder for max input */
    maxPlaceholder?: string;
    /** Allow negative numbers */
    allowNegative?: boolean;
    /** Step for input */
    step?: number;
  }>(),
  {
    minLabel: "От:",
    maxLabel: "До:",
    minPlaceholder: "",
    maxPlaceholder: "",
    allowNegative: false,
    step: 1,
  },
);

const emit = defineEmits<{
  apply: [min: number | null, max: number | null];
  clear: [];
}>();

// Local state for inputs (as strings for input binding)
const minInput = ref(props.minValue?.toString() ?? "");
const maxInput = ref(props.maxValue?.toString() ?? "");

// Sync with props when they change
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

// Parse input to number
function parseInput(value: string | number): number | null {
  const strValue = String(value ?? "");
  if (!strValue.trim()) return null;
  const num = parseFloat(strValue);
  if (isNaN(num)) return null;
  if (!props.allowNegative && num < 0) return 0;
  return num;
}

// Check if values differ from props
const canApply = computed(() => {
  const minParsed = parseInput(minInput.value);
  const maxParsed = parseInput(maxInput.value);
  return minParsed !== props.minValue || maxParsed !== props.maxValue;
});

// Check if clear button should be shown
const showClear = computed(() => {
  return props.minValue !== null || props.maxValue !== null;
});

function handleApply() {
  emit("apply", parseInput(minInput.value), parseInput(maxInput.value));
}

function handleClear() {
  minInput.value = "";
  maxInput.value = "";
  emit("clear");
}

// Handle keyboard
function handleKeydown(event: KeyboardEvent) {
  if (event.key === "Enter") {
    event.preventDefault();
    handleApply();
  }
}
</script>

<template>
  <div class="numeric-range-picker">
    <div class="range-row">
      <label class="range-label">{{ minLabel }}</label>
      <input
        v-model="minInput"
        type="number"
        class="range-input"
        :placeholder="minPlaceholder"
        :min="allowNegative ? undefined : 0"
        :step="step"
        @keydown="handleKeydown"
      />
    </div>
    <div class="range-row">
      <label class="range-label">{{ maxLabel }}</label>
      <input
        v-model="maxInput"
        type="number"
        class="range-input"
        :placeholder="maxPlaceholder"
        :min="allowNegative ? undefined : 0"
        :step="step"
        @keydown="handleKeydown"
      />
    </div>
    <div class="range-actions">
      <FilterApplyButton :disabled="!canApply" @click="handleApply" />
      <FilterApplyButton
        v-if="showClear"
        label="Сбросить"
        variant="secondary"
        @click="handleClear"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"

.numeric-range-picker
  padding: $small
  border-bottom: 1px solid $border

.range-row
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

.range-label
  width: 30px
  font-size: $secondary-font-size
  color: $text-muted

.range-input
  flex: 1
  padding: $small
  font-size: $secondary-font-size
  font-family: inherit
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  color: $text
  outline: none
  box-sizing: border-box
  -moz-appearance: textfield

  &:focus
    border-color: $link

  // Hide spinner buttons
  &::-webkit-outer-spin-button,
  &::-webkit-inner-spin-button
    -webkit-appearance: none
    margin: 0

.range-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
