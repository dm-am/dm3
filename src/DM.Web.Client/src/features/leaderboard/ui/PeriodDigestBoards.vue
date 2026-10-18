<script setup lang="ts">
/**
 * PeriodDigestBoards — the leaderboards of a closed period rendered INSIDE
 * its auto-created "Итоги …" topic. The topic carries only the period marker
 * (topic.periodDigest); the boards are always fetched live from the
 * statistics API — SSOT: board data is computed, never copied into content.
 *
 * Collapsed (news teaser): the three rating boards, top-5 rows each, with
 * the standard "... показать полностью" affordance below. Expanded (or on
 * the topic page): all boards, top-10.
 *
 * Teaser instances participate in the page-wide expandable registry, so the
 * ScrollNav "Развернуть все / Свернуть все" toggle drives them exactly like
 * every TruncatedContent block. Started-expanded instances (topic page) have
 * no collapsed state to offer and stay unregistered.
 */
import { ref, computed, onMounted } from "vue";
import { statisticsApi } from "@/entities/statistics";
import { unwrapResource } from "@/shared/api";
import type { Leaderboards } from "@/shared/api/models/community";
import { useExpandableSection } from "@/shared/lib/composables/useExpandableSection";
import StatBoard from "./StatBoard.vue";
import { LEADERBOARD_BOARDS, TEASER_BOARDS } from "../model/boards";

const props = withDefaults(
  defineProps<{
    /** Calendar year of the digest period. */
    year: number;
    /** Calendar month (1-12); omit for a yearly digest. */
    month?: number | null;
    /** Start expanded (topic page); the news teaser starts collapsed. */
    expanded?: boolean;
  }>(),
  {
    month: null,
    expanded: false,
  },
);

const boards = ref<Leaderboards | null>(null);
const loading = ref(true);

// The full expandable-section contract (unified reveal tempo, the page-wide
// "Развернуть/Свернуть все" toggle, manual-toggle semantics) in one call.
const zoneRef = ref<HTMLElement | null>(null);
const { isExpanded, toggle, zoneBindings } = useExpandableSection({
  el: zoneRef,
  startExpanded: props.expanded,
  label: "PeriodDigestBoards",
});

onMounted(async () => {
  const { data, error } = await statisticsApi.getLeaderboards(
    props.year,
    props.month ?? undefined,
  );
  loading.value = false;
  if (!error) boards.value = unwrapResource<Leaderboards>(data);
});

const boardList = computed(() => {
  const defs = isExpanded.value ? LEADERBOARD_BOARDS : TEASER_BOARDS;
  const cap = isExpanded.value ? 10 : 5;
  return defs.map((def) => ({
    key: def.key,
    title: def.title,
    kind: def.kind,
    entries: boards.value ? (def.select(boards.value) ?? []).slice(0, cap) : [],
  }));
});
</script>

<template>
  <div class="digest-boards">
    <div ref="zoneRef" class="expand-zone" v-bind="zoneBindings">
      <div class="boards-grid">
        <StatBoard
          v-for="board in boardList"
          :key="board.key"
          :title="board.title"
          :entries="board.entries"
          :kind="board.kind"
          :loading="loading"
          :skeleton-rows="isExpanded ? 10 : 5"
          empty-text="Данных за этот период нет"
        />
      </div>
    </div>
    <button
      v-if="!isExpanded"
      type="button"
      class="digest-expand-button"
      :aria-expanded="isExpanded"
      @click="toggle(true)"
    >
      ... <strong>показать полностью</strong>
    </button>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// The reveal animation lives on the global .expand-zone class (Reset.sass),
// driven by useExpandableSection.

// Vertical rhythm inside the topic card: the gaps above and below the grid
// equal the grid's cell gap ($medium). The title above and the footer below
// each contribute $small via their margins; this padding (immune to margin
// collapsing through the wrapper chain) adds the other $small.
.digest-boards
  padding: $small 0
  // The box the grid below asks how many boards stand side by side: the
  // width of the topic card, not the window's (see .boards-grid).
  container: stat-boards / inline-size
  // A query container is laid out as a box of its own, and such a box steps
  // aside for a float instead of running under it. The "Вся статистика
  // сайта" floated into the title of the card hangs below the title where the
  // title takes two lines (a narrow card), and beside it the boards were left
  // a width less the link's: 124 of 251 at 1024. Cleared, they start below
  // it and keep the card's width; where the link stands within the title
  // line, nothing moves.
  clear: both

// How many boards stand side by side is decided by the width of the card they
// stand in, and the same way on the statistics page (SiteStatisticsPage.vue,
// .boards-grid, held equal by its spec). A window of 1024 leaves the middle
// column 285 between two sidebars, narrower than a phone's, and three boards
// sized by their own longest names stood 155 into the right column there.
//
// The narrowest board holds a login of twenty letters on one line of it
// ("LongestLoginPossible", 147 in the site's face at 16) inside its own 16
// on either side: 11.5em. One board under two of those and their gap; two up
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

// Owner-picked board chrome INSIDE a topic: no borders, the faintest wash of
// the scale — just enough to tell the board apart from the topic surface it
// sits on, in both themes (alpha overlays stay correct in either). The
// /statistics page keeps StatBoard's own dashed card.
:deep(.stat-board)
  border: none
  background-color: $overlay-faint

// The shared "... показать полностью" idiom (SSOT mixin in Inputs.sass).
.digest-expand-button
  +expand-toggle-button
</style>
