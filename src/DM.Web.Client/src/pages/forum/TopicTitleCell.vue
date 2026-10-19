<script setup lang="ts">
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import type { RouteLocationRaw } from "vue-router";
import type { DisplayTopic } from "./topicColumns";
import type { TopicPins } from "./useTopicPins";

/**
 * The title of a topic in the listing with its pin and lock. For the board's
 * moderators the pin is a toggle, as a like is, and in the pinned block the
 * handle of the order (drag, or arrows with the caret); it stands beside the
 * link, a button inside a link being two controls in one.
 */
const props = defineProps<{
  row: DisplayTopic;
  index: number;
  to: RouteLocationRaw;
  search?: string;
  pins: TopicPins;
}>();

const {
  canPin,
  pinnedOrder,
  pinHint,
  isMovable,
  dropMark,
  onPinClick,
  onPinPointerDown,
  onPinKeydown,
} = props.pins;
</script>

<template>
  <span v-if="canPin" class="topic-line">
    <Tooltip
      :text="pinHint(row, index)"
      :disabled="pinnedOrder.held.value !== null"
    >
      <button
        type="button"
        class="pin-toggle"
        :class="[
          dropMark(row, index),
          {
            pinned: row.isPinned,
            'pin-handle': isMovable(row, index),
            held: isMovable(row, index) && pinnedOrder.held.value === index,
          },
        ]"
        :data-pin-toggle="row.id"
        :data-pin-handle="isMovable(row, index) ? row.id : undefined"
        :aria-label="`Закрепить топик ${row.title}`"
        :aria-pressed="row.isPinned"
        :aria-keyshortcuts="
          isMovable(row, index) ? 'ArrowUp ArrowDown' : undefined
        "
        @click="onPinClick(row, $event)"
        @pointerdown="onPinPointerDown(row, index, $event)"
        @pointermove="pinnedOrder.onPointerMove"
        @pointerup="pinnedOrder.onPointerUp"
        @pointercancel="pinnedOrder.onPointerCancel"
        @keydown="onPinKeydown(row, index, $event)"
      >
        <SvgIcon name="pin" class="topic-icon" />
      </button>
    </Tooltip>
    <router-link
      :to="to"
      :class="['topic-link', { pinned: row.isPinned, closed: row.isClosed }]"
    >
      <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
      <span v-if="search" v-html="highlightMatch(row.title, search)" />
      <template v-else>{{ row.title }}</template>
    </router-link>
  </span>
  <router-link
    v-else
    :to="to"
    :class="['topic-link', { pinned: row.isPinned, closed: row.isClosed }]"
  >
    <SvgIcon v-if="row.isPinned" name="pin" class="topic-icon" />
    <SvgIcon v-if="row.isClosed" name="locked" class="topic-icon" />
    <span v-if="search" v-html="highlightMatch(row.title, search)" />
    <template v-else>{{ row.title }}</template>
  </router-link>
</template>

<style scoped lang="sass">

.topic-link
  display: inline-flex
  align-items: center
  gap: $minor
  &.pinned
    font-weight: bold
  &.closed
    opacity: $closed-opacity
    &.pinned
      opacity: 1

.topic-icon
  flex-shrink: 0

// The pin before the link, in the place and size the mark has in it.
.topic-line
  display: inline-flex
  align-items: center
  gap: $minor

  // Laid out as a line of its own, the tooltip would stand the pin on the baseline.
  :deep(.tooltip-trigger)
    display: inline-flex

// The pin as a toggle: the mark's glyph, no frame or fill, muted where not pinned.
.pin-toggle
  display: inline-flex
  padding: 0
  border: none
  background: none
  font: inherit
  color: $text-muted
  cursor: pointer

  &.pinned
    color: $link

  &:hover
    color: $link-hover

.pin-handle
  cursor: grab

  &.held
    cursor: grabbing
</style>
