<script setup lang="ts">
// Shared primitive for linking to a game room, wrapped in a rich tooltip.
// Mirrors GameLink.vue. The tooltip lists characters with access to the
// room in the same "- Name (owner)" format used by the game characters
// tooltip on the games page (see useGameDisplay.buildRoomTooltip).
//
// Tooltip visibility follows room access rules:
//   - Open rooms: tooltip shown to everyone.
//   - Private rooms: tooltip shown only to users who can view the room
//     (master / assistant / mentor, or an owner of a character listed in
//     room.claims). Other viewers get a plain router-link.
//
// Participant source of truth:
//   1. room.claims (when backend returns them — exact room access)
//   2. game.activeCharacters (fallback — all game-level active characters)
//
// The optional `game` prop lets callers enrich the tooltip when the Room
// itself does not carry the extra fields (e.g. featured post on home).
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { Game, GameRef, Room } from "../model/types";
import { useGameDisplay } from "../model/useGameDisplay";
import { useAuthStore } from "@/entities/user/@x/game";

const props = defineProps<{
  room: Room;
  /** Optional enrichment source for the participant list */
  game?: Game | GameRef;
}>();

const { buildRoomTooltip } = useGameDisplay();
const { user } = storeToRefs(useAuthStore());

const tooltip = computed(() =>
  buildRoomTooltip(props.room, props.game, user.value?.username),
);
const hasTooltip = computed(() => tooltip.value.length > 0);

const to = computed(() => ({
  name: "game-room",
  params: {
    id:
      props.game?.publicId ??
      props.room.game?.publicId ??
      props.room.game?.id ??
      "",
    num: props.room.roomNumber,
  },
}));
</script>

<template>
  <Tooltip v-if="hasTooltip" :text="tooltip">
    <router-link :to="to">{{ room.title }}</router-link>
  </Tooltip>
  <router-link v-else :to="to">{{ room.title }}</router-link>
</template>
