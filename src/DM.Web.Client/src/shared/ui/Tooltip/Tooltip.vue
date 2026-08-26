<template>
  <span
    ref="triggerRef"
    class="tooltip-trigger"
    @mouseenter="handleMouseEnter"
    @mouseleave="handleMouseLeave"
    @focusin="handleFocusIn"
    @focusout="handleFocusOut"
    @touchstart="handleTouchStart"
    @keydown.esc="hide"
    :aria-describedby="isVisible ? tooltipId : undefined"
    :tabindex="focusable ? 0 : undefined"
  >
    <slot />
  </span>
  <Teleport to="body">
    <Transition name="tooltip-fade">
      <div
        v-if="isVisible && !disabled && (text || hasContentSlot)"
        ref="tooltipRef"
        :id="tooltipId"
        role="tooltip"
        class="tooltip"
        :class="`tooltip--${position.placement}`"
        :style="tooltipStyle"
        @mouseenter="handleTooltipMouseEnter"
        @mouseleave="handleTooltipMouseLeave"
        @focusout="handleFocusOut"
      >
        <span ref="contentRef" class="tooltip-text">
          <slot name="content">{{ text }}</slot>
        </span>
        <span class="tooltip-arrow" :style="arrowStyle" />
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  nextTick,
  onMounted,
  onUnmounted,
  toRef,
  useSlots,
} from "vue";
import { useTooltip } from "./useTooltip";
import type { TooltipPlacement } from "./types";

const slots = useSlots();
const hasContentSlot = computed(() => !!slots.content);

// Shows text/content on hover, keyboard focus, or touch tap (WCAG 1.4.13).
// Trigger is only reachable by Tab when `focusable` is set (the caller's
// element already provides its own focus target otherwise, e.g. a <button>).
// Tab can continue into interactive tooltip content (links, buttons) without
// closing it — see handleFocusOut below.
//
// The tooltip describes its trigger and never names it: `aria-describedby`
// lands on the wrapper span, and only while the tooltip is on screen. An
// icon-only button wrapped here carries its own `aria-label` — a description
// is announced after a name, not instead of one.
const props = withDefaults(
  defineProps<{
    /** Tooltip text. If undefined/empty, tooltip won't show */
    text?: string;
    placement?: TooltipPlacement;
    /** Delay in ms before showing/hiding */
    delay?: number;
    disabled?: boolean;
    /** Make the trigger span keyboard-focusable (tabindex="0"). Use when the
     * slotted content isn't itself a focusable element (e.g. plain text). */
    focusable?: boolean;
  }>(),
  {
    text: undefined,
    placement: "top",
    delay: 150,
    disabled: false,
    focusable: false,
  },
);

// Generate unique ID for ARIA
const tooltipId = `tooltip-${Math.random().toString(36).slice(2, 9)}`;

const triggerRef = ref<HTMLElement | null>(null);
const tooltipRef = ref<HTMLElement | null>(null);
const contentRef = ref<HTMLElement | null>(null);
const shrunkWidth = ref<number | null>(null);

const { isVisible, position, show, hide, updatePosition } = useTooltip(
  triggerRef,
  tooltipRef,
  toRef(props, "placement"),
);

// Solve shrinkwrap problem using canvas text measurement. Plain-text
// tooltips only: rich `#content` popovers skip the shrink (see
// measureAndShrink) and size themselves via CSS width/max-width.
const MAX_TOOLTIP_WIDTH = 340;
const measurePhase = ref<"measure" | "final">("measure");

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
  if (!tooltipRef.value || !contentRef.value) return;

  // Rich slot content (block layout, nested elements, grids) cannot be
  // simulated by the canvas TEXT measurement: textContent glues all nodes
  // into one pseudo-line, under-measures the real block layout and the
  // resulting width clips the popover (eaten right padding, cut columns).
  // Skip the shrink entirely — CSS `width: max-content` + `max-width: 340px`
  // already size slot popovers from their content.
  if (hasContentSlot.value) {
    measurePhase.value = "final";
    updatePosition();
    return;
  }

  if (measurePhase.value === "measure") {
    const text = contentRef.value.textContent || "";
    if (!text.trim()) {
      measurePhase.value = "final";
      return;
    }

    // Get computed font, padding, and border
    const style = getComputedStyle(tooltipRef.value);
    const font = `${style.fontSize} ${style.fontFamily}`;
    const paddingLeft = parseFloat(style.paddingLeft) || 0;
    const paddingRight = parseFloat(style.paddingRight) || 0;
    const borderLeft = parseFloat(style.borderLeftWidth) || 0;
    const borderRight = parseFloat(style.borderRightWidth) || 0;
    const horizontalExtra =
      paddingLeft + paddingRight + borderLeft + borderRight;

    // Max text width (content area)
    const maxTextWidth = MAX_TOOLTIP_WIDTH - horizontalExtra;

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
    measurePhase.value = "final";
    nextTick(() => updatePosition());
  }
}

// Simple hover state machine
let showTimer: ReturnType<typeof setTimeout> | null = null;
let hideTimer: ReturnType<typeof setTimeout> | null = null;

function clearAllTimers() {
  if (showTimer) {
    clearTimeout(showTimer);
    showTimer = null;
  }
  if (hideTimer) {
    clearTimeout(hideTimer);
    hideTimer = null;
  }
}

function handleMouseEnter() {
  if (props.disabled) return;
  clearAllTimers();
  showTimer = setTimeout(show, props.delay);
}

function handleMouseLeave() {
  clearAllTimers();
  hideTimer = setTimeout(hide, props.delay);
}

function handleTooltipMouseEnter() {
  // Cancel pending hide when entering tooltip
  clearAllTimers();
}

function handleTooltipMouseLeave() {
  // Schedule hide when leaving tooltip
  clearAllTimers();
  hideTimer = setTimeout(hide, props.delay);
}

// Keyboard: show immediately on focus (no delay), hide when focus leaves
function handleFocusIn() {
  if (props.disabled) return;
  clearAllTimers();
  show();
}

function handleFocusOut(e: FocusEvent) {
  // Ignore focus moves within the trigger (e.g. between its children)
  const next = e.relatedTarget as Node | null;
  if (next && triggerRef.value?.contains(next)) return;
  // Keep tooltip open when focus moves into the tooltip itself, symmetric
  // to handleTooltipMouseEnter — lets Tab reach links inside tooltip content
  // (e.g. award popovers) instead of closing on the way there
  if (next && tooltipRef.value?.contains(next)) return;
  clearAllTimers();
  hide();
}

// Touch: show immediately, hide on touch outside
function handleTouchStart() {
  if (props.disabled) return;
  clearAllTimers();
  if (!isVisible.value) {
    show();
  }
}

function handleDocumentTouch(e: TouchEvent) {
  if (!isVisible.value) return;
  const target = e.target as Node;
  if (
    triggerRef.value?.contains(target) ||
    tooltipRef.value?.contains(target)
  ) {
    return;
  }
  hide();
}

onMounted(() => {
  document.addEventListener("touchstart", handleDocumentTouch, {
    passive: true,
  });
});

// Update position when tooltip becomes visible
watch(isVisible, async (visible) => {
  if (visible) {
    // Reset to measurement phase
    measurePhase.value = "measure";
    shrunkWidth.value = null;
    await nextTick();
    measureAndShrink();
  } else {
    shrunkWidth.value = null;
    measurePhase.value = "measure";
  }
});

// Re-measure when text changes while tooltip is visible
watch(
  () => props.text,
  async () => {
    if (isVisible.value) {
      // Reset and re-measure
      measurePhase.value = "measure";
      shrunkWidth.value = null;
      await nextTick();
      measureAndShrink();
    }
  },
);

// Cleanup
onUnmounted(() => {
  clearAllTimers();
  document.removeEventListener("touchstart", handleDocumentTouch);
});

const tooltipStyle = computed(() => ({
  top: `${position.value.top}px`,
  left: `${position.value.left}px`,
  ...(shrunkWidth.value ? { width: `${shrunkWidth.value}px` } : {}),
}));

// Arrow offset style - adjusts arrow position when tooltip is clamped to viewport
const arrowStyle = computed(() => {
  const offset = position.value.arrowOffset;
  if (!offset) return {};
  // For top/bottom placement, offset moves arrow horizontally
  // translateX is relative to the arrow's centered position (50%)
  return { transform: `translateX(calc(-50% + ${offset}px))` };
});
</script>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

.tooltip-trigger
  // No styles - behaves as inline text
  display: inline

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
  border: 1px solid $tooltip-border
  border-radius: $border-radius
  box-shadow: 0 2px 8px $shadow-color
  white-space: pre-line
  // Allow hovering over tooltip content
  pointer-events: auto

// Must be inline for getClientRects() to return one rect per line
.tooltip-text
  display: inline

  img
    display: block
    max-width: 100%
    height: auto
    border-radius: $tiny

// Arrow - purely decorative, must not capture events
.tooltip-arrow
  position: absolute
  width: 0
  height: 0
  border: 6px solid transparent
  pointer-events: none  // Critical: don't block clicks

.tooltip--top .tooltip-arrow
  bottom: -10px
  left: 50%
  transform: translateX(-50%)
  border-top-color: $tooltip-bg
  border-bottom-width: 0

.tooltip--bottom .tooltip-arrow
  top: -10px
  left: 50%
  transform: translateX(-50%)
  border-bottom-color: $tooltip-bg
  border-top-width: 0

.tooltip--left .tooltip-arrow
  right: -10px
  top: 50%
  transform: translateY(-50%)
  border-left-color: $tooltip-bg
  border-right-width: 0

.tooltip--right .tooltip-arrow
  left: -10px
  top: 50%
  transform: translateY(-50%)
  border-right-color: $tooltip-bg
  border-left-width: 0

// Fade transition
.tooltip-fade-enter-active,
.tooltip-fade-leave-active
  transition: opacity 0.15s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.tooltip-fade-enter-from,
.tooltip-fade-leave-to
  opacity: 0
</style>
