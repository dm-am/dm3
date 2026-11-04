<script setup lang="ts">
// The boards are fetched live, never copied into the topic; the teaser joins the page's "Развернуть все".
import { ref, computed, onMounted } from "vue";
import { statisticsApi } from "@/entities/statistics";
import { unwrapResource } from "@/shared/api";
import type { Leaderboards } from "@/shared/api/models/community";
import { useExpandableSection } from "@/shared/lib/composables/useExpandableSection";
import StatBoard from "./StatBoard.vue";
import { LEADERBOARD_BOARDS, TEASER_BOARDS } from "../model/boards";

const props = withDefaults(
  defineProps<{
    year: number;
    /** Omitted for a yearly digest. */
    month?: number | null;
    expanded?: boolean;
  }>(),
  {
    month: null,
    expanded: false,
  },
);

const boards = ref<Leaderboards | null>(null);
const loading = ref(true);

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

const rows = computed(() => (isExpanded.value ? 10 : 5));

const boardList = computed(() => {
  const defs = isExpanded.value ? LEADERBOARD_BOARDS : TEASER_BOARDS;
  const cap = rows.value;
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
          :skeleton-rows="rows"
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
@use "@/assets/styles/Links" as *

// The padding is immune to margin collapsing through the wrapper chain.
.digest-boards
  padding: $small 0
  // The card's width, not the window's, decides how many boards stand side by side.
  container: stat-boards / inline-size
  // A query container steps aside for a float: cleared, the boards keep the card's width.
  clear: both

// The narrowest board holds a twenty-letter login (147px at 16) inside its 16 a side: 11.5em.
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

// No border, the faintest wash: just enough to tell the board from the topic.
:deep(.stat-board)
  border: none
  background-color: $overlay-faint

.digest-expand-button
  +expand-toggle-button
</style>
