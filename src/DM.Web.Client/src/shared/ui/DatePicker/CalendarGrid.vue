<script setup lang="ts">
/**
 * CalendarGrid — shared month-grid calendar core (single source of truth).
 *
 * Renders the panel with month navigation (‹ / ›), weekday captions and a
 * fixed 6×7 day grid. Picking a day emits the date as a YYYY-MM-DD string.
 * Days outside [min, max] are disabled; the selected day and today are
 * highlighted.
 *
 * Positioning is the consumer's job: DatePicker (global chat) places it as
 * an absolute popover, DateInput (filters) as a fixed one.
 */
import { ref, computed, watch } from "vue";
import dayjs from "dayjs";
import { RU_MONTHS_CAPITALIZED } from "@/shared/lib/utils/months";

const props = defineProps<{
  /** Selected date, YYYY-MM-DD (or null/empty for none). */
  modelValue?: string | null;
  /** Latest selectable date, YYYY-MM-DD. Later days are disabled. */
  max?: string;
  /** Earliest selectable date, YYYY-MM-DD. Earlier days are disabled. */
  min?: string;
}>();

const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
const MONTHS = RU_MONTHS_CAPITALIZED;

// Month currently shown in the grid (first day of that month).
const viewMonth = ref(dayjs().startOf("month"));

const todayValue = computed(() => dayjs().format("YYYY-MM-DD"));

watch(
  () => props.modelValue,
  (val) => {
    if (val) viewMonth.value = dayjs(val).startOf("month");
  },
  { immediate: true },
);

const monthLabel = computed(
  () => `${MONTHS[viewMonth.value.month()]} ${viewMonth.value.year()}`,
);

// 6×7 grid of days covering the visible month (leading/trailing days from
// adjacent months fill the weeks). dayjs day(): 0=Sun..6=Sat → shift to Mon-first.
const days = computed(() => {
  const firstOfMonth = viewMonth.value;
  const weekdayMon = (firstOfMonth.day() + 6) % 7;
  const gridStart = firstOfMonth.subtract(weekdayMon, "day");
  const cells: {
    value: string;
    label: number;
    inMonth: boolean;
    disabled: boolean;
    isToday: boolean;
    isSelected: boolean;
  }[] = [];
  for (let i = 0; i < 42; i++) {
    const d = gridStart.add(i, "day");
    const value = d.format("YYYY-MM-DD");
    const tooLate = props.max ? value > props.max : false;
    const tooEarly = props.min ? value < props.min : false;
    cells.push({
      value,
      label: d.date(),
      inMonth: d.month() === firstOfMonth.month(),
      disabled: tooLate || tooEarly,
      isToday: value === todayValue.value,
      isSelected: !!props.modelValue && value === props.modelValue,
    });
  }
  return cells;
});

function prevMonth() {
  viewMonth.value = viewMonth.value.subtract(1, "month");
}

function nextMonth() {
  viewMonth.value = viewMonth.value.add(1, "month");
}

function pick(value: string, disabled: boolean) {
  if (disabled) return;
  emit("update:modelValue", value);
}

// Don't let month navigation walk past the months containing `max` / `min`.
const canGoNext = computed(() => {
  if (!props.max) return true;
  return viewMonth.value.isBefore(dayjs(props.max).startOf("month"));
});

const canGoPrev = computed(() => {
  if (!props.min) return true;
  return viewMonth.value.isAfter(dayjs(props.min).startOf("month"));
});
</script>

<template>
  <div class="calendar-grid" role="dialog" aria-label="Выбор даты">
    <!-- Inline flow (not flex) with zero-width spaces so the header copies
         as "‹ Июль 2026 ›" in one line. -->
    <div class="dp-header">
      <button
        type="button"
        class="dp-nav"
        aria-label="Предыдущий месяц"
        :disabled="!canGoPrev"
        @click.stop="prevMonth"
      >
        ‹</button
      ><span class="copy-space">{{ " " }}</span
      ><span class="dp-month">{{ monthLabel }}</span
      ><span class="copy-space">{{ " " }}</span
      ><button
        type="button"
        class="dp-nav"
        aria-label="Следующий месяц"
        :disabled="!canGoNext"
        @click.stop="nextMonth"
      >
        ›
      </button>
    </div>
    <div class="dp-weekdays">
      <span v-for="w in WEEKDAYS" :key="w" class="dp-weekday">{{ w }}</span>
    </div>
    <div class="dp-grid">
      <button
        v-for="cell in days"
        :key="cell.value"
        type="button"
        class="dp-day"
        :class="{
          'out-month': !cell.inMonth,
          today: cell.isToday,
          selected: cell.isSelected,
        }"
        :disabled="cell.disabled"
        @click.stop="pick(cell.value, cell.disabled)"
      >
        {{ cell.label }}
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
// Panel visuals live here so every consumer renders the identical calendar;
// only positioning (absolute/fixed) is added by the consumer.
.calendar-grid
  width: $grid-step * 62
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: 0 4px 12px var(--shadow-color)

// Inline flow (not flex) — copies as one line; the month label stretches
// between the fixed-width nav buttons, reproducing space-between geometry.
.dp-header
  white-space: nowrap
  margin-bottom: $small

.dp-month
  display: inline-block
  width: calc(100% - #{$grid-step * 12})
  text-align: center
  vertical-align: middle
  font-weight: bold
  color: $text

.dp-nav
  vertical-align: middle
  width: $grid-step * 6
  height: $grid-step * 6
  border: none
  border-radius: $border-radius
  background: transparent
  color: $link
  font-size: $font-size
  cursor: pointer
  line-height: 1

  &:hover:not(:disabled)
    background-color: $bg-element-accent

  &:disabled
    color: $text-muted
    cursor: default

.dp-weekdays
  display: grid
  grid-template-columns: repeat(7, 1fr)
  margin-bottom: $tiny

.dp-weekday
  text-align: center
  font-size: $tertiary-font-size
  color: $text-muted

.dp-grid
  display: grid
  grid-template-columns: repeat(7, 1fr)
  gap: 1px

.dp-day
  aspect-ratio: 1
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text
  cursor: pointer
  font: inherit
  font-size: $secondary-font-size

  &:hover:not(:disabled)
    background-color: $bg-highlight-blue

  &.out-month
    color: $text-muted

  &.today
    font-weight: bold
    color: $link

  &.selected
    background-color: $button-bg
    color: $button-text
    font-weight: bold

  &:disabled
    color: $text-muted
    opacity: 0.4
    cursor: default
</style>
