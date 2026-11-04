<script setup lang="ts">
/**
 * The period lives in the query (/statistics?year=2026&month=7, ?year=2026,
 * ?period=all), so a link names an exact period; the current month keeps a
 * clean URL.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { statisticsApi } from "@/entities/statistics";
import { unwrapResource } from "@/shared/api";
import type { Leaderboards } from "@/shared/api/models/community";
import { LeadText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { MonthYearPicker } from "@/shared/ui/MonthYearPicker";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import { StatBoard, LEADERBOARD_BOARDS } from "@/features/leaderboard";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { SITE_FOUNDED_YEAR } from "@/shared/config/site";
import { createKeyedCache } from "@/shared/lib/utils/keyedCache";
import { describeFailureOrWait } from "@/shared/lib/errors";
import { siteTime } from "@/shared/lib/utils/datetime";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

type Granularity = "month" | "year" | "all";

const route = useRoute();
const router = useRouter();

// The current month by the site's clock, the one every date on it keeps.
const now = siteTime();
const curYear = now.year();
const curMonth = now.month() + 1;

const granularity = ref<Granularity>("month");
const selYear = ref(curYear);
const selMonth = ref(curMonth);

/** Parses the period query; an invalid value falls back to the current month. */
function applyQuery() {
  const q = route.query;
  if (q.period === "all") {
    granularity.value = "all";
    return;
  }
  const year = Number(q.year);
  const month = Number(q.month);
  const yearValid =
    Number.isInteger(year) && year >= SITE_FOUNDED_YEAR && year <= curYear;
  if (yearValid && Number.isInteger(month) && month >= 1 && month <= 12) {
    granularity.value = "month";
    selYear.value = year;
    selMonth.value = year === curYear && month > curMonth ? curMonth : month;
  } else if (yearValid && q.month === undefined) {
    granularity.value = "year";
    selYear.value = year;
  } else {
    granularity.value = "month";
    selYear.value = curYear;
    selMonth.value = curMonth;
  }
}

/** The canonical query of the selection. */
const periodQuery = computed<Record<string, string>>(() => {
  // Built by assignment so the type stays Record<string, string>; key order
  // matters, the URL sync compares JSON.stringify of it with route.query.
  const q: Record<string, string> = {};
  if (granularity.value === "all") {
    q.period = "all";
    return q;
  }
  if (granularity.value === "year") {
    q.year = String(selYear.value);
    return q;
  }
  if (selYear.value === curYear && selMonth.value === curMonth) return q;
  q.year = String(selYear.value);
  q.month = String(selMonth.value);
  return q;
});

applyQuery();

// replace, not push: back leaves the page instead of unwinding every toggle.
watch(periodQuery, (query) => {
  const current = JSON.stringify(route.query);
  if (current !== JSON.stringify(query)) {
    router.replace({ query });
  }
});

watch(
  () => route.query,
  (query) => {
    if (JSON.stringify(query) !== JSON.stringify(periodQuery.value)) {
      applyQuery();
    }
  },
);

// A year pick can land on a future month; the page never asks for one.
watch(selYear, (year) => {
  if (year === curYear && selMonth.value > curMonth) selMonth.value = curMonth;
});

const granularityOptions: { value: Granularity; label: string }[] = [
  { value: "month", label: "Месяц" },
  { value: "year", label: "Год" },
  { value: "all", label: "Все время" },
];

// A closed period never changes, the running one does: two caches.
const closedPeriods = createKeyedCache<Leaderboards>({ ttlMs: Infinity });
const openPeriod = createKeyedCache<Leaderboards>({ ttlMs: 60_000 });

const boards = ref<Leaderboards | null>(null);

const LOAD_FAILURE = "Не удалось загрузить статистику";

/**
 * The banner below is the only place this page can answer from, so the read
 * owns every refusal; without a banner the page would draw empty boards for
 * a period the server never answered for.
 */

// The banner goes away while the next period loads.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({
  // The shared wording names the seconds; the 429 title carries no number.
  message: (error) => describeFailureOrWait(error, LOAD_FAILURE),
  clearErrorOnStart: true,
});

function periodKey(): string {
  if (granularity.value === "all") return "all";
  if (granularity.value === "year") return `${selYear.value}`;
  return `${selYear.value}-${selMonth.value}`;
}

/** All-time and the running month or year keep changing. */
function isClosedPeriod(): boolean {
  if (granularity.value === "all") return false;
  if (granularity.value === "year") return selYear.value < curYear;
  return (
    selYear.value < curYear ||
    (selYear.value === curYear && selMonth.value < curMonth)
  );
}

function fetchStats(): Promise<void> {
  const key = periodKey();
  const cached = closedPeriods.get(key) ?? openPeriod.get(key);
  if (cached) {
    boards.value = cached;
    clearError();
    return Promise.resolve();
  }

  const year = granularity.value === "all" ? 0 : selYear.value;
  const month = granularity.value === "month" ? selMonth.value : undefined;

  return run(
    () => statisticsApi.getLeaderboards(year, month, OWNS_REFUSAL),
    (data) => {
      const resource = unwrapResource<Leaderboards>(data);
      if (resource) {
        (isClosedPeriod() ? closedPeriods : openPeriod).set(key, resource);
      }
      boards.value = resource;
    },
  );
}

onMounted(fetchStats);
watch([granularity, selYear, selMonth], fetchStats);

// The server sends positive, ordinally ranked entries only.
const boardList = computed(() =>
  LEADERBOARD_BOARDS.map((def) => ({
    key: def.key,
    title: def.title,
    kind: def.kind,
    entries: boards.value ? (def.select(boards.value) ?? []) : [],
  })),
);

const showSkeleton = computed(() => loading.value && !boards.value);
/** Previous period's boards stay visible but dimmed while a new one loads. */
const refreshing = computed(() => loading.value && !!boards.value);

// An empty period is one message, not eight empty cards; a closed one drops "пока".
const emptyText = computed(() =>
  isClosedPeriod() ? "Данных за этот период нет" : "Данных за период пока нет",
);
const allEmpty = computed(
  () =>
    !loading.value &&
    !loadError.value &&
    boards.value !== null &&
    boardList.value.every((b) => b.entries.length === 0),
);
</script>

<template>
  <div class="site-statistics-page">
    <PageTitle>Статистика сайта</PageTitle>
    <LeadText
      >Найдите себя, свою игру или любимый блог в топах вебсайта</LeadText
    >

    <!-- One run of inline children, not flex items, so a selection copies as one
         line; the picker goes down under the toggle where the row has no room. -->
    <div class="controls">
      <span class="period-line"
        ><SegmentedControl
          v-model="granularity"
          class="period-segments"
          :options="granularityOptions"
          ariaLabel="Период" /><template v-if="granularity !== 'all'"
          ><span class="copy-space">{{ " " }}</span
          ><MonthYearPicker
            class="period-picker"
            :year="selYear"
            :month="selMonth"
            :mode="granularity === 'year' ? 'year' : 'month'"
            :min-year="SITE_FOUNDED_YEAR"
            :max-year="curYear"
            :max-month="curMonth"
            stepper
            @update:year="selYear = $event"
            @update:month="selMonth = $event" /></template
      ></span>
    </div>

    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetchStats"
    />

    <!-- No period heading: the controls name the period. -->
    <div
      class="boards-zone"
      :class="{ refreshing }"
      :aria-busy="loading ? 'true' : undefined"
    >
      <SecondaryText v-if="allEmpty">{{ emptyText }}</SecondaryText>

      <div v-else class="boards-grid">
        <StatBoard
          v-for="board in boardList"
          :key="board.key"
          :title="board.title"
          :entries="board.entries"
          :kind="board.kind"
          :loading="showSkeleton"
          :skeleton-rows="10"
          :empty-text="emptyText"
        />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">

.site-statistics-page
  width: 100%

.controls
  margin-bottom: $medium

// One run, not flex items, so a selection copies as one line. The margins on
// the top keep the part that opens a line at the left edge of the column.
.period-line
  display: block
  margin-top: (-$small)

.period-segments,
.period-picker
  margin-top: $small
  vertical-align: top

.period-segments
  margin-right: $small

.error-banner
  margin-bottom: $medium

// As many cards side by side as the zone has room for, the same rule as
// PeriodDigestBoards.vue. 11.5em holds a login of twenty letters.
$board-min: 11.5em
$board-gap: 1em

.boards-grid
  display: grid
  grid-template-columns: minmax(0, 1fr)
  gap: $medium

@container stat-boards (min-width: #{2 * $board-min + $board-gap})
  .boards-grid
    grid-template-columns: repeat(2, minmax(0, 1fr))

@container stat-boards (min-width: #{3 * $board-min + 2 * $board-gap})
  .boards-grid
    grid-template-columns: repeat(3, minmax(0, 1fr))

// The previous boards stay in place, dimmed, while a new period loads.
.boards-zone
  // The zone's width, not the window's.
  container: stat-boards / inline-size
  transition: opacity $transition-fast

  &.refreshing
    opacity: $opacity-half
    pointer-events: none
</style>
