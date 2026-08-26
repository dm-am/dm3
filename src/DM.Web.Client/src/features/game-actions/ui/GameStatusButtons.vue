<script setup lang="ts">
/**
 * GameStatusButtons — the game status-transition button group: reads the
 * current status from the game-details store, offers the applicable
 * transitions (Start / Finish / Freeze / Close / Reopen) and hands them to the
 * shared StatusButtons, which draws them and dispatches the chosen one.
 *
 * Gating (who may change status) is the caller's responsibility — this
 * component only supplies the transitions and the store call.
 *
 * `variant` is passed straight through: "strip" (default) for the sidebar,
 * "button" for page surfaces.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { StatusButtons } from "@/shared/ui/StatusButtons";
import { availableStatusTransitions } from "../model/transitions";

withDefaults(defineProps<{ variant?: "strip" | "button" }>(), {
  variant: "strip",
});

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const transitions = computed(() =>
  availableStatusTransitions(game.value?.status, game.value?.closedReason),
);
</script>

<template>
  <StatusButtons
    :transitions="transitions"
    :apply="store.transitionStatus"
    subject="игры"
    :variant="variant"
  />
</template>
