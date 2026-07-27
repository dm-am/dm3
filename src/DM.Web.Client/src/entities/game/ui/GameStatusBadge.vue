<script setup lang="ts">
import { computed } from "vue";
import { GameStatus, ClosedReason } from "../model/types";

const props = defineProps<{
  /** Game status */
  status: GameStatus | string;
  /** Whether game is recruiting players */
  isRecruiting?: boolean;
  /** Whether this is a subsequent recruitment ("донабор") */
  isSubsequent?: boolean;
  /** Closed reason (for Closed status) */
  closedReason?: ClosedReason | string;
}>();

// Status checks
const isDraft = computed(
  () => props.status === GameStatus.Draft || props.status === "Draft",
);
const isActive = computed(
  () => props.status === GameStatus.Active || props.status === "Active",
);
const isClosed = computed(
  () => props.status === GameStatus.Closed || props.status === "Closed",
);
const isFinished = computed(
  () =>
    props.closedReason === ClosedReason.Finished ||
    props.closedReason === "Finished",
);
const isFrozen = computed(
  () =>
    props.closedReason === ClosedReason.Frozen ||
    props.closedReason === "Frozen",
);

// Status display text
const statusDisplay = computed<string>(() => {
  if (isDraft.value) return "Оформляется";
  if (isActive.value) {
    if (props.isRecruiting) {
      return props.isSubsequent ? "Донабор игроков" : "Набор игроков";
    }
    return "Идет игра";
  }
  if (isClosed.value) {
    if (isFinished.value) return "Завершена";
    if (isFrozen.value) return "Заморожена";
    return "Закрыта";
  }
  return String(props.status);
});
</script>

<template>
  <span class="game-status">
    {{ statusDisplay }}
  </span>
</template>

<style scoped lang="sass">
.game-status
  display: inline
  word-wrap: break-word
</style>
<!-- SVG icons moved to shared/lib/utils/icons.ts: gameFinished, gameFrozen -->
