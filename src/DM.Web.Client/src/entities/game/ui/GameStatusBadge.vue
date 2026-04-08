<script setup lang="ts">
import { computed } from "vue";
import { GameStatus, ClosedReason } from "../model/types";
import { Tooltip } from "@/shared/ui/Tooltip";

const props = defineProps<{
  /** Game status */
  status: GameStatus | string;
  /** Whether game is recruiting players */
  isRecruiting?: boolean;
  /** Whether this is a subsequent recruitment (донабор) */
  isSubsequent?: boolean;
  /** Closed reason (for Closed status) */
  closedReason?: ClosedReason | string;
  /** Current player count */
  pcCount?: number;
  /** Player limit (max slots) */
  pcLimit?: number;
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

// Show slots info for Active games with open recruitment and limit
const showSlots = computed(() => {
  return (
    isActive.value &&
    props.isRecruiting &&
    props.pcLimit != null &&
    props.pcLimit > 0
  );
});

// Slots display: [current/limit]
const slotsDisplay = computed(() => {
  if (!showSlots.value) return "";
  const current = props.pcCount ?? 0;
  const limit = props.pcLimit ?? 0;
  return `[${current}/${limit}]`;
});
</script>

<template>
  <span class="game-status">
    {{ statusDisplay
    }}<Tooltip v-if="showSlots" text="Персонажей / мест">
      <span class="slots-info">&nbsp;{{ slotsDisplay }}</span>
    </Tooltip>
  </span>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.game-status
  display: inline
  word-wrap: break-word

.slots-info
  color: $text-muted
  cursor: help
</style>
<!-- SVG icons moved to shared/lib/utils/icons.ts: gameFinished, gameFrozen -->
