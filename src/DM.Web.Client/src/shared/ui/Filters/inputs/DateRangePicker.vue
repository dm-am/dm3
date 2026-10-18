<script setup lang="ts">
/**
 * DateRangePicker - Date range input with From/To fields and Apply button.
 *
 * Used for filtering by date ranges (created, activated, closed, etc.)
 *
 * One calendar for the whole range, set into the panel under the two fields.
 * Each field used to open a calendar of its own over the panel, which covered
 * the other field and "Применить", and neither calendar showed the range. Now
 * the first click on a day sets the start and the second the end, the band
 * between them follows the pointer until the second click, and two ends
 * clicked back to front change places. A date typed into a field is drawn on
 * the calendar at once. "Неделя", "Месяц" and "Год" fill both fields with a
 * period that ends today; applying is still "Применить".
 *
 * Days after today are out of reach, since nothing a filter looks for (a game
 * created, a topic written, a user registered) happens tomorrow. A poll is the
 * exception: it starts and ends ahead, and its filter passes `allowFuture`.
 * "Today" is on the site's clock (siteTime), not the device's.
 */
import { ref, watch, computed } from "vue";
import dayjs from "dayjs";
import { CalendarGrid, DateInput } from "@/shared/ui/DatePicker";
import { siteTime } from "@/shared/lib/utils/datetime";
import { FilterApplyButton } from "../primitives";
import type { DateRangePickerProps } from "../types";

defineOptions({ name: "DateRangePicker" });

const props = withDefaults(defineProps<DateRangePickerProps>(), {
  fromLabel: "От",
  toLabel: "До",
  showClearButton: true,
  allowFuture: false,
});

const emit = defineEmits<{
  apply: [from: string | null, to: string | null];
  clear: [];
}>();

const DAY = "YYYY-MM-DD";

/** The quick periods, each ending today and counted back by one unit. */
const PRESETS = [
  { label: "Неделя", unit: "week" },
  { label: "Месяц", unit: "month" },
  { label: "Год", unit: "year" },
] as const;

const calendarRef = ref<InstanceType<typeof CalendarGrid> | null>(null);

// Local state for inputs
const fromInput = ref(props.fromValue || "");
const toInput = ref(props.toValue || "");

/** The start is set and the next click on a day sets the end. */
const awaitingEnd = ref(false);

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

const today = computed(() => siteTime().format(DAY));

/** The latest day a field or the calendar takes. */
const maxDay = computed(() => (props.allowFuture ? undefined : today.value));

const isInvalidRange = computed(() => {
  return (
    !!fromInput.value && !!toInput.value && fromInput.value > toInput.value
  );
});

// Check if apply button should be enabled. This is the only source for the
// button's disabled state: an inverted range has to keep the button locked,
// otherwise the "Начало не может быть позже конца" reason never reaches the
// tooltip and the range gets applied backwards.
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
    <!-- The fields are the page's own, 38 with the text 8 in, level with the
         buttons of the panel above and below them. -->
    <div class="date-range-row">
      <span class="date-label">{{ fromLabel }}</span>
      <DateInput
        class="date-input"
        bare
        :model-value="fromInput || null"
        :max="maxDay"
        :aria-label="`Дата: ${fromLabel}`"
        @update:model-value="setFrom"
        @focus-calendar="calendarRef?.focusGrid()"
      />
    </div>
    <div class="date-range-row">
      <span class="date-label">{{ toLabel }}</span>
      <DateInput
        class="date-input"
        bare
        :model-value="toInput || null"
        :max="maxDay"
        :aria-label="`Дата: ${toLabel}`"
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
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Inputs" as *

+dropdown-date-range

// Three quick periods in a row over the fields, the panel's own buttons
// ("Применить" is one) shared out in equal thirds.
.date-presets
  display: flex
  gap: $small
  margin-bottom: $small

.date-preset
  flex: 1
  min-width: 0
  +button

// The calendar of the range, between the fields it fills and the button that
// applies them.
.date-calendar
  margin: $tiny 0 $small

.date-actions
  display: flex
  flex-direction: column
  gap: $small
</style>
