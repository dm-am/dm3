<script setup lang="ts">
/**
 * The bubble of every tooltip of the site: the dark plate with its arrow,
 * shrunk to its words and placed against the rectangle it explains.
 *
 * Tooltip.vue shows it over its trigger. The hidden text of a post shows it
 * over the line under the pointer (anchoredTooltip.ts), where there is no
 * component to carry a trigger: the text is server HTML. Both draw this one
 * component, so the two bubbles cannot drift apart in look or in placement.
 *
 * Mounted only while it shows. The parent renders it behind a v-if inside
 * its own <Transition name="tooltip-fade">; the fade itself is styled here,
 * on the element it runs on.
 *
 * Two more kinds of the same plate. A nested one is the small tooltip a word
 * of a tag description opens over the tooltip it stands in (TooltipContent):
 * it carries a frame in the colour of the site's borders, which sets it off
 * from the plate under it in the light theme, where a tooltip has no frame.
 * An inline one stands in the flow of the page, open, pointing down at what
 * stands under it: the preview of a tag description in the window of the tag,
 * where a tooltip carried to <body> would cover the window's own panels.
 */
import { ref, computed, watch, nextTick, onMounted, toRef } from "vue";
import { useTooltipPosition } from "./useTooltip";
import type { TooltipPlacement } from "./types";

const props = withDefaults(
  defineProps<{
    /** The words of a plain bubble, and what its width is measured from. */
    text?: string;
    /** The default slot holds rich content with a layout of its own. */
    rich?: boolean;
    /** The rectangle the bubble points at, read again on scroll and resize. */
    anchor: () => DOMRect | null;
    placement?: TooltipPlacement;
    /** The widest the bubble grows, its padding and frame included. */
    maxWidth?: number;
    /** The small tooltip over a word of a tooltip: framed to stand apart. */
    nested?: boolean;
    /** In the flow of the page and not over it, always open. */
    inline?: boolean;
  }>(),
  {
    text: undefined,
    rich: false,
    placement: "top",
    maxWidth: 340,
    nested: false,
    inline: false,
  },
);

const bubbleRef = ref<HTMLElement | null>(null);
const contentRef = ref<HTMLElement | null>(null);
const shrunkWidth = ref<number | null>(null);

const { position, updatePosition } = useTooltipPosition(
  () => props.anchor(),
  bubbleRef,
  toRef(props, "placement"),
);

// Solve shrinkwrap problem using canvas text measurement. Plain-text
// tooltips only: rich slot popovers skip the shrink (see measureAndShrink)
// and size themselves via CSS width/max-width. The widest is maxWidth, 340
// unless the caller asks for less.

// Singleton canvas for text measurement (performance optimization)
let measureCanvas: HTMLCanvasElement | null = null;
let measureCtx: CanvasRenderingContext2D | null = null;

// Measure text width using canvas (accurate, no layout needed)
function measureTextWidth(text: string, font: string): number {
  if (!measureCanvas) {
    measureCanvas = document.createElement("canvas");
    measureCtx = measureCanvas.getContext("2d");
  }
  if (!measureCtx) return 0;
  measureCtx.font = font;
  return measureCtx.measureText(text).width;
}

// Simulate word wrapping and find widest line (respects \n as forced line breaks)
function getWidestLineWidth(
  text: string,
  font: string,
  maxWidth: number,
): number {
  // Split by newlines first (white-space: pre-line preserves them)
  const paragraphs = text.split(/\n/);
  let widestWidth = 0;

  for (const paragraph of paragraphs) {
    if (!paragraph.trim()) continue;

    // Simulate word wrapping within each paragraph
    const words = paragraph.split(/(\s+)/);
    let currentLine = "";

    for (const word of words) {
      const testLine = currentLine + word;
      const testWidth = measureTextWidth(testLine, font);

      if (testWidth > maxWidth && currentLine.length > 0) {
        // Line would overflow, measure current line and start new one
        const lineWidth = measureTextWidth(currentLine.trimEnd(), font);
        widestWidth = Math.max(widestWidth, lineWidth);
        currentLine = word.trimStart();
      } else {
        currentLine = testLine;
      }
    }

    // Measure last line of paragraph
    if (currentLine.length > 0) {
      const lineWidth = measureTextWidth(currentLine.trimEnd(), font);
      widestWidth = Math.max(widestWidth, lineWidth);
    }
  }

  return widestWidth;
}

function measureAndShrink() {
  if (!bubbleRef.value || !contentRef.value) return;

  // Rich slot content (block layout, nested elements, grids) cannot be
  // simulated by the canvas TEXT measurement: textContent glues all nodes
  // into one pseudo-line, under-measures the real block layout and the
  // resulting width clips the popover (eaten right padding, cut columns).
  // Skip the shrink entirely — CSS `width: max-content` + `max-width: 340px`
  // already size slot popovers from their content.
  if (props.rich) {
    updatePosition();
    return;
  }

  const text = contentRef.value.textContent || "";
  if (!text.trim()) return;

  // Get computed font, padding, and border
  const style = getComputedStyle(bubbleRef.value);
  const font = `${style.fontSize} ${style.fontFamily}`;
  const paddingLeft = parseFloat(style.paddingLeft) || 0;
  const paddingRight = parseFloat(style.paddingRight) || 0;
  const borderLeft = parseFloat(style.borderLeftWidth) || 0;
  const borderRight = parseFloat(style.borderRightWidth) || 0;
  const horizontalExtra = paddingLeft + paddingRight + borderLeft + borderRight;

  // Max text width (content area)
  const maxTextWidth = props.maxWidth - horizontalExtra;

  // Check if text has explicit newlines or needs wrapping
  const hasNewlines = text.includes("\n");
  const naturalWidth = measureTextWidth(text.replace(/\n/g, " "), font);

  let textWidth: number;
  if (!hasNewlines && naturalWidth <= maxTextWidth) {
    // Single line, fits without wrapping
    textWidth = naturalWidth;
  } else {
    // Has newlines or needs wrapping - find widest line
    textWidth = getWidestLineWidth(text, font, maxTextWidth);
  }

  // With box-sizing: border-box, width includes padding and border
  shrunkWidth.value = Math.ceil(textWidth) + horizontalExtra;
  nextTick(() => updatePosition());
}

// Measured at its natural width first, then shrunk and placed.
async function remeasure() {
  shrunkWidth.value = null;
  await nextTick();
  measureAndShrink();
}

onMounted(remeasure);

// Re-measure when the words change while the bubble is up (a like arriving
// while the list of likers is open).
watch(() => props.text, remeasure);

const bubbleStyle = computed(() =>
  props.inline
    ? // No wider than what it stands in either: on a phone the preview of a
      // window is narrower than a tooltip may be.
      { maxWidth: `min(${props.maxWidth}px, 100%)` }
    : {
        top: `${position.value.top}px`,
        left: `${position.value.left}px`,
        maxWidth: `${props.maxWidth}px`,
        ...(shrunkWidth.value ? { width: `${shrunkWidth.value}px` } : {}),
      },
);

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
  /** Measure and place again, for content that changed its size (a picture in). */
  reposition: remeasure,
});
</script>

<template>
  <div
    ref="bubbleRef"
    role="tooltip"
    class="tooltip"
    :class="[
      `tooltip--${inline ? 'top' : position.placement}`,
      { 'tooltip--nested': nested, 'tooltip--inline': inline },
    ]"
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

// The bubble's outline, and the arrow's: one width, because the arrow carries
// the same line and has to meet it.
$tooltip-border-width: 1px
// The arrow is the half of a square rotated 45deg that sticks out of the
// bubble, so its visible base is that square's diagonal and its depth is half
// of it. $arrow-square is $arrow-span / sqrt(2).
$arrow-span: 12px
$arrow-depth: 6px
$arrow-square: 8.49px
// The arrow starts at the bubble's BORDER box, not at its padding box. The
// offset used to be measured from the padding box, which is where the 3px
// slit between bubble and arrow came from.
$arrow-offset: -($arrow-depth + $tooltip-border-width)

.tooltip
  position: fixed
  z-index: $z-tooltip
  box-sizing: border-box
  width: max-content
  max-width: 340px
  padding: $small $medium
  font-size: $secondary-font-size
  line-height: 1.4
  color: $tooltip-text
  background-color: $tooltip-bg
  border: $tooltip-border-width solid $tooltip-border
  border-radius: $border-radius
  box-shadow: 0 2px 8px $shadow-color
  white-space: pre-line
  // Allow hovering over tooltip content
  pointer-events: auto

// The small tooltip over a word of a tooltip: the frame of the site's borders,
// on the plate and on the two sides of the arrow that show.
.tooltip--nested
  border-color: $border

  .tooltip-arrow::before
    border-color: $border

// In the flow of the page: a block as wide as its words, its arrow pointing
// down at the start of what stands under it rather than at its middle.
.tooltip--inline
  position: relative
  z-index: auto

  &.tooltip--top .tooltip-arrow
    left: $medium + $small

// Must be inline for getClientRects() to return one rect per line
.tooltip-text
  display: inline

  img
    display: block
    max-width: 100%
    height: auto

// Arrow - purely decorative, must not capture events.
//
// Drawn as a square rotated 45deg, not as the usual transparent-border
// triangle. The half of the square that hides behind the bubble carries the
// bubble's background and covers the bubble's outline where the arrow leaves
// it; the half that sticks out carries that same outline on the two sides
// that stay visible. A border triangle can do neither: it has no border of
// its own, so in the dark theme the bubble's outline simply stopped at the
// arrow.
//
// The rotation sits on the pseudo-element because the host span's own
// transform belongs to the viewport-clamp offset (arrowStyle above), which
// would overwrite a rotation written beside it.
.tooltip-arrow
  position: absolute
  pointer-events: none  // Critical: don't block clicks

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
  transition: opacity 0.15s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.tooltip-fade-enter-from,
.tooltip-fade-leave-to
  opacity: 0
</style>
