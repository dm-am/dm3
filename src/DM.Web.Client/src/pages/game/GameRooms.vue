<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Icon from "@/shared/ui/Icon/Icon.vue";
import { IconType } from "@/shared/ui/Icon/iconType";
import { RoomType, RoomAccessType } from "@/entities/game";

const gameStore = useGameDetailsStore();
const { game, rooms, roomsLoading, roomsError } = storeToRefs(gameStore);

const roomTypeLabels: Record<RoomType, string> = {
  [RoomType.Clean]: "Чистая",
  [RoomType.Hybrid]: "Гибрид",
  [RoomType.Chat]: "Чат",
};

const roomTypeIcons: Record<RoomType, IconType> = {
  [RoomType.Clean]: IconType.List,
  [RoomType.Hybrid]: IconType.Edit,
  [RoomType.Chat]: IconType.Comment,
};

const accessTypeLabels: Record<RoomAccessType, string> = {
  [RoomAccessType.Open]: "Открытая",
  [RoomAccessType.Private]: "Приватная",
};
</script>

<template>
  <div class="game-rooms">
    <div v-if="roomsError" class="rooms-error">
      {{ roomsError }}
    </div>

    <div v-else-if="rooms.length === 0" class="rooms-empty">
      <secondary-text>В этой игре пока нет комнат</secondary-text>
    </div>

    <div v-else class="rooms-list">
      <router-link
        v-for="room in rooms"
        :key="room.id"
        :to="`/game/${game?.id}/rooms/${room.roomNumber}`"
        class="room-card"
      >
        <div class="room-header">
          <Icon
            v-if="room.type"
            :font="roomTypeIcons[room.type]"
            class="room-icon"
          />
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
        <div v-if="room.pendings?.length" class="room-pendings">
          <secondary-text>
            Ожидают ответа:
            {{ room.pendings.map((p) => p.characterName).join(", ") }}
          </secondary-text>
        </div>
      </router-link>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.game-rooms
  min-height: $grid-step * 50

.rooms-error,
.rooms-empty
  padding: $big
  text-align: center

.rooms-error
  color: $accent-red

.rooms-list
  display: flex
  flex-direction: column
  gap: $small

.room-card
  display: block
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius
  text-decoration: none
  color: $text
  transition: background-color 0.2s

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
