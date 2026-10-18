<script setup lang="ts">
/**
 * SiteStatisticsPage — "Статистика сайта" (doc 4.2.3.4.3 / block 4.2.2.8):
 * the leaderboard cards for a user-selected period. Period is chosen with a
 * "Месяц" / "Год" / "Все время" segmented control + a month/year picker; there
 * is no period heading — the controls themselves state the period (the h1 +
 * lead + controls + content composition matches the other list pages).
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
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { SITE_FOUNDED_YEAR } from "@/shared/config/site";
import { createKeyedCache } from "@/shared/lib/utils/keyedCache";
import { describeFailure, describeRateLimit } from "@/shared/lib/errors";
import { siteTime } from "@/shared/lib/utils/datetime";

type Granularity = "month" | "year" | "all";

const route = useRoute();
const router = useRouter();

// The current month by the site's clock, the one every date on it keeps.
const now = siteTime();
const curYear = now.year();
const curMonth = now.month() + 1;

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

const LOAD_FAILURE = "Не удалось загрузить статистику";

/**
 * The read answers for itself, because the banner below is the only place this
 * page can answer from.
 *
 * Drop a status from the banner and the page draws eight empty boards under
 * "Данных за период пока нет" — a statement about the period, made about a
 * period the server never answered for. So the banner speaks for every status,
 * and the read takes the refusal over: the interceptor stops putting the same
 * sentence on a plate over a banner that already carries it.
 *
 * An expired session and a dead connection stay with the interceptor. Neither
 * is about this period — one is about the reader's session, the other about the
 * road to the site — and only the interceptor knows the other address. The
 * banner beside them says a different thing: that these boards are not there.
 */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

// clearErrorOnStart: what this page did before the composable — the banner goes
// away while the next period loads.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({
  // A wait is said in the shared wording, the one the interceptor would have
  // raised: it is the only one that names the seconds the server asked for, and
  // the title sent with a 429 carries no number at all.
  message: (error) =>
    error.status === 429
      ? describeRateLimit(error.retryAfterSeconds)
      : describeFailure(error, LOAD_FAILURE),
  clearErrorOnStart: true,
});

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

    <!-- Period: granularity toggle + the month/year picker, one line where
         the row has room for both, a phone's too. Where it has not (the
         middle column of 1024), the picker goes down a line of its own under
         the toggle, level with it: no row of the site scrolls sideways. One
         run of inline children with a zero-width space (not flex items) so a
         selection copies as one line:
         "Месяц Год Все время ‹ Июль 2026 ›". The picker opens its panel in a
         fixed layer, which the row does not cut. -->
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
@use "@/assets/styles/Inputs" as *

.site-statistics-page
  width: 100%

.controls
  margin-bottom: $medium

// One run (not flex items) so a cross-selection copies as one clean line;
// the segmented control and the picker are inline-blocks separated by a
// zero-width .copy-space. The gap beside them is the segments' margin on the
// right, and the gap under them a margin on the top of each part, with the
// run standing that far up: a part that opens a line stands at the left edge
// of the column, whether it is the toggle or the picker gone down under it,
// and the picker under the toggle stands $small below it. No margin pulls the
// run out to the left: its own box would then stand past the column, and
// page-width.spec names any box that does. A part is never cut and the row
// never scrolls.
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

// The leaderboard cards; the card visuals live in StatBoard. How many stand
// side by side is decided by the width of the zone they stand in, and the
// same way in the digest topic of a period (PeriodDigestBoards.vue,
// .boards-grid; the spec holds the two equal). A window of 1024 leaves the
// middle column 285 between two sidebars, and three cards sized by their own
// longest names stood 206 into the right column there.
//
// The narrowest card holds a login of twenty letters on one line of it
// ("LongestLoginPossible", 147 in the site's face at 16) inside its own 16
// on either side: 11.5em. One card under two of those and their gap; two up
// to three; three from there, as before on a wide window. The columns are
// equal and never wider than the grid, whatever the names in them.
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

// A new period is loading: the previous period's boards stay in place but
// visibly recede (no layout jump); aria-busy on the same wrapper carries
// the signal to assistive tech.
.boards-zone
  // The box the grid above asks; its own width, not the window's.
  container: stat-boards / inline-size
  transition: opacity $transition-fast

  &.refreshing
    opacity: 0.5
    pointer-events: none
</style>
