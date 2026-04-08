<script setup lang="ts">
/**
 * DateRangePicker - Date range input with From/To fields and Apply button.
 *
 * Used for filtering by date ranges (created, activated, closed, etc.)
 */
import { ref, watch, computed } from "vue";
import { FilterApplyButton } from "../primitives";

defineOptions({ name: "DateRangePicker" });

const props = withDefaults(
  defineProps<{
    /** From date value (YYYY-MM-DD format) */
    fromValue: string | null;
    /** To date value (YYYY-MM-DD format) */
    toValue: string | null;
    /** Label for "from" input */
    fromLabel?: string;
    /** Label for "to" input */
    toLabel?: string;
    /** Show clear button when values exist */
    showClearButton?: boolean;
  }>(),
  {
    fromLabel: "От:",
    toLabel: "До:",
    showClearButton: true,
  },
);

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

// Check if apply button should be enabled
const canApply = computed(() => {
  // Can apply if values differ from props
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
      <label class="date-label">{{ fromLabel }}</label>
      <input v-model="fromInput" type="date" class="date-input" />
    </div>
    <div class="date-range-row">
      <label class="date-label">{{ toLabel }}</label>
      <input v-model="toInput" type="date" class="date-input" />
    </div>
    <div class="date-actions">
      <FilterApplyButton :disabled="!canApply && !hasValues" @click="handleApply" />
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
@import "src/assets/styles/Filters"

+dropdown-date-range

.date-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
