<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { RoomType, RoomAccessType } from "@/entities/game";

const gameStore = useGameDetailsStore();
const { game, rooms, activeRooms, archivedRooms, roomsError } =
  storeToRefs(gameStore);

const roomTypeLabels: Record<RoomType, string> = {
  [RoomType.Default]: "Обычная",
  [RoomType.Chat]: "Чат",
};

const accessTypeLabels: Record<RoomAccessType, string> = {
  [RoomAccessType.Open]: "Открытая",
  [RoomAccessType.Private]: "Приватная",
};

// Chat-type rooms use the chat route; regular rooms the post-room route.
function roomRoute(room: (typeof rooms.value)[number]) {
  return {
    name: room.type === RoomType.Chat ? "game-chat-room" : "game-room",
    params: {
      id: game.value?.publicId ?? game.value?.id,
      num: room.roomNumber,
    },
  };
}

// Active rooms first, then an "Архивные комнаты" section — one card template
// driven by the store's active/archived split (never a flat list that mixes
// archived rooms in with no label).
const sections = computed(() => [
  { key: "active", title: null as string | null, rooms: activeRooms.value },
  {
    key: "archived",
    title: "Архивные комнаты" as string | null,
    rooms: archivedRooms.value,
  },
]);
</script>

<template>
  <div class="game-rooms">
    <div v-if="roomsError" class="rooms-error">
      {{ roomsError }}
    </div>

    <div v-else-if="rooms.length === 0" class="rooms-empty">
      <secondary-text>В этой игре пока нет комнат</secondary-text>
    </div>

    <template v-else>
      <template v-for="section in sections" :key="section.key">
        <h3
          v-if="section.title && section.rooms.length"
          class="rooms-archived-title"
        >
          {{ section.title }}
        </h3>
        <div v-if="section.rooms.length" class="rooms-list">
          <router-link
            v-for="room in section.rooms"
            :key="room.id"
            :to="roomRoute(room)"
            class="room-card"
          >
            <div class="room-header">
              <span class="room-title">{{ room.title }}</span>
              <span v-if="room.unreadPostsCount" class="room-unread">
                +{{ room.unreadPostsCount }}
              </span>
            </div>
            <div class="room-meta">
              <span v-if="room.type" class="room-type">
                {{ roomTypeLabels[room.type] }}
              </span>
              <span v-if="room.access" class="room-access">
                {{ accessTypeLabels[room.access] }}
              </span>
            </div>
            <div v-if="room.pendencies?.length" class="room-pendings">
              <secondary-text>
                Ожидают ответа:
                {{ room.pendencies.map((p) => p.characterName).join(", ") }}
              </secondary-text>
            </div>
          </router-link>
        </div>
      </template>
    </template>
  </div>
</template>

<style scoped lang="sass">
.game-rooms
  min-height: $grid-step * 50

.rooms-error,
.rooms-empty
  padding: $big

.rooms-error
  color: $accent-red

.rooms-list
  display: flex
  flex-direction: column
  gap: $small

// Heading for the archived-rooms section.
.rooms-archived-title
  margin: $medium 0 $small
  font-size: $font-size
  font-weight: bold
  color: $text-muted

.room-card
  display: block
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius
  text-decoration: none
  color: $text

  &:hover
    background-color: $bg-element-accent

.room-header
  display: flex
  align-items: center
  gap: $small

.room-icon
  color: $text-muted

.room-title
  font-weight: bold
  flex: 1

.room-unread
  font-size: $secondary-font-size
  font-weight: bold
  padding: 2px $small
  background-color: $accent-red
  color: white
  border-radius: $border-radius

.room-meta
  display: flex
  gap: $medium
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $text-muted

.room-pendings
  margin-top: $small
  padding-top: $small
  border-top: 1px solid $border
</style>
