<script setup lang="ts">
import { ClosedReason } from "@/shared/api/models/common";
import { computed } from "vue";
import { GameStatus } from "../model/gameTypes";

const props = defineProps<{
  status: GameStatus | string;
  isRecruiting?: boolean;
  isSubsequent?: boolean;
  closedReason?: ClosedReason | string;
}>();

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
  overflow-wrap: break-word
</style>
