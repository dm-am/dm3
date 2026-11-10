<script setup lang="ts">
// One calendar under both fields: the first pick sets the start, the second the end. Days after today
// are out of reach (nothing a filter finds happens tomorrow), except where `allowFuture` says otherwise.
import { ref, watch, computed } from "vue";
import dayjs from "dayjs";
import { CalendarGrid, DateInput } from "@/shared/ui/DatePicker";
import { DAY, siteTime } from "@/shared/lib/utils/datetime";
import { FilterApplyButton } from "../primitives";
import type { DateRangePickerProps } from "../types";

const props = withDefaults(defineProps<DateRangePickerProps>(), {
  showClearButton: true,
  allowFuture: false,
});

const emit = defineEmits<{
  apply: [from: string | null, to: string | null];
  clear: [];
}>();

/** The quick periods, each ending today and counted back by one unit. */
const PRESETS = [
  { label: "Неделя", unit: "week" },
  { label: "Месяц", unit: "month" },
  { label: "Год", unit: "year" },
] as const;

const calendarRef = ref<InstanceType<typeof CalendarGrid> | null>(null);

const fromInput = ref(props.fromValue || "");
const toInput = ref(props.toValue || "");

/** The start is set and the next click on a day sets the end. */
const awaitingEnd = ref(false);

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

const today = computed(() => siteTime().format(DAY));

/** The latest day a field or the calendar takes. */
const maxDay = computed(() => (props.allowFuture ? undefined : today.value));

const isInvalidRange = computed(
  () => !!fromInput.value && !!toInput.value && fromInput.value > toInput.value,
);

// The only source of the button's disabled state: an inverted range keeps it locked, with its reason.
const canApply = computed(() => {
  if (isInvalidRange.value) return false;
  const fromChanged = (fromInput.value || null) !== props.fromValue;
  const toChanged = (toInput.value || null) !== props.toValue;
  return fromChanged || toChanged;
});

const showClear = computed(
  () => props.showClearButton && (props.fromValue || props.toValue),
);

function setFrom(value: string | null) {
  fromInput.value = value || "";
  awaitingEnd.value = false;
}

function setTo(value: string | null) {
  toInput.value = value || "";
  awaitingEnd.value = false;
}

/**
 * A day clicked on the calendar. The first click starts a new range and
 * empties the end, the second closes it; a second day earlier than the first
 * becomes the start and the first the end.
 */
function onPick(day: string) {
  const start = fromInput.value;
  if (!awaitingEnd.value || !start) {
    fromInput.value = day;
    toInput.value = "";
    awaitingEnd.value = true;
    return;
  }
  if (day < start) {
    fromInput.value = day;
    toInput.value = start;
  } else {
    toInput.value = day;
  }
  awaitingEnd.value = false;
}

/** A quick period: from one unit back, the day after it, to today. */
function applyPreset(unit: (typeof PRESETS)[number]["unit"]) {
  const end = dayjs(today.value);
  fromInput.value = end.subtract(1, unit).add(1, "day").format(DAY);
  toInput.value = end.format(DAY);
  awaitingEnd.value = false;
}

function handleApply() {
  emit("apply", fromInput.value || null, toInput.value || null);
}

function handleClear() {
  fromInput.value = "";
  toInput.value = "";
  awaitingEnd.value = false;
  emit("clear");
}
</script>

<template>
  <div class="dropdown-date-range">
    <div class="date-presets">
      <button
        v-for="preset in PRESETS"
        :key="preset.unit"
        type="button"
        class="date-preset"
        @click="applyPreset(preset.unit)"
      >
        {{ preset.label }}
      </button>
    </div>
    <div class="date-range-row">
      <span class="date-label">От</span>
      <DateInput
        class="date-input"
        bare
        :model-value="fromInput || null"
        :max="maxDay"
        aria-label="Дата: От"
        @update:model-value="setFrom"
        @focus-calendar="calendarRef?.focusGrid()"
      />
    </div>
    <div class="date-range-row">
      <span class="date-label">До</span>
      <DateInput
        class="date-input"
        bare
        :model-value="toInput || null"
        :max="maxDay"
        aria-label="Дата: До"
        @update:model-value="setTo"
        @focus-calendar="calendarRef?.focusGrid()"
      />
    </div>
    <CalendarGrid
      ref="calendarRef"
      class="date-calendar"
      embedded
      range
      :range-start="fromInput || null"
      :range-end="toInput || null"
      :range-open="awaitingEnd"
      :max="maxDay"
      @update:model-value="onPick"
    />
    <div class="date-actions">
      <FilterApplyButton
        :disabled="!canApply"
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
@use "@/assets/styles/Buttons" as *

.dropdown-date-range
  padding: $small
  border-bottom: 1px solid $border

.date-range-row
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

.date-label
  width: 30px
  font-size: $secondary-font-size
  color: $text-muted

.date-input
  flex: 1
  min-width: 0

.date-presets
  display: flex
  gap: $small
  margin-bottom: $small

.date-preset
  flex: 1
  min-width: 0
  +button

.date-calendar
  margin: $tiny 0 $small

.date-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
