<script setup lang="ts">
// The one bubble of every tooltip, Tooltip.vue's and the hidden text's (anchoredTooltip.ts), so the two cannot
// drift apart. Mounted only while it shows, inside the parent's <Transition name="tooltip-fade">.
import { ref, computed, watch, nextTick, onMounted, toRef } from "vue";
import { useTooltipPosition } from "./useTooltip";
import type { TooltipPlacement } from "./types";

const props = withDefaults(
  defineProps<{
    /** The words of a plain bubble. */
    text?: string;
    /** The rectangle the bubble points at, read again on scroll and resize. */
    anchor: () => DOMRect | null;
    placement?: TooltipPlacement;
    /** The widest the bubble grows, its padding and frame included. */
    maxWidth?: number;
    /** The small tooltip over a word of a tooltip: framed to stand apart. */
    nested?: boolean;
  }>(),
  {
    text: undefined,
    placement: "top",
    maxWidth: 340,
    nested: false,
  },
);

const bubbleRef = ref<HTMLElement | null>(null);
const contentRef = ref<HTMLElement | null>(null);

const { position, updatePosition } = useTooltipPosition(
  () => props.anchor(),
  bubbleRef,
  toRef(props, "placement"),
);

// The width is the stylesheet's: as wide as the words up to the cap, so placing waits for the layout.
async function reposition() {
  await nextTick();
  updatePosition();
}

onMounted(reposition);

// The words change while the bubble is up (a like arriving under an open list).
watch(() => props.text, reposition);

const bubbleStyle = computed(() => ({
  top: `${position.value.top}px`,
  left: `${position.value.left}px`,
  maxWidth: `${props.maxWidth}px`,
}));

// Arrow offset style - adjusts arrow position when tooltip is clamped to viewport
const arrowStyle = computed(() => {
  const offset = position.value.arrowOffset;
  if (!offset) return {};
  // For top/bottom placement, offset moves arrow horizontally
  // translateX is relative to the arrow's centered position (50%)
  return { transform: `translateX(calc(-50% + ${offset}px))` };
});

defineExpose({
  /** The bubble on screen, for "was this press inside it". */
  element: bubbleRef,
  /** The inline element that holds the words: one client rect per line. */
  content: contentRef,
  /** Place again, for content that changed its size (a picture in). */
  reposition,
});
</script>

<template>
  <div
    ref="bubbleRef"
    role="tooltip"
    class="tooltip"
    :class="[`tooltip--${position.placement}`, { 'tooltip--nested': nested }]"
    :style="bubbleStyle"
  >
    <span ref="contentRef" class="tooltip-text">
      <slot>{{ text }}</slot>
    </span>
    <span class="tooltip-arrow" :style="arrowStyle" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

// One width for the outline and the arrow's, which has to meet it.
$tooltip-border-width: 1px
// Half a square rotated 45deg sticks out: $arrow-square is $arrow-span / sqrt(2).
$arrow-span: 12px
$arrow-depth: 6px
$arrow-square: 8.49px
// From the border box, not the padding box, or a slit shows between bubble and arrow.
$arrow-offset: -($arrow-depth + $tooltip-border-width)

.tooltip
  position: fixed
  z-index: $z-tooltip
  box-sizing: border-box
  // As wide as its longest line up to the cap, and a line breaks where the
  // last one would not be left a word alone.
  width: max-content
  max-width: 340px
  text-wrap: pretty
  padding: $small $medium
  font-size: $secondary-font-size
  line-height: 1.4
  color: $tooltip-text
  background-color: $tooltip-bg
  border: $tooltip-border-width solid $tooltip-border
  border-radius: $border-radius
  box-shadow: $shadow-small
  white-space: pre-line
  pointer-events: auto

// The small tooltip over a word of a tooltip: framed in the site's borders.
.tooltip--nested
  border-color: $border

  .tooltip-arrow::before
    border-color: $border

// Inline, so getClientRects() returns one rect per line.
.tooltip-text
  display: inline

  img
    display: block
    max-width: 100%
    height: auto

// A rotated square, not a border triangle: its hidden half covers the bubble's outline, its visible half
// carries it. The rotation is on ::before: the span's transform is the viewport-clamp offset.
.tooltip-arrow
  position: absolute
  pointer-events: none

  &::before
    content: ""
    position: absolute
    box-sizing: border-box
    width: $arrow-square
    height: $arrow-square
    background-color: $tooltip-bg
    border: 0 solid $tooltip-border

.tooltip--top .tooltip-arrow
  bottom: $arrow-offset
  left: 50%
  width: $arrow-span
  height: $arrow-depth
  transform: translateX(-50%)

  // Centred on the arrow box's top edge, which lies on the bubble's border
  // box: the square's lower two sides are the ones on show.
  &::before
    top: 0
    left: 50%
    transform: translate(-50%, -50%) rotate(45deg)
    border-right-width: $tooltip-border-width
    border-bottom-width: $tooltip-border-width

.tooltip--bottom .tooltip-arrow
  top: $arrow-offset
  left: 50%
  width: $arrow-span
  height: $arrow-depth
  transform: translateX(-50%)

  &::before
    bottom: 0
    left: 50%
    transform: translate(-50%, 50%) rotate(45deg)
    border-top-width: $tooltip-border-width
    border-left-width: $tooltip-border-width

.tooltip--left .tooltip-arrow
  right: $arrow-offset
  top: 50%
  width: $arrow-depth
  height: $arrow-span
  transform: translateY(-50%)

  &::before
    left: 0
    top: 50%
    transform: translate(-50%, -50%) rotate(45deg)
    border-top-width: $tooltip-border-width
    border-right-width: $tooltip-border-width

.tooltip--right .tooltip-arrow
  left: $arrow-offset
  top: 50%
  width: $arrow-depth
  height: $arrow-span
  transform: translateY(-50%)

  &::before
    right: 0
    top: 50%
    transform: translate(50%, -50%) rotate(45deg)
    border-bottom-width: $tooltip-border-width
    border-left-width: $tooltip-border-width

// Fade transition. The parents name it on their <Transition>; the classes land
// on this element, so they are styled in its scope.
.tooltip-fade-enter-active,
.tooltip-fade-leave-active
  transition: opacity $transition-normal
  @media (prefers-reduced-motion: reduce)
    transition: none

.tooltip-fade-enter-from,
.tooltip-fade-leave-to
  opacity: 0
</style>
