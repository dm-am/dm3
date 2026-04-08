<script setup lang="ts">
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { Board } from "@/entities/forum";

const { boards, selectedBoard } = storeToRefs(useBoardsStore());

function getModeratorsTooltip(board: Board): string | undefined {
  const moderators = board.moderators;
  if (!moderators?.length) return undefined;
  const label = moderators.length === 1 ? "Модератор" : "Модераторы";
  const names = moderators.map((m) => m.username).join(", ");
  return `${label}: ${names}`;
}
</script>

<template>
  <nav v-if="boards?.length" class="board-navigation">
    <template v-for="(board, idx) in boards" :key="board.id">
      <span v-if="idx > 0" class="separator">|</span>
      <Tooltip :text="getModeratorsTooltip(board)">
        <router-link
          :to="{ name: 'forum', params: { alias: board.alias } }"
          :class="['board-link', { active: selectedBoard?.id === board.id }]"
          >{{ board.title }}</router-link
        >
      </Tooltip>
    </template>
  </nav>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.board-navigation
  display: flex
  flex-wrap: nowrap
  align-items: center
  justify-content: space-between
  margin-bottom: $small
  overflow-x: auto
  -ms-overflow-style: none
  scrollbar-width: none

  // Hide scrollbar but allow scrolling on mobile
  &::-webkit-scrollbar
    display: none

.separator
  color: $text-muted
  flex-shrink: 0
  font-size: 15px

.board-link
  color: $link
  text-decoration: none
  white-space: nowrap
  font-size: 15px
  &:hover
    color: $link-hover
  &.active
    font-weight: 600
    color: $text
</style>
