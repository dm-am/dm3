<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { StatusButtons } from "@/shared/ui/StatusButtons";
import {
  availableStatusTransitions,
  GAME_STATUS_WORDS,
} from "../model/transitions";

const store = useGameDetailsStore();
const { game } = storeToRefs(store);

const transitions = computed(() =>
  availableStatusTransitions(
    GAME_STATUS_WORDS,
    game.value?.status,
    game.value?.closedReason,
  ),
);
</script>

<template>
  <StatusButtons
    :transitions="transitions"
    :apply="store.transitionStatus"
    subject="игры"
  />
</template>
