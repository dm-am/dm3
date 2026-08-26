<script setup lang="ts">
/**
 * CalendarGrid — shared month-grid calendar core (single source of truth).
 *
 * Renders the panel with month navigation (‹ / ›), weekday captions and a
 * fixed 6×7 day grid. Picking a day emits the date as a YYYY-MM-DD string.
 * Days outside [min, max] are disabled; the selected day and today are
 * highlighted.
 *
 * Three levels, not one. The header "Июль 2026" is a button into a grid of
 * months, and the year in that grid's header is a button into a grid of
 * years; picking drops back a level. Without them the only way to another
 * year was the ‹ arrow, one month per click — a birthday in 1970 is 670
 * clicks away, which is not a preference about arrows but a dead end.
 *
 * The two grids are MonthYearGrid, the same panel the statistics period
 * picker shows; they are not re-drawn here. Bounds are handed to it as years
 * and months: absent min/max means open-ended paging, which is what a date
 * field usually needs and what the statistics picker never does.
 *
 * Positioning is the consumer's job: DatePicker (global chat) places it as
 * an absolute popover, DateInput (filters) as a fixed one.
 */
import { ref, computed, watch } from "vue";
import dayjs from "dayjs";
import { RU_MONTHS_CAPITALIZED } from "@/shared/lib/utils/months";
import { MonthYearGrid } from "@/shared/ui/MonthYearPicker";

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

/** Which grid is on screen: days, the months of a year, or a block of years. */
type Level = "days" | "months" | "years";
const level = ref<Level>("days");

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

// The day limits as the month/year grids read them. Undefined stays
// undefined: a calendar with no max may page into any year, and a birthday
// field has to reach the far side of the century.
const maxYear = computed(() =>
  props.max ? dayjs(props.max).year() : undefined,
);
const maxMonth = computed(() =>
  props.max ? dayjs(props.max).month() + 1 : undefined,
);
const minYear = computed(() =>
  props.min ? dayjs(props.min).year() : undefined,
);
const minMonth = computed(() =>
  props.min ? dayjs(props.min).month() + 1 : undefined,
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

/** A month chosen in the month grid: show its days. */
function pickMonth(year: number, month: number) {
  viewMonth.value = viewMonth.value.year(year).month(month - 1);
  level.value = "days";
}

/** A year chosen in the year grid: back to the months of that year. */
function pickYear(year: number) {
  viewMonth.value = viewMonth.value.year(year);
  level.value = "months";
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
    <template v-if="level === 'days'">
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
        ><button type="button" class="dp-month" @click.stop="level = 'months'">
          {{ monthLabel }}</button
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
    </template>

    <!-- The two upper levels are one component in two modes. The key is
         load-bearing: without it Vue patches the same instance across the
         branches and the grid keeps the year it was navigated to. -->
    <MonthYearGrid
      v-else
      :key="level"
      :mode="level === 'months' ? 'month' : 'year'"
      :year="viewMonth.year()"
      :month="viewMonth.month() + 1"
      :min-year="minYear"
      :min-month="minMonth"
      :max-year="maxYear"
      :max-month="maxMonth"
      year-navigable
      @pick-month="pickMonth"
      @pick-year="pickYear"
      @open-years="level = 'years'"
    />
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

// The way up: "Июль 2026" opens the month grid. A button and not a link —
// it opens a panel, it does not go anywhere — so it takes the hover of the
// arrows beside it and keeps the label's own ink and weight. Inline-block,
// like the span it replaced, so the strip still copies as one line.
.dp-month
  display: inline-block
  width: calc(100% - #{$grid-step * 12})
  padding: 0
  text-align: center
  vertical-align: middle
  border: none
  border-radius: $border-radius
  background: none
  font: inherit
  font-weight: bold
  color: $text
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

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

// The shared month/year panel is drawn for the 220px statistics popover; this
// one is wider and its day header uses smaller arrows. Line the two headers up
// so nothing changes size under the cursor when the level does.
//
// Nested under the root on purpose. A bare `:deep(.myp-nav)` compiles to
// `[data-v-here] .myp-nav`, which weighs exactly what the panel's own
// `.myp-nav[data-v-there]` weighs, and a tie is settled by whichever
// stylesheet the bundler happened to emit second. Under `.calendar-grid` the
// override outweighs it and the order stops mattering.
.calendar-grid
  :deep(.myp-nav)
    width: $grid-step * 6
    height: $grid-step * 6

  :deep(.myp-year)
    width: calc(100% - #{$grid-step * 12})

  // Twelve cells over the area six weeks of days occupy: without a floor the
  // rows collapse to the height of the words and the panel halves.
  :deep(.myp-grid)
    grid-auto-rows: minmax(#{$grid-step * 9}, 1fr)

.dp-weekdays
  display: grid
  grid-template-columns: repeat(7, 1fr)
  margin-bottom: $tiny

.dp-weekday
  text-align: center
  font-size: $tertiary-font-size
  color: $text-muted

// 1px is a hairline between day cells, not a spacing step, and stays as it is:
// the cells are aspect-ratio squares in a seven-column 1fr grid, so the
// smallest step of the scale ($tiny) doubles the seam and shrinks every cell of
// the calendar with it.
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
