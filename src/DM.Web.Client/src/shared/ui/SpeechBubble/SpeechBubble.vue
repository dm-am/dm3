<script setup lang="ts">
/** The bubble knows nothing of authors or dates: its links come from the user slice. */
import { ref, watch, nextTick, onMounted, onBeforeUnmount } from "vue";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";

const props = withDefaults(
  defineProps<{
    expandable?: boolean;
    /** Identity of what is shown: a change re-measures and re-collapses (the gallery reuses the instance). */
    contentKey?: string | number;
  }>(),
  { expandable: false, contentKey: undefined },
);

// Shared by the measurement and the CSS variables, so the clip and the overflow test never drift.
const MAX_COLLAPSED_LINES = 3;
const LINE_HEIGHT = 1.5;

const isExpanded = ref(false);
const isOverflowing = ref(false);
const contentRef = ref<HTMLElement | null>(null);

// The inline max-height during a transition; null leaves it to .collapsed.
const overrideMaxHeight = ref<string | null>(null);

function collapsedMaxHeightPx(): number {
  if (!contentRef.value) return 0;
  const fontSize = parseFloat(getComputedStyle(contentRef.value).fontSize);
  // Floor: browsers round line boxes up, and scrollHeight is what it is compared with.
  return Math.floor(fontSize * LINE_HEIGHT * MAX_COLLAPSED_LINES);
}

function measureOverflow() {
  const el = contentRef.value;
  if (!props.expandable || !el) {
    isOverflowing.value = false;
    return;
  }
  // The clamp is released for the measure.
  const prevMaxHeight = el.style.maxHeight;
  el.style.maxHeight = "none";
  void el.offsetHeight;
  const natural = el.scrollHeight;
  el.style.maxHeight = prevMaxHeight;
  isOverflowing.value = natural > collapsedMaxHeightPx() + 1;
}

// Pin, nextTick, measure, release on transitionend: without the nextTick Vue coalesces both writes.
async function toggleExpand() {
  const el = contentRef.value;
  if (!isOverflowing.value || !el) return;

  const collapsedPx = collapsedMaxHeightPx();

  if (!isExpanded.value) {
    // The target is measured after .collapsed goes: it switches white-space, and multi-paragraph text changes height.
    overrideMaxHeight.value = `${collapsedPx}px`;
    await nextTick();
    void el.offsetHeight;
    isExpanded.value = true;
    await nextTick();
    void el.offsetHeight;
    overrideMaxHeight.value = `${el.scrollHeight}px`;
  } else {
    overrideMaxHeight.value = `${el.scrollHeight}px`;
    await nextTick();
    void el.offsetHeight;
    isExpanded.value = false;
    overrideMaxHeight.value = `${collapsedPx}px`;
  }
}

/** A press of the reader's own clears the registry's pending bulk action. */
function manualToggle() {
  notifyExpandableChanged();
  toggleExpand();
}

function onTransitionEnd(e: TransitionEvent) {
  if (e.propertyName !== "max-height") return;
  if (!contentRef.value) return;
  if (isExpanded.value) {
    // Released, so late content (a link preview) can reflow.
    overrideMaxHeight.value = "none";
  } else {
    overrideMaxHeight.value = null;
  }
}

onMounted(() => {
  if (props.expandable) {
    nextTick(measureOverflow);
  }
});

watch(
  () => props.contentKey,
  () => {
    if (!props.expandable) return;
    isExpanded.value = false;
    overrideMaxHeight.value = null;
    nextTick(measureOverflow);
  },
);

// isExpanded lets the rotating gallery keep an entry being read.
defineExpose({ measureContent: measureOverflow, isExpanded });

// Registered only while something overflows, or "Свернуть все" would show where nothing collapses.
let unregister: (() => void) | null = null;

function syncRegistration() {
  const collapsible = props.expandable && isOverflowing.value;
  if (collapsible && !unregister) {
    unregister = registerExpandable({
      id: Symbol("SpeechBubble"),
      isExpanded: () => isExpanded.value,
      expand: () => {
        if (!isExpanded.value) toggleExpand();
      },
      collapse: () => {
        if (isExpanded.value) toggleExpand();
      },
    });
  } else if (!collapsible && unregister) {
    unregister();
    unregister = null;
  }
}

watch(isOverflowing, syncRegistration, { immediate: true });

onBeforeUnmount(() => {
  unregister?.();
  unregister = null;
});
</script>

<template>
  <article class="bubble-card">
    <div
      class="bubble"
      :class="{
        expandable,
        collapsed: expandable && !isExpanded && isOverflowing,
        'has-toggle': expandable && isOverflowing,
      }"
      :style="
        expandable
          ? {
              '--line-height': LINE_HEIGHT,
              '--max-lines': MAX_COLLAPSED_LINES,
            }
          : undefined
      "
    >
      <!-- During a transition an inline max-height gives the animation concrete ends. -->
      <div
        ref="contentRef"
        class="bubble-content"
        :style="
          overrideMaxHeight !== null
            ? { maxHeight: overrideMaxHeight }
            : undefined
        "
        @transitionend="onTransitionEnd"
      >
        <!-- Holds the actions' corner, so a long first line wraps short of them. -->
        <span
          v-if="$slots.actions"
          class="bubble-actions-space"
          aria-hidden="true"
        />
        <slot />
      </div>
      <!-- After the text in the DOM: a screen reader hears what is edited before the controls. -->
      <span v-if="$slots.actions" class="bubble-actions"
        ><slot name="actions"
      /></span>
      <button
        v-if="expandable && isOverflowing"
        type="button"
        class="bubble-toggle"
        :aria-expanded="isExpanded"
        :aria-label="isExpanded ? 'Свернуть' : 'Показать полностью'"
        @click="manualToggle"
      >
        <SvgIcon
          name="chevronDown"
          class="toggle-icon"
          :class="{ expanded: isExpanded }"
        />
      </button>
    </div>
    <!-- A block line, not a flex row: flex items copy as separate lines. The footer is the caller's slots:
         its links come from the user slice. -->
    <div v-if="$slots.left || $slots.right" class="bubble-footer">
      <span class="bubble-footer-left"><slot name="left" /></span
      ><span class="copy-space">{{ " " }}</span
      ><span class="bubble-footer-right"><slot name="right" /></span>
    </div>
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Animations" as *

// Named: the corner actions hold a share of the first line from the same edge.
$bubble-padding-x: $medium + $small

.bubble-card
  margin: 0

// Every entry renders at the same height, so the gallery's rotation shifts nothing; the toggle lives
// inside the bottom padding (the owner's rule: the chevron must not grow the bottom space).
.bubble
  position: relative
  display: flex
  flex-direction: column
  justify-content: center
  box-sizing: border-box
  padding: $medium + $tiny $bubble-padding-x
  margin-bottom: $small
  border-radius: $bubble-radius
  background-color: $bg-highlight-green
  color: $text-on-green
  min-height: calc(var(--line-height, 1.5) * var(--max-lines, 3) * 1em + 2 * ($medium + $tiny))

  // On .has-toggle, not .collapsed: that flips at the start of the animation and would jump the bubble.
  &.has-toggle
    justify-content: flex-start

  .bubble-content
    position: relative
    overflow: hidden
    white-space: pre-wrap
    overflow-wrap: break-word
    line-height: var(--line-height, 1.5)
    // Quint ease-out crawls at its end: collapse runs the slow step, so both directions read at one tempo.
    transition: max-height $expand-duration $expand-easing

  // Paragraph breaks collapse to spaces in the excerpt.
  &.collapsed .bubble-content
    max-height: calc(var(--line-height) * var(--max-lines) * 1em)
    transition-duration: $duration-slow
    white-space: normal

  &::after
    position: absolute
    top: 100%
    left: $medium
    content: ''
    border: solid 8px transparent
    border-top-color: $bg-highlight-green
    border-left-color: $bg-highlight-green

// Inside the bottom padding strip, lifted $tiny off the edge; the tap target is the 18px strip by the owner's rule.
.bubble-toggle
  position: absolute
  left: 50%
  bottom: $tiny
  transform: translateX(-50%)
  display: inline-flex
  align-items: center
  justify-content: center
  min-width: 36px
  min-height: $medium + $tiny
  padding: 0 $small
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text-on-green
  cursor: pointer
  opacity: $opacity-soft
  transition: opacity $transition-normal, background-color $transition-normal

  &:hover
    opacity: 1
    background-color: $hover-overlay

  &:focus-visible
    +focus-ring($inward: true)
    outline-color: $text-on-green
    opacity: 1

.toggle-icon
  display: block
  font-size: 16px
  // The content's curve and duration: one motion.
  transition: transform $duration-slow $expand-easing

  &.expanded
    transform: rotate(180deg)
    transition-duration: $expand-duration

// Pinned to the bubble, not to the centred text, or the icons would stand halfway down a one-line entry.
$actions-top: $small
$actions-right: $small + $tiny
$actions-gap: $minor
$action-glyph-inset: $minor

.bubble-actions
  position: absolute
  top: $actions-top
  right: $actions-right
  display: flex
  gap: $actions-gap

  // The slot renders only comments for a viewer who may do nothing.
  &:empty
    display: none

  // The bubble's ink: $link has nothing to stand against on green.
  :deep(button:focus-visible)
    +focus-ring($inward: true)
    outline-color: $text-on-green

// Only the first line can meet the icons; sized for both, since a slot does not say how many it holds.
.bubble-actions-space
  float: right
  width: $actions-right + 2 * $bubble-action-size + $actions-gap + $medium - $action-glyph-inset - $bubble-padding-x
  height: 1em

.bubble:has(> .bubble-actions:empty) .bubble-actions-space
  display: none

// A block line, not flex: see the template.
.bubble-footer
  display: block
  margin-top: 18px

  // A clearfix, not a BFC, which could change the width against the arrow.
  &::after
    content: ""
    display: block
    clear: both

// A float, not space-between: the span stays in the inline copy flow.
.bubble-footer-right
  float: right
</style>
