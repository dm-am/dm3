<script setup lang="ts">
// The game id comes from the caller when it has one, otherwise from the room's own game.
import { computed } from "vue";
import type { Game, GameRef } from "../model/gameTypes";
import type { Room } from "../model/roomTypes";

const props = defineProps<{
  room: Room;
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
