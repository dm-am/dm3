<script setup lang="ts">
/**
 * ChatFrameReplica — DEV-ONLY chrome for the events-strip catalog: the chat
 * frame with a feed inside it, so a strip is judged where it will live rather
 * than on a blank page. It answers the only question the catalog is about —
 * how much of the chat a variant costs, and whether it gets in the way of
 * reading.
 *
 * The frame copies the geometry of the real container (dashed border, clipped,
 * column flow) and the feed copies ChatMessage's measurements (72px avatar,
 * $small $medium row padding, $medium between rows). The feed is a replica and
 * not the widget itself on purpose: every real message registers its own
 * expandable with the site registry, and a dozen frames of them would drown
 * the "Развернуть все" button that the disclosure variants are judged by.
 *
 * `width` narrows the frame instead of the window, so a narrow strip stands
 * next to a wide one on the same screen. The frame is a size container, so a
 * variant writes its narrow rules as @container queries and the width switch
 * above the catalog fires them for real.
 *
 * `ruler` draws a hairline at a fixed offset from the frame top: whatever a
 * strip does to the feed shows up as the first message crossing that line,
 * instead of having to be caught by eye between two switch clicks.
 */
import { computed } from "vue";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { FEED, type FrameWidth } from "./chatEventsMock";

const props = withDefaults(
  defineProps<{
    /** Width probe: the full column, or a pixel budget. */
    width?: FrameWidth;
    /** Draw the layout-jump hairline. */
    ruler?: boolean;
    /** Show the message feed; off for variants that replace it. */
    showFeed?: boolean;
    /** First feed row that belongs to a running event, or -1 for none. */
    markFrom?: number;
  }>(),
  { width: "full", ruler: false, showFeed: true, markFrom: -1 },
);

const frameStyle = computed(() =>
  props.width === "full" ? undefined : { maxWidth: `${props.width}px` },
);

const isEventRow = (index: number): boolean =>
  props.markFrom >= 0 && index >= props.markFrom;
</script>

<template>
  <div class="replica" :style="frameStyle">
    <div v-if="$slots.above" class="replica-above"><slot name="above" /></div>
    <div class="replica-frame">
      <slot />
      <div class="replica-feed">
        <template v-if="showFeed">
          <template v-for="(row, index) in FEED" :key="row.id">
            <slot v-if="index === 2" name="in-feed" />
            <div
              class="feed-row"
              :class="{
                continuation: row.isContinuation,
                'in-event': isEventRow(index),
              }"
            >
              <div class="feed-gutter">
                <AvatarImg
                  v-if="!row.isContinuation"
                  :picture="null"
                  alt=""
                  :size="72"
                  img-class="feed-avatar"
                />
                <span v-else class="feed-gutter-time">{{ row.time }}</span>
              </div>
              <div class="feed-body">
                <div v-if="!row.isContinuation" class="feed-head">
                  <span class="feed-author">{{ row.author }}</span
                  ><span class="feed-space">{{ " " }}</span
                  ><span class="feed-time">{{ row.time }}</span>
                </div>
                <div class="feed-text">{{ row.text }}</div>
              </div>
            </div>
          </template>
        </template>
        <slot v-else name="instead" />
      </div>
      <div v-if="ruler" class="replica-ruler" aria-hidden="true"></div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/ZIndex"

.replica
  margin-bottom: $small

.replica-above
  margin-bottom: $tiny

// The chat frame: the real container's geometry at a fixed height, so every
// variant is measured against the same amount of chat. A size container, so a
// variant's @container rules answer the width switch and a narrow strip can be
// read next to a wide one.
.replica-frame
  position: relative
  display: flex
  flex-direction: column
  height: 320px
  overflow: hidden
  border: 1px dashed $border
  container-type: inline-size

.replica-feed
  flex: 1
  min-height: 0
  overflow: hidden
  padding-top: $small

.feed-row
  display: flex
  gap: $medium
  padding: $small $medium
  margin-bottom: $medium
  background-color: $bg-page

  &.continuation
    margin-top: -$small
    margin-bottom: $tiny

  // A message sent while the event was running: the probe for the variant
  // that marks the feed instead of describing it in a strip.
  &.in-event
    +tint($accent-green, 6%)
    box-shadow: inset 3px 0 0 0 $accent-green

.feed-gutter
  flex: none
  display: flex
  justify-content: flex-end
  width: 72px

.feed-avatar
  display: block
  width: 72px
  height: 72px
  object-fit: cover

.feed-gutter-time
  align-self: flex-start
  font-size: $secondary-font-size
  color: $text-muted

.feed-body
  flex: 1
  min-width: 0

.feed-head
  margin-bottom: $tiny
  line-height: 1

.feed-author
  font-weight: 500
  color: $text-muted

// A real rendered space, as in the message header itself, so a copied row
// reads "Astrellan 21:02" and not "Astrellan21:02".
.feed-space
  display: inline-block
  width: $small
  white-space: pre

.feed-time
  font-size: $secondary-font-size
  color: $text-muted

.feed-text
  line-height: 1.5
  word-break: break-word

// Fixed reference line at a constant offset from the frame top. Switching the
// scenario then shows a layout jump as the first message crossing it.
.replica-ruler
  position: absolute
  top: 96px
  left: 0
  right: 0
  z-index: $z-chat-fab
  border-top: 1px dashed $accent-red
  pointer-events: none
</style>
