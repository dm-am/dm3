<script setup lang="ts">
/**
 * DateRangePicker - Date range input with From/To fields and Apply button.
 *
 * Used for filtering by date ranges (created, activated, closed, etc.)
 * Each field is a DateInput: manual typing plus the shared site calendar
 * (no native <input type="date">).
 */
import { ref, watch, computed } from "vue";
import { DateInput } from "@/shared/ui/DatePicker";
import { FilterApplyButton } from "../primitives";
import type { DateRangePickerProps } from "../types";

defineOptions({ name: "DateRangePicker" });

const props = withDefaults(defineProps<DateRangePickerProps>(), {
  fromLabel: "От",
  toLabel: "До",
  showClearButton: true,
});

const emit = defineEmits<{
  apply: [from: string | null, to: string | null];
  clear: [];
}>();

// Local state for inputs
const fromInput = ref(props.fromValue || "");
const toInput = ref(props.toValue || "");

// Sync with props when they change
watch(
  () => props.fromValue,
  (newVal) => {
    fromInput.value = newVal || "";
  },
);

watch(
  () => props.toValue,
  (newVal) => {
    toInput.value = newVal || "";
  },
);

// Check if any date is set
const hasValues = computed(() => !!fromInput.value || !!toInput.value);

const isInvalidRange = computed(() => {
  return (
    !!fromInput.value && !!toInput.value && fromInput.value > toInput.value
  );
});

// Check if apply button should be enabled
const canApply = computed(() => {
  if (isInvalidRange.value) return false;
  const fromChanged = (fromInput.value || null) !== props.fromValue;
  const toChanged = (toInput.value || null) !== props.toValue;
  return fromChanged || toChanged;
});

// Check if clear button should be shown
const showClear = computed(() => {
  return props.showClearButton && (props.fromValue || props.toValue);
});

function handleApply() {
  emit("apply", fromInput.value || null, toInput.value || null);
}

function handleClear() {
  fromInput.value = "";
  toInput.value = "";
  emit("clear");
}
</script>

<template>
  <div class="dropdown-date-range">
    <div class="date-range-row">
      <span class="date-label">{{ fromLabel }}</span>
      <DateInput
        class="date-input"
        :model-value="fromInput || null"
        :max="toInput || undefined"
        :aria-label="`Дата: ${fromLabel}`"
        @update:model-value="fromInput = $event || ''"
      />
    </div>
    <div class="date-range-row">
      <span class="date-label">{{ toLabel }}</span>
      <DateInput
        class="date-input"
        :model-value="toInput || null"
        :min="fromInput || undefined"
        :aria-label="`Дата: ${toLabel}`"
        @update:model-value="toInput = $event || ''"
      />
    </div>
    <div class="date-actions">
      <FilterApplyButton
        :disabled="!canApply && !hasValues"
        :disabled-reason="
          isInvalidRange ? 'Начало не может быть позже конца' : undefined
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
@import "@/assets/styles/Filters"

+dropdown-date-range

.date-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
