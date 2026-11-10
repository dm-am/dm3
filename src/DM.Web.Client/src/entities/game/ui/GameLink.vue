<script setup lang="ts">
import { computed } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { GameStatus, type Game, type GameRef } from "../model/gameTypes";
import { useGameDisplay } from "../model/useGameDisplay";

const props = defineProps<{
  game: Game | GameRef;
  highlightNew?: boolean;
  mutedClosed?: boolean;
}>();

const { buildTooltip, isNew } = useGameDisplay();

const tooltip = computed(() => buildTooltip(props.game));
const isClosedGame = computed(
  () => props.mutedClosed && props.game.status === GameStatus.Closed,
);
const isNewGame = computed(
  () => props.highlightNew && !isClosedGame.value && isNew(props.game),
);

// A game with neither id would throw "Missing required param" in the router and take the tree down; an empty string throws too.
const routeId = computed(
  () => props.game.publicId || props.game.id || undefined,
);

const to = computed(() =>
  routeId.value ? { name: "game", params: { id: routeId.value } } : undefined,
);
</script>

<template>
  <Tooltip :text="tooltip">
    <router-link
      v-if="to"
      :to="to"
      :class="{ 'new-game': isNewGame, 'closed-game': isClosedGame }"
      >{{ game.title }}</router-link
    >
    <span
      v-else
      :class="{ 'new-game': isNewGame, 'closed-game': isClosedGame }"
      >{{ game.title }}</span
    >
  </Tooltip>
</template>

<style scoped lang="sass">
.new-game
  color: $accent-green
  &:hover
    color: $accent-green-hover

.closed-game
  color: $text-muted
  &:hover
    color: $link-hover
</style>
