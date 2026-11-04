<script setup lang="ts">
// Days, months and years: the header buttons climb a level, Escape steps back down and goes no further.
// Only the weeks of the month are drawn, so the panel changes height and grows downward under the arrows.
import { ref, computed, watch, nextTick, onMounted } from "vue";
import dayjs from "dayjs";
import { RU_MONTHS_CAPITALIZED } from "@/shared/lib/utils/months";
import { DAY, siteTime } from "@/shared/lib/utils/datetime";
import { SvgIcon } from "@/shared/ui/Icon";
import { MonthYearGrid } from "@/shared/ui/MonthYearPicker";

type Level = "days" | "months" | "years";

const props = withDefaults(
  defineProps<{
    /** Selected date, YYYY-MM-DD (or null/empty for none). Single mode only. */
    modelValue?: string | null;
    /** Latest selectable date, YYYY-MM-DD. Later days are disabled. */
    max?: string;
    /** Earliest selectable date, YYYY-MM-DD. Earlier days are disabled. */
    min?: string;
    /** Draw a range from rangeStart/rangeEnd instead of one selected day. */
    range?: boolean;
    /** One end of the range, YYYY-MM-DD. */
    rangeStart?: string | null;
    /** The other end of the range, YYYY-MM-DD. */
    rangeEnd?: string | null;
    /** The next pick closes the range at rangeStart: the band previews it. */
    rangeOpen?: boolean;
    /** The grid the calendar opens on. */
    initialLevel?: Level;
    /**
     * The day a calendar with no date opens on, YYYY-MM-DD; today when
     * absent. A birthday opens on the years around the current year minus
     * twelve, since the block of the last twelve years holds nobody's.
     */
    openOn?: string;
    /** Take the keyboard focus when mounted: opened from a button. */
    focusOnOpen?: boolean;
    /** Set into another panel: no ground, frame or width of its own. */
    embedded?: boolean;
  }>(),
  {
    modelValue: null,
    max: undefined,
    min: undefined,
    rangeStart: null,
    rangeEnd: null,
    initialLevel: "days",
    openOn: undefined,
  },
);

const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

interface DayCell {
  value: string;
  label: number;
  disabled: boolean;
  isToday: boolean;
  isSelected: boolean;
  /** Strictly inside the band between the two ends of a range. */
  inRange: boolean;
  /** The earlier end of a band longer than one day. */
  rangeFrom: boolean;
  /** The later end of a band longer than one day. */
  rangeTo: boolean;
}

const rootRef = ref<HTMLElement | null>(null);
const gridRef = ref<HTMLElement | null>(null);

const level = ref<Level>(props.initialLevel);

// Today on the site's clock (siteTime): the day a signed-in reader's account
// is in, which is not always the device's.
const todayValue = computed(() => siteTime().format(DAY));

/** The day the calendar is about: the selection, or the range's later end. */
const anchor = computed(() =>
  props.range ? props.rangeEnd || props.rangeStart : props.modelValue,
);

// A plain calendar date: the grid counts days, and a day has no zone.
const viewMonth = ref(
  dayjs(anchor.value || props.openOn || todayValue.value).startOf("month"),
);

/** The day the pointer (or the keyboard, in a range) is on. */
const pointerDay = ref<string | null>(null);

/** The day that holds the grid's one Tab stop, once the keyboard moved it. */
const focusDay = ref<string | null>(null);

/** The last picks made here: the grid turns to a changed prop (a typed date), never to its own picks. */
const ownPicks: string[] = [];

const monthName = computed(
  () => RU_MONTHS_CAPITALIZED[viewMonth.value.month()],
);

// The month and year the month and year grids fill: those of the date, not of
// the page on view. An empty birthday opens on the years, and a year filled
// there would read as a birthday already chosen.
const markedYear = computed(() =>
  anchor.value ? dayjs(anchor.value).year() : null,
);
const markedMonth = computed(() =>
  anchor.value ? dayjs(anchor.value).month() + 1 : null,
);
const viewYear = computed(() => viewMonth.value.year());

// Undefined stays undefined: a birthday field has to page into any year.
const yearMonthOf = (day: string | undefined) =>
  day ? { year: dayjs(day).year(), month: dayjs(day).month() + 1 } : undefined;
const minBound = computed(() => yearMonthOf(props.min));
const maxBound = computed(() => yearMonthOf(props.max));

function withinBounds(value: string): boolean {
  if (props.min && value < props.min) return false;
  if (props.max && value > props.max) return false;
  return true;
}

function clampToBounds(value: string): string {
  if (props.min && value < props.min) return props.min;
  if (props.max && value > props.max) return props.max;
  return value;
}

function isInView(value: string | null | undefined): value is string {
  return !!value && value.slice(0, 7) === viewMonth.value.format("YYYY-MM");
}

const selectedValues = computed(() =>
  (props.range
    ? [props.rangeStart, props.rangeEnd]
    : [props.modelValue]
  ).filter((value): value is string => !!value),
);

/** The band of a range, in order: the two ends, or the start and the pointer. */
const band = computed<{ from: string; to: string } | null>(() => {
  if (!props.range) return null;
  const start = props.rangeStart || null;
  const other = props.rangeOpen ? pointerDay.value : props.rangeEnd || null;
  if (!start || !other) return null;
  return start <= other
    ? { from: start, to: other }
    : { from: other, to: start };
});

// The days of the month on view, and nothing of the months around it.
const days = computed<DayCell[]>(() => {
  const first = viewMonth.value;
  const span =
    band.value && band.value.from !== band.value.to ? band.value : null;
  const cells: DayCell[] = [];
  for (let i = 0; i < first.daysInMonth(); i++) {
    const value = first.add(i, "day").format(DAY);
    cells.push({
      value,
      label: i + 1,
      disabled: !withinBounds(value),
      isToday: value === todayValue.value,
      isSelected: selectedValues.value.includes(value),
      inRange: !!span && value > span.from && value < span.to,
      rangeFrom: !!span && value === span.from,
      rangeTo: !!span && value === span.to,
    });
  }
  return cells;
});

/** Column of the 1st, Monday first: dayjs counts from Sunday. */
const firstColumn = computed(() => ((viewMonth.value.day() + 6) % 7) + 1);

/**
 * The day that takes the grid's one Tab stop: where the keyboard left it,
 * else the selection, else today, else the first day that can be picked.
 */
const tabDay = computed(() => {
  for (const candidate of [focusDay.value, anchor.value, todayValue.value]) {
    if (isInView(candidate) && withinBounds(candidate)) return candidate;
  }
  return (days.value.find((cell) => !cell.disabled) ?? days.value[0]).value;
});

function prevMonth() {
  viewMonth.value = viewMonth.value.subtract(1, "month");
}

function nextMonth() {
  viewMonth.value = viewMonth.value.add(1, "month");
}

function pick(cell: DayCell) {
  if (cell.disabled) return;
  focusDay.value = cell.value;
  ownPicks.unshift(cell.value);
  ownPicks.splice(2);
  emit("update:modelValue", cell.value);
}

function onPointer(value: string) {
  if (props.rangeOpen) pointerDay.value = value;
}

function dayButton(value: string): HTMLButtonElement | null {
  return (
    gridRef.value?.querySelector<HTMLButtonElement>(`[data-day="${value}"]`) ??
    null
  );
}

/** Put the keyboard focus on the grid on screen, at its one Tab stop. */
function focusGrid() {
  nextTick(() => {
    if (level.value === "days") {
      dayButton(tabDay.value)?.focus();
      return;
    }
    rootRef.value
      ?.querySelector<HTMLButtonElement>('.myp-cell[tabindex="0"]')
      ?.focus();
  });
}

/** Swaps the grid; a focus inside moves into the new one, or it would fall to the page. */
function setLevel(next: Level) {
  const holdsFocus = !!rootRef.value?.contains(document.activeElement);
  level.value = next;
  if (holdsFocus) focusGrid();
}

/** A month chosen in the month grid: show its days. */
function pickMonth(year: number, month: number) {
  viewMonth.value = viewMonth.value.year(year).month(month - 1);
  setLevel("days");
}

/** A year chosen in the year grid: back to the months of that year. */
function pickYear(year: number) {
  viewMonth.value = viewMonth.value.year(year);
  setLevel("months");
}

/** Steps down to the days and goes no further: the owner closes the calendar on Escape. */
function onRootKeydown(e: KeyboardEvent) {
  if (e.key !== "Escape" || level.value === "days") return;
  e.preventDefault();
  e.stopPropagation();
  setLevel("days");
}

/** Move the keyboard to a day, the month on view following it. */
function moveFocus(value: string) {
  const target = clampToBounds(value);
  focusDay.value = target;
  viewMonth.value = dayjs(target).startOf("month");
  if (props.rangeOpen) pointerDay.value = target;
  nextTick(() => dayButton(target)?.focus());
}

function onGridKeydown(e: KeyboardEvent) {
  // Enter and Space pick here and not through the button's own activation,
  // which a consumer that takes Enter for itself (a form, a field) would
  // cancel on the way up. The keyup of Space is swallowed below, or the
  // button would click a second time.
  if (e.key === "Enter" || e.key === " ") {
    e.preventDefault();
    const cell = days.value.find((one) => one.value === tabDay.value);
    if (cell) pick(cell);
    return;
  }
  const from = dayjs(tabDay.value);
  const weekday = (from.day() + 6) % 7;
  let to: dayjs.Dayjs;
  switch (e.key) {
    case "ArrowLeft":
      to = from.subtract(1, "day");
      break;
    case "ArrowRight":
      to = from.add(1, "day");
      break;
    case "ArrowUp":
      to = from.subtract(7, "day");
      break;
    case "ArrowDown":
      to = from.add(7, "day");
      break;
    case "PageUp":
      to = from.subtract(1, e.shiftKey ? "year" : "month");
      break;
    case "PageDown":
      to = from.add(1, e.shiftKey ? "year" : "month");
      break;
    case "Home":
      to = from.subtract(weekday, "day");
      break;
    case "End":
      to = from.add(6 - weekday, "day");
      break;
    default:
      return;
  }
  // The page would scroll on every one of these keys.
  e.preventDefault();
  moveFocus(to.format(DAY));
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

/** A day set from outside (typed): shown among its days, whichever grid is on screen. */
function reveal(value: string | null | undefined) {
  if (!value || ownPicks.includes(value)) return;
  level.value = "days";
  if (!isInView(value)) viewMonth.value = dayjs(value).startOf("month");
}

watch(() => props.modelValue, reveal);
watch(() => props.rangeStart, reveal);
watch(() => props.rangeEnd, reveal);

watch(
  () => props.rangeOpen,
  (open) => {
    if (!open) pointerDay.value = null;
  },
);

onMounted(() => {
  if (props.focusOnOpen) focusGrid();
});

defineExpose({ focusGrid });
</script>

<template>
  <div
    ref="rootRef"
    class="calendar-grid"
    :class="embedded ? 'calendar-grid--embedded' : 'calendar-grid--panel'"
    :role="embedded ? 'group' : 'dialog'"
    :aria-label="embedded ? 'Календарь' : 'Выбор даты'"
    @keydown="onRootKeydown"
  >
    <template v-if="level === 'days'">
      <!-- Inline flow (not flex) with zero-width spaces so the header copies
           as "‹ Сентябрь 2026 ›" in one line. -->
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
        ><span class="dp-title"
          ><button
            type="button"
            class="dp-level dp-month"
            @click.stop="setLevel('months')"
          >
            {{ monthName
            }}<SvgIcon name="chevronDown" class="dp-chevron" /></button
          ><span class="copy-space">{{ " " }}</span
          ><button
            type="button"
            class="dp-level dp-year"
            @click.stop="setLevel('years')"
          >
            {{ viewYear
            }}<SvgIcon name="chevronDown" class="dp-chevron" /></button></span
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
      <div
        ref="gridRef"
        class="dp-grid"
        role="group"
        :aria-label="`${monthName} ${viewYear}`"
        @keydown="onGridKeydown"
        @keyup.space.prevent
        @mouseleave="pointerDay = null"
      >
        <button
          v-for="(cell, i) in days"
          :key="cell.value"
          type="button"
          class="dp-day"
          :class="{
            today: cell.isToday,
            selected: cell.isSelected,
            'in-range': cell.inRange,
            'range-from': cell.rangeFrom,
            'range-to': cell.rangeTo,
          }"
          :style="i === 0 ? { gridColumnStart: firstColumn } : undefined"
          :data-day="cell.value"
          :tabindex="cell.value === tabDay ? 0 : -1"
          :aria-current="cell.isToday ? 'date' : undefined"
          :disabled="cell.disabled"
          @click.stop="pick(cell)"
          @mouseenter="onPointer(cell.value)"
        >
          {{ cell.label }}
        </button>
      </div>
    </template>

    <!-- The key keeps the year from leaking between the two modes of one instance. -->
    <MonthYearGrid
      v-else
      :key="level"
      :mode="level === 'months' ? 'month' : 'year'"
      :year="viewYear"
      :month="viewMonth.month() + 1"
      :min-year="minBound?.year"
      :min-month="minBound?.month"
      :max-year="maxBound?.year"
      :max-month="maxBound?.month"
      :marked-year="markedYear"
      :marked-month="markedMonth"
      year-navigable
      @pick-month="pickMonth"
      @pick-year="pickYear"
      @open-years="setLevel('years')"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Buttons" as *
@use "@/shared/ui/DatePicker/calendar" as *

// Border-box, so a date field can stand exactly as wide ($calendar-width).
.calendar-grid--panel
  box-sizing: border-box
  width: $calendar-width
  padding: $small
  +dropdown-surface

// Inline flow (not flex) — copies as one line; the title stretches between
// the fixed-width nav buttons, reproducing space-between geometry.
.dp-header
  white-space: nowrap
  margin-bottom: $small

// The two ways up, "Сентябрь" and "2026", side by side in the middle of the
// header. Inline-block, like the label they replaced, so the strip still
// copies as one line.
.dp-title
  display: inline-block
  width: calc(100% - #{$grid-step * 12})
  text-align: center
  vertical-align: middle

// A button, not a link: it opens a grid. The chevron is Select's: there is a choice behind the word.
.dp-level
  display: inline-block
  height: $grid-step * 6
  padding: 0 $minor 0 $small
  vertical-align: middle
  border: none
  border-radius: $border-radius
  background: none
  font: inherit
  font-weight: bold
  line-height: $grid-step * 6
  color: $text
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

// The gap between the two: the space between them in a copy is zero-width.
.dp-level + .copy-space + .dp-level
  margin-left: $minor

.dp-chevron
  +calendar-chevron

.dp-nav
  +calendar-nav($grid-step * 6)

// Lines the shared month/year header up with this one; nested under the root to outweigh the panel's rule.
.calendar-grid
  :deep(.myp-nav)
    width: $grid-step * 6
    height: $grid-step * 6

  :deep(.myp-year)
    width: calc(100% - #{$grid-step * 12})

  // A floor, or the twelve rows collapse to their words and the panel halves.
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

// A 1px hairline between weeks; none between days, so a range's band runs unbroken.
.dp-grid
  display: grid
  grid-template-columns: repeat(7, 1fr)
  row-gap: 1px

.dp-day
  aspect-ratio: 1
  padding: 0
  +calendar-cell
    // Today is ringed and not filled: the fill belongs to the selection, which
    // is what the reader is about to act on and has to be the stronger mark.
    &.today
      font-weight: bold
      color: $link
      box-shadow: inset 0 0 0 1px $link

    // The band of a range: the days between the ends, and an end the pointer
    // is only previewing. Square where it meets the next day of the band.
    &.in-range,
    &.range-from:not(.selected),
    &.range-to:not(.selected)
      background-color: $bg-highlight-blue

    &.in-range
      border-radius: 0

    &.range-from
      border-top-right-radius: 0
      border-bottom-right-radius: 0

    &.range-to
      border-top-left-radius: 0
      border-bottom-left-radius: 0

    // The band is the pointer's own tint, so a day of it answers the pointer
    // with the overlay every control lays over its ground.
    &.in-range:hover:not(:disabled),
    &.range-from:not(.selected):hover:not(:disabled),
    &.range-to:not(.selected):hover:not(:disabled)
      background-image: linear-gradient($hover-overlay, $hover-overlay)
</style>
