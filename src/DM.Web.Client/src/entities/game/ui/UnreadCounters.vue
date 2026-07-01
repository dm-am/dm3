<script setup lang="ts">
import { Tooltip } from "@/shared/ui/Tooltip";
import { useGameDisplay } from "../model/useGameDisplay";

const props = defineProps<{
  /** Game Guid id (NOT publicId) - first-unread routes bind it as a Guid */
  gameId: string;
  /** Number of unread posts */
  unreadPostsCount?: number;
  /** Number of unread comments */
  unreadCommentsCount?: number;
}>();

const { formatUnreadPostsTooltip, formatUnreadCommentsTooltip } =
  useGameDisplay();
</script>

<template>
  <span v-if="unreadPostsCount || unreadCommentsCount" class="unread-counters">
    <Tooltip
      v-if="unreadPostsCount"
      :text="formatUnreadPostsTooltip(unreadPostsCount)"
    >
      <router-link
        :to="{ name: 'game-first-unread-post', params: { id: gameId } }"
        class="counter posts"
      >
        {{ unreadPostsCount }}
      </router-link>
    </Tooltip>
    <Tooltip
      v-if="unreadCommentsCount"
      :text="formatUnreadCommentsTooltip(unreadCommentsCount)"
    >
      <router-link
        :to="{ name: 'game-first-unread-comment', params: { id: gameId } }"
        class="counter comments"
      >
        {{ unreadCommentsCount }}
      </router-link>
    </Tooltip>
  </span>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.unread-counters
  display: inline-flex
  gap: $tiny
  margin-left: $small

.counter
  display: inline-flex
  align-items: center
  justify-content: center
  min-width: 18px
  height: 18px
  padding: 0 $minor
  font-size: $tertiary-font-size
  font-weight: bold
  border-radius: 9px
  text-decoration: none

  &.posts
    background-color: $accent-green-muted
    color: $accent-green

    &:hover
      background-color: $accent-green
      color: $text-on-green

  &.comments
    background-color: $accent-yellow
    color: $text

    &:hover
      filter: brightness(1.1)
</style>
