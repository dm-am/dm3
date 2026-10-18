<script setup lang="ts">
/**
 * MessengerPage: the open correspondence and, at its right, the list of the
 * reader's conversations, where the right column of the site stands on other
 * pages (the messenger is mounted without it). The page scrolls as a whole,
 * the global chat's way: the feed keeps its own height, the list runs down
 * with the page.
 *
 * The edge between the two is drawn and dragged: the list narrows down to its
 * narrow state, the pictures and the counts of unread alone, and widens back
 * to the width of the right column (useListWidth).
 *
 * No guest branch: the /messenger subtree is requiresAuth, so the router is
 * the one mechanism that turns a guest away (guardAuthenticated).
 *
 * The heading carries no count: the unread messages are counted in the header
 * greeting, "(0/4)", and in each row of the list.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useMessagingStore } from "@/entities/message";
import ChatsList from "./ChatsList.vue";
import {
  LIST_FULL_WIDTH,
  LIST_NARROW_WIDTH,
  useListWidth,
} from "./useListWidth";

const {
  width,
  narrow,
  dragging,
  widen,
  onPointerDown,
  onPointerMove,
  onPointerUp,
  onKeydown,
} = useListWidth();

const { chats, error } = storeToRefs(useMessagingStore());

/**
 * The list has no rows to draw: it failed to load, or the reader has no
 * conversation yet. Its sentence then needs the words of the list, and the
 * narrow state waits for rows.
 */
const nothingToList = computed(
  () =>
    Boolean(error.value) ||
    (chats.value !== null && !chats.value.resources.length),
);

const listNarrow = computed(() => narrow.value && !nothingToList.value);

const listWidth = computed(() =>
  narrow.value && !listNarrow.value ? LIST_FULL_WIDTH : width.value,
);
</script>

<template>
  <page-title>Личные сообщения</page-title>
  <div class="messenger" :class="{ 'messenger--dragging': dragging }">
    <div class="messenger__chat">
      <router-view />
    </div>
    <div
      class="messenger__edge"
      role="separator"
      tabindex="0"
      aria-orientation="vertical"
      aria-label="Ширина списка переписок"
      aria-controls="messenger-list"
      :aria-valuenow="listWidth"
      :aria-valuemin="LIST_NARROW_WIDTH"
      :aria-valuemax="LIST_FULL_WIDTH"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerUp"
      @keydown="onKeydown"
    />
    <aside
      id="messenger-list"
      class="messenger__list"
      :style="{ width: `${listWidth}px` }"
      aria-label="Переписки"
    >
      <ChatsList :narrow="listNarrow" @widen="widen" />
    </aside>
  </div>
</template>

<style scoped lang="sass">
// The correspondence takes what the list leaves it. Their tops stand level;
// each is as tall as its own content.
.messenger
  display: flex
  align-items: flex-start

// The pointer keeps the edge's look over the whole page while it drags.
.messenger--dragging
  cursor: col-resize

.messenger__chat
  flex: 1
  min-width: 0

// The edge: the gap the right column keeps from the page, with a dashed line
// down its middle, the frame's own line. It answers the pointer as an edge to
// drag, and the keyboard by the arrows.
.messenger__edge
  flex: none
  align-self: stretch
  position: relative
  width: $big
  cursor: col-resize
  touch-action: none
  &::before
    content: ""
    position: absolute
    top: 0
    bottom: 0
    left: 50%
    border-left: 1px dashed $border
  &:hover::before,
  .messenger--dragging &::before
    border-left-color: $text-muted
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

.messenger__list
  flex: none
  min-width: 0
</style>
