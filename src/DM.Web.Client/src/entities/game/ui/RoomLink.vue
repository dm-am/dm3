<script setup lang="ts">
// Shared primitive for linking to a game room. Rooms carry no tooltip, so
// the component is only the route: the game id comes from the caller when
// it has one, otherwise from the room's own embedded game reference. That
// fallback chain is the reason callers reuse this instead of writing the
// router-link inline.
import { computed } from "vue";
import type { Game, GameRef, Room } from "../model/types";

const props = defineProps<{
  room: Room;
  /** Game the room belongs to, when the caller already holds it */
  game?: Game | GameRef;
}>();

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
  <router-link :to="to">{{ room.title }}</router-link>
</template>
