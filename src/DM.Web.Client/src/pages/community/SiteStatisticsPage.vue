<script setup lang="ts">
/**
 * SiteStatisticsPage — "Статистика сайта" (doc 4.2.3.4.3 / block 4.2.2.8):
 * the leaderboard cards for a user-selected period. Period is chosen with a
 * Месяц/Год/Все время segmented control + a month/year picker; there is no
 * period heading — the controls themselves state the period (the h1 + lead +
 * controls + content composition matches the other list pages).
 *
 * The selected period lives in the URL query (per URL_STRUCTURE: filters are
 * query parameters): /statistics?year=2026&month=7 (month), ?year=2026
 * (year), ?period=all (all-time); the default current month keeps a clean
 * URL. Deep links share exact periods — the future "итоги месяца" topics
 * will link here the same way.
 *
 * Periods are cached client-side: closed calendar periods are immutable for
 * the session, the still-open current period gets a short TTL. While an
 * uncached period loads, the previous boards stay visible but dimmed (no
 * layout jump); the skeleton shows only when there is nothing to dim.
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
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { SITE_FOUNDED_YEAR } from "@/shared/config/site";
import { createKeyedCache } from "@/shared/lib/utils/keyedCache";
import { describeFailure } from "@/shared/lib/errors";

type Granularity = "month" | "year" | "all";

const route = useRoute();
const router = useRouter();

const now = new Date();
const curYear = now.getFullYear();
const curMonth = now.getMonth() + 1;

// --- Period selection, initialized from the URL query ---
const granularity = ref<Granularity>("month");
const selYear = ref(curYear);
const selMonth = ref(curMonth);

/** Parses and clamps the period query into state. Invalid values fall back
 * to the default (current month) rather than erroring — a hand-edited URL
 * should degrade gracefully. */
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

/** The canonical query for the current selection; the default current month
 * keeps a clean /statistics URL. */
const periodQuery = computed<Record<string, string>>(() => {
  // Built by assignment (not object literals per branch) so the type stays a
  // plain Record<string, string> — literals would union in optional keys typed
  // `undefined`, which no index signature accepts. Key order is load-bearing:
  // the URL sync compares JSON.stringify of this against route.query.
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

// State → URL. replace (not push): period switches are filter tweaks, same
// idiom as the profile tables and global chat date — back leaves the page
// instead of unwinding every toggle.
watch(periodQuery, (query) => {
  const current = JSON.stringify(route.query);
  if (current !== JSON.stringify(query)) {
    router.replace({ query });
  }
});

// URL → state (deep links, back/forward into a different period state).
watch(
  () => route.query,
  (query) => {
    if (JSON.stringify(query) !== JSON.stringify(periodQuery.value)) {
      applyQuery();
    }
  },
);

// A year pick alone can land the selection on a future month (December
// selected, then the year switched to the current one) — clamp it so the
// page never requests a period that has not started yet.
watch(selYear, (year) => {
  if (year === curYear && selMonth.value > curMonth) selMonth.value = curMonth;
});

const granularityOptions: { value: Granularity; label: string }[] = [
  { value: "month", label: "Месяц" },
  { value: "year", label: "Год" },
  { value: "all", label: "Все время" },
];

// --- Data: per-period client cache ---
// Two caches rather than one with a per-entry rule: a closed calendar period is
// finished history and never has to be read again, while the running one keeps
// changing. The distinction was a condition at the read site; here it is which
// cache the answer went into.
const closedPeriods = createKeyedCache<Leaderboards>({ ttlMs: Infinity });
const openPeriod = createKeyedCache<Leaderboards>({ ttlMs: 60_000 });

const boards = ref<Leaderboards | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
const guard = createRequestGuard();

function periodKey(): string {
  if (granularity.value === "all") return "all";
  if (granularity.value === "year") return `${selYear.value}`;
  return `${selYear.value}-${selMonth.value}`;
}

/** All-time and the still-running current month/year keep changing;
 * closed calendar periods are finished history. */
function isClosedPeriod(): boolean {
  if (granularity.value === "all") return false;
  if (granularity.value === "year") return selYear.value < curYear;
  return (
    selYear.value < curYear ||
    (selYear.value === curYear && selMonth.value < curMonth)
  );
}

async function fetchStats() {
  const key = periodKey();
  const cached = closedPeriods.get(key) ?? openPeriod.get(key);
  if (cached) {
    boards.value = cached;
    loadError.value = null;
    return;
  }

  const requestId = guard.next();
  loading.value = true;
  loadError.value = null;
  const year = granularity.value === "all" ? 0 : selYear.value;
  const month = granularity.value === "month" ? selMonth.value : undefined;

  const { data, error } = await statisticsApi.getLeaderboards(year, month);
  if (!guard.isCurrent(requestId)) return;
  loading.value = false;

  if (error) {
    loadError.value = describeFailure(error, "Не удалось загрузить статистику");
    return;
  }
  const resource = unwrapResource<Leaderboards>(data);
  if (resource) {
    (isClosedPeriod() ? closedPeriods : openPeriod).set(key, resource);
  }
  boards.value = resource;
}

onMounted(fetchStats);
watch([granularity, selYear, selMonth], fetchStats);

// --- Board composition (SSOT: features/leaderboard model). The server
// guarantees positive-only, ordinally ranked entries. ---
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

// A fully empty period (typical for early-history years) collapses to one
// page-level message instead of eight identical empty cards. Closed past
// periods drop the "пока" — nothing more is coming to a finished month.
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

    <!-- Period: granularity toggle + the month/year picker. Block container
         with inline children + a zero-width space (not flex) so a selection
         copies as one line: "Месяц Год Все время ‹ Июль 2026 ›". -->
    <div class="controls">
      <SegmentedControl
        v-model="granularity"
        :options="granularityOptions"
        ariaLabel="Период"
      /><template v-if="granularity !== 'all'"
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
          @update:month="selMonth = $event"
      /></template>
    </div>

    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetchStats"
    />

    <!-- No period heading here: the controls above ARE the period statement
         (active segment names the granularity, the picker trigger names the
         concrete month/year) — a "Статистика за …" h2 would restate both the
         h1 and the picker. Same title→lead→content composition as the other
         list pages (games, community, pulse). -->
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
@import "@/assets/styles/Inputs"

.site-statistics-page
  width: 100%

// Block (not flex) so a cross-selection copies as one clean line; the
// segmented control and the picker are inline-blocks separated by a
// zero-width .copy-space, the visible gap comes from the picker's margin.
.controls
  margin-bottom: $medium

.period-picker
  margin-left: $small

.error-banner
  margin-bottom: $medium

// Leaderboard cards in three columns; the card visuals live in StatBoard.
.boards-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

// A new period is loading: the previous period's boards stay in place but
// visibly recede (no layout jump); aria-busy on the same wrapper carries
// the signal to assistive tech.
.boards-zone
  transition: opacity $transition-fast

  &.refreshing
    opacity: 0.5
    pointer-events: none

// 3 columns is the target; collapse to a single column only when genuinely
// too narrow for it (mobile / very narrow content area).
@media (max-width: 620px)
  .boards-grid
    grid-template-columns: 1fr
</style>
