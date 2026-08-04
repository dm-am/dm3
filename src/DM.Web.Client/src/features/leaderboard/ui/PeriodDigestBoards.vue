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
import { useExpandableSection } from "@/shared/lib/composables";
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
@import "@/assets/styles/Inputs"

// The reveal animation lives on the global .expand-zone class (Reset.sass),
// driven by useExpandableSection.

// Vertical rhythm inside the topic card: the gaps above and below the grid
// equal the grid's cell gap ($medium). The title above and the footer below
// each contribute $small via their margins; this padding (immune to margin
// collapsing through the wrapper chain) adds the other $small.
.digest-boards
  padding: $small 0

.boards-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

// Owner-picked board chrome INSIDE a topic: no borders, the subtle overlay
// (slightly darker than the topic surface; alpha overlays stay correct in
// both themes). The /statistics page keeps StatBoard's own dashed card.
:deep(.stat-board)
  border: none
  background-color: $overlay-subtle

// The shared "... показать полностью" idiom (SSOT mixin in Inputs.sass).
.digest-expand-button
  +expand-toggle-button

@media (max-width: $bp-mobile)
  .boards-grid
    grid-template-columns: 1fr
</style>
