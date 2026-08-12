<script setup lang="ts">
// Shared primitive for linking to a game, wrapped in a rich tooltip.
// Used in the sidebar, in featured post breadcrumbs, and anywhere else
// a game title needs to become a navigable link with participant info.
//
// The tooltip content ("Мастер", "Ассистенты", "Персонажи X/Y", "Читатели") is
// sourced from useGameDisplay().buildTooltip — single source of truth.
//
// Defense-in-depth: if the incoming game object has neither publicId nor
// id (broken API response, stale reactive state, race between two store
// fetches), the component falls back to rendering a plain <span> instead
// of a <router-link>. This prevents vue-router's "Missing required param"
// exception from taking down the entire component tree — we'd rather show
// an un-linked title than crash the page.
import { computed } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { GameStatus, type Game, type GameRef } from "../model/types";
import { useGameDisplay } from "../model/useGameDisplay";

const props = withDefaults(
  defineProps<{
    game: Game | GameRef;
    /** Apply the sidebar's green "new game" highlight to the link text */
    highlightNew?: boolean;
    /** Render closed games in muted gray until hover (sidebar affordance) */
    mutedClosed?: boolean;
  }>(),
  {
    highlightNew: false,
    mutedClosed: false,
  },
);

const { buildTooltip, isNew } = useGameDisplay();

const tooltip = computed(() => buildTooltip(props.game));
const isClosedGame = computed(
  () => props.mutedClosed && props.game.status === GameStatus.Closed,
);
// A closed game is not highlighted as new — muted takes priority.
const isNewGame = computed(
  () => props.highlightNew && !isClosedGame.value && isNew(props.game),
);

// Resolved route identifier. Prefer the SEO-friendly publicId; fall back
// to the raw id. Both may be missing on a malformed or partially-loaded
// game object — in that case `routeId` is undefined and we skip the link.
const routeId = computed(() => {
  const pid = props.game?.publicId;
  const id = props.game?.id;
  // Treat empty strings as "no id" — router stringify throws on empty too.
  if (pid && typeof pid === "string" && pid.length > 0) return pid;
  if (id && typeof id === "string" && id.length > 0) return id;
  return undefined;
});

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

// Closed games: muted gray; on hover — regular link behavior.
.closed-game
  color: $text-muted
  &:hover
    color: $link-hover
</style>
