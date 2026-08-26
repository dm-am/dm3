<script setup lang="ts">
/**
 * MonthYearGrid — the panel a period picker shows instead of days: a header
 * with ‹ › navigation and a 12-cell grid under it. Two modes:
 *  - "month": year navigation (‹ 2026 ›) + a 12-month grid.
 *  - "year": block navigation (‹ 2015–2026 ›) + a 12-year grid, paged in
 *    fixed 12-year blocks anchored at maxYear so the grid mirrors the month
 *    one; the oldest block may be partial.
 *
 * It draws the panel and nothing else — no popover, no trigger, no open
 * state. That is what lets the two owners share it: MonthYearPicker hangs it
 * under its own trigger, and CalendarGrid swaps it in over the day grid when
 * the reader clicks the "Июль 2026" header. Before this existed the calendar
 * had no way up at all: another year was only reachable by stepping months.
 *
 * Bounds are open-ended when absent, which is the difference between the two
 * callers. The statistics picker is bounded on both sides (the site has a
 * founding year and no future data); a date field usually is not — a birthday
 * has to reach 1970 — so a missing minYear/maxYear pages on instead of
 * stopping, anchored at the year handed in.
 *
 * `yearNavigable` turns the year in the month-mode header into a button to
 * the year grid. It is off by default: whoever owns the level above decides
 * whether there is a level above.
 *
 * The header is inline flow (not flex) with zero-width `.copy-space` text
 * nodes, so a selection copies as "‹ 2026 ›" on one line — see
 * onelineCopy.spec.ts for why a flex row would not.
 */
import { ref, computed } from "vue";
import { RU_MONTHS_SHORT } from "@/shared/lib/utils/months";

const props = withDefaults(
  defineProps<{
    mode?: "month" | "year";
    /** Selected year. */
    year: number;
    /** Selected month, 1-12 (month mode). */
    month?: number;
    /** Earliest selectable year; paging is open-ended when absent. */
    minYear?: number;
    /** Earliest selectable month within minYear (1-12). */
    minMonth?: number;
    /** Latest selectable year; paging is open-ended when absent. */
    maxYear?: number;
    /** Latest selectable month within maxYear (1-12). */
    maxMonth?: number;
    /** Make the year of the month-mode header a button to the year grid. */
    yearNavigable?: boolean;
  }>(),
  { mode: "month", month: 1, yearNavigable: false },
);

const emit = defineEmits<{
  "pick-month": [year: number, month: number];
  "pick-year": [year: number];
  "open-years": [];
}>();

const MONTHS_SHORT = RU_MONTHS_SHORT;

const maxM = computed(() => props.maxMonth ?? 12);
const minM = computed(() => props.minMonth ?? 1);

// --- month mode ------------------------------------------------------------

/** Year shown in the header while navigating; the pick carries it out. */
const navYear = ref(props.year);

const canPrevYear = computed(
  () => props.minYear === undefined || navYear.value > props.minYear,
);
const canNextYear = computed(
  () => props.maxYear === undefined || navYear.value < props.maxYear,
);

function prevNavYear() {
  if (canPrevYear.value) navYear.value -= 1;
}
function nextNavYear() {
  if (canNextYear.value) navYear.value += 1;
}

function monthDisabled(m: number): boolean {
  if (props.maxYear !== undefined && navYear.value === props.maxYear) {
    if (m > maxM.value) return true;
  }
  if (props.minYear !== undefined && navYear.value === props.minYear) {
    if (m < minM.value) return true;
  }
  return false;
}

function pickMonth(m: number) {
  if (monthDisabled(m)) return;
  emit("pick-month", navYear.value, m);
}

// --- year mode (12-year blocks) --------------------------------------------

const YEARS_PER_BLOCK = 12;

/** Newest year of block 0 — the bound when there is one, else the selection. */
const anchor = computed(() => props.maxYear ?? props.year);

/** Blocks back from the anchor; grows going into the past. */
const yearBlock = ref(0);
yearBlock.value = (() => {
  const start = props.maxYear ?? props.year;
  const holding = Math.max(
    0,
    Math.floor((start - props.year) / YEARS_PER_BLOCK),
  );
  if (props.minYear === undefined) return holding;
  const oldest = Math.floor((start - props.minYear) / YEARS_PER_BLOCK);
  return Math.min(oldest, holding);
})();

const blockNewest = computed(
  () => anchor.value - yearBlock.value * YEARS_PER_BLOCK,
);
const blockOldest = computed(() => {
  const oldest = blockNewest.value - (YEARS_PER_BLOCK - 1);
  return props.minYear === undefined ? oldest : Math.max(props.minYear, oldest);
});

// Ascending within the block (2015, 2016, … 2026) — the same reading order
// as the month grid ("Янв" → "Дек") and as the block label.
const years = computed(() => {
  const list: number[] = [];
  for (let y = blockOldest.value; y <= blockNewest.value; y++) list.push(y);
  return list;
});

const blockLabel = computed(() =>
  blockOldest.value === blockNewest.value
    ? `${blockNewest.value}`
    : `${blockOldest.value}–${blockNewest.value}`,
);

const canPrevBlock = computed(
  () => props.minYear === undefined || blockOldest.value > props.minYear,
);
const canNextBlock = computed(
  () => props.maxYear === undefined || yearBlock.value > 0,
);

function prevBlock() {
  if (canPrevBlock.value) yearBlock.value += 1;
}
function nextBlock() {
  if (canNextBlock.value) yearBlock.value -= 1;
}

function pickYear(y: number) {
  emit("pick-year", y);
}
</script>

<template>
  <div class="myp-body">
    <!-- Month mode: year nav + month grid. -->
    <template v-if="mode === 'month'">
      <div class="myp-header">
        <button
          type="button"
          class="myp-nav"
          aria-label="Предыдущий год"
          :disabled="!canPrevYear"
          @click.stop="prevNavYear"
        >
          ‹</button
        ><span class="copy-space">{{ " " }}</span
        ><button
          v-if="yearNavigable"
          type="button"
          class="myp-year myp-year-button"
          @click.stop="emit('open-years')"
        >
          {{ navYear }}</button
        ><span v-else class="myp-year">{{ navYear }}</span
        ><span class="copy-space">{{ " " }}</span
        ><button
          type="button"
          class="myp-nav"
          aria-label="Следующий год"
          :disabled="!canNextYear"
          @click.stop="nextNavYear"
        >
          ›
        </button>
      </div>
      <div class="myp-grid myp-grid--months">
        <button
          v-for="(m, i) in MONTHS_SHORT"
          :key="m"
          type="button"
          class="myp-cell"
          :class="{ selected: navYear === year && i + 1 === month }"
          :aria-current="
            navYear === year && i + 1 === month ? 'date' : undefined
          "
          :disabled="monthDisabled(i + 1)"
          @click.stop="pickMonth(i + 1)"
        >
          {{ m }}
        </button>
      </div>
    </template>

    <!-- Year mode: block navigation + 12-year grid. -->
    <template v-else>
      <div class="myp-header">
        <button
          type="button"
          class="myp-nav"
          aria-label="Предыдущие годы"
          :disabled="!canPrevBlock"
          @click.stop="prevBlock"
        >
          ‹</button
        ><span class="copy-space">{{ " " }}</span
        ><span class="myp-year">{{ blockLabel }}</span
        ><span class="copy-space">{{ " " }}</span
        ><button
          type="button"
          class="myp-nav"
          aria-label="Следующие годы"
          :disabled="!canNextBlock"
          @click.stop="nextBlock"
        >
          ›
        </button>
      </div>
      <div class="myp-grid myp-grid--years">
        <button
          v-for="y in years"
          :key="y"
          type="button"
          class="myp-cell"
          :class="{ selected: y === year }"
          :aria-current="y === year ? 'date' : undefined"
          @click.stop="pickYear(y)"
        >
          {{ y }}
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// Inline flow (not flex) so the header copies as "‹ 2026 ›" in one line; the
// label stretches between the fixed-width nav buttons and centers its text,
// reproducing the old space-between geometry.
.myp-header
  white-space: nowrap
  margin-bottom: $small

.myp-year
  display: inline-block
  width: calc(100% - #{$grid-step * 14})
  text-align: center
  vertical-align: middle
  font-weight: bold
  color: $text

// The year as the way up to the year grid. Same hover as the arrows beside
// it, because it is the third control of the same header and not a link.
.myp-year-button
  padding: 0
  border: none
  border-radius: $border-radius
  background: none
  font: inherit
  font-weight: bold
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.myp-nav
  width: $grid-step * 7
  height: $grid-step * 7
  border: none
  border-radius: $border-radius
  background: transparent
  color: $link
  font-size: $font-size
  cursor: pointer
  line-height: 1
  vertical-align: middle

  &:hover:not(:disabled)
    background-color: $bg-element-accent

  &:disabled
    color: $text-muted
    cursor: default

.myp-grid
  display: grid
  gap: $tiny
  grid-template-columns: repeat(3, 1fr)

.myp-cell
  padding: $tiny 0
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text
  cursor: pointer
  font: inherit
  font-size: $secondary-font-size

  &:hover:not(:disabled)
    background-color: $bg-highlight-blue

  &.selected
    background-color: $button-bg
    color: $button-text
    font-weight: bold

  &:disabled
    color: $text-muted
    opacity: 0.4
    cursor: default
</style>
