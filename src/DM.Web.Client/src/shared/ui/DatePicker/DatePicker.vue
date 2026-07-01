<script setup lang="ts">
/**
 * DatePicker — a compact calendar popover styled in the site's own design
 * (no native <input type="date">). A trigger button opens a month grid;
 * picking a day emits the date as a YYYY-MM-DD string and closes the popover.
 *
 * Month navigation via ‹ / ›, future days past `max` are disabled, the
 * selected day and today are highlighted. Closes on click-outside and Esc.
 */
import { ref, computed, watch, onMounted, onUnmounted } from "vue";
import dayjs from "dayjs";

const props = withDefaults(
  defineProps<{
    /** Selected date, YYYY-MM-DD (or null/empty for none). */
    modelValue?: string | null;
    /** Latest selectable date, YYYY-MM-DD. Later days are disabled. */
    max?: string;
    /** Earliest selectable date, YYYY-MM-DD. Earlier days are disabled. */
    min?: string;
    /** Trigger button label. */
    label?: string;
  }>(),
  { modelValue: null, label: "Перейти к дате" },
);

const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
const MONTHS = [
  "Январь",
  "Февраль",
  "Март",
  "Апрель",
  "Май",
  "Июнь",
  "Июль",
  "Август",
  "Сентябрь",
  "Октябрь",
  "Ноябрь",
  "Декабрь",
];

const isOpen = ref(false);
const rootRef = ref<HTMLElement | null>(null);

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

function open() {
  if (props.modelValue)
    viewMonth.value = dayjs(props.modelValue).startOf("month");
  isOpen.value = true;
}

function close() {
  isOpen.value = false;
}

function toggle() {
  if (isOpen.value) close();
  else open();
}

function prevMonth() {
  viewMonth.value = viewMonth.value.subtract(1, "month");
}

function nextMonth() {
  viewMonth.value = viewMonth.value.add(1, "month");
}

function pick(value: string, disabled: boolean) {
  if (disabled) return;
  emit("update:modelValue", value);
  close();
}

function onDocClick(e: MouseEvent) {
  if (
    isOpen.value &&
    rootRef.value &&
    !rootRef.value.contains(e.target as Node)
  ) {
    close();
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === "Escape" && isOpen.value) close();
}

onMounted(() => {
  document.addEventListener("click", onDocClick);
  document.addEventListener("keydown", onKeydown);
});
onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  document.removeEventListener("keydown", onKeydown);
});

// Don't let next-month navigation walk past the month containing `max`.
const canGoNext = computed(() => {
  if (!props.max) return true;
  return viewMonth.value.isBefore(dayjs(props.max).startOf("month"));
});
</script>

<template>
  <div ref="rootRef" class="date-picker">
    <button
      type="button"
      class="dp-trigger"
      :aria-expanded="isOpen"
      @click.stop="toggle"
    >
      {{ label }}
    </button>

    <div v-if="isOpen" class="dp-popover" role="dialog" aria-label="Выбор даты">
      <div class="dp-header">
        <button
          type="button"
          class="dp-nav"
          aria-label="Предыдущий месяц"
          @click.stop="prevMonth"
        >
          ‹
        </button>
        <span class="dp-month">{{ monthLabel }}</span>
        <button
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
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/ZIndex"
@import "src/assets/styles/Inputs"

.date-picker
  position: relative
  display: inline-block

// Same look as the filter / sort buttons (unified +button mixin).
.dp-trigger
  +button

.dp-popover
  position: absolute
  right: 0
  top: calc(100% + #{$tiny})
  z-index: $z-dropdown
  width: $grid-step * 62
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: 0 4px 12px var(--shadow-color)

.dp-header
  display: flex
  align-items: center
  justify-content: space-between
  margin-bottom: $small

.dp-month
  font-weight: bold
  color: $text

.dp-nav
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
