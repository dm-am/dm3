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
      <!-- The collapsed clamp is applied via CSS max-height (calc based on
           line-height × lines × em), driven by the .collapsed class. During
           transitions the JS handler pins an inline max-height override so the
           animation has concrete start and end values. -->
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
        <slot />
      </div>
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
    <!-- The footer is a block line (left part inline, right part floated) —
         NOT a flex row: Chrome's selection serializer emits a newline between
         element flex items, so a flex footer copied as "author\ndate". Inline
         flow plus float keeps the same geometry and copies as one line. The
         .copy-space span is a zero-width (font-size: 0) preserved-space text
         node: invisible, but Selection.toString() still yields a real space
         between the two parts. Both are the caller's slots, because their
         links come from the user slice and shared may not reach for it. -->
    <div v-if="$slots.left || $slots.right" class="bubble-footer">
      <span class="bubble-footer-left"><slot name="left" /></span
      ><span class="copy-space">{{ " " }}</span
      ><span class="bubble-footer-right"><slot name="right" /></span>
    </div>
  </article>
</template>

<script setup lang="ts">
/**
 * The green speech bubble of the review family: geometry, the tail, and the
 * three-line collapse with its chevron. What goes inside is the caller's —
 * plain text for a recommendation, server-rendered BBCode for a game review —
 * so the two surfaces share one shape instead of one copying the other's CSS.
 *
 * Extracted from TestimonialCard, which now renders through it unchanged. The
 * bubble knows nothing about authors or dates: the footer is a slot, because
 * its links come from the user slice and shared may not reach for it.
 */
import { ref, watch, nextTick, onMounted, onBeforeUnmount } from "vue";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";

const props = withDefaults(
  defineProps<{
    /** Enable expand/collapse for long content (gallery mode) */
    expandable?: boolean;
    /**
     * Identity of what is being shown. A change re-measures and re-collapses:
     * the gallery swaps one testimonial for another inside the same component
     * instance, and without this the new text would inherit the old one's
     * expanded state and overflow verdict.
     */
    contentKey?: string | number;
  }>(),
  { expandable: false, contentKey: undefined },
);

// Single source of truth for the collapsed-state budget. These values
// are shared between the JS measurement and the CSS variables so the
// visual clip and the overflow detection can never drift.
const MAX_COLLAPSED_LINES = 3;
const LINE_HEIGHT = 1.5;

const isExpanded = ref(false);
const isOverflowing = ref(false);
const contentRef = ref<HTMLElement | null>(null);

// Inline max-height applied during transitions. null = no inline override
// (the declarative CSS max-height via .collapsed class takes over).
const overrideMaxHeight = ref<string | null>(null);

function collapsedMaxHeightPx(): number {
  if (!contentRef.value) return 0;
  const fontSize = parseFloat(getComputedStyle(contentRef.value).fontSize);
  // Use floor to guarantee the value is strictly less than or equal to
  // MAX_COLLAPSED_LINES lines — browsers sometimes round line box metrics
  // upward, and we want to match what scrollHeight reports.
  return Math.floor(fontSize * LINE_HEIGHT * MAX_COLLAPSED_LINES);
}

function measureOverflow() {
  if (!props.expandable) {
    isOverflowing.value = false;
    return;
  }
  const el = contentRef.value;
  if (!el) {
    isOverflowing.value = false;
    return;
  }
  // Temporarily release the clamp so scrollHeight reflects the natural
  // content height, measure, then restore.
  const prevMaxHeight = el.style.maxHeight;
  el.style.maxHeight = "none";
  void el.offsetHeight;
  const natural = el.scrollHeight;
  el.style.maxHeight = prevMaxHeight;
  isOverflowing.value = natural > collapsedMaxHeightPx() + 1;
}

// Smooth expand/collapse. See TruncatedContent.vue for the full pattern
// rationale — same scrollHeight pin-and-animate approach with a mandatory
// `await nextTick()` between the start-state and target-state writes so
// Vue commits the start value to the DOM before the target value goes in.
// Without the nextTick pause, Vue coalesces the two writes into one render
// and the browser never sees the transition starting point.
async function toggleExpand() {
  const el = contentRef.value;
  if (!isOverflowing.value || !el) return;

  const collapsedPx = collapsedMaxHeightPx();

  if (!isExpanded.value) {
    // Collapsed → expanded: pin the current clamped height as the
    // transition start, let Vue render it, force reflow so the pinned value
    // becomes the browser's committed "before" state, then flip state and
    // pin the target (natural scrollHeight).
    //
    // The target MUST be measured only after the `.collapsed` class removal
    // has committed: `.collapsed` also switches `white-space`, and for
    // multi-paragraph content the natural height differs between the two
    // modes. Measuring before the flip yields an undersized target — the
    // animation lands short and the rest pops in at transitionend.
    overrideMaxHeight.value = `${collapsedPx}px`;
    await nextTick();
    void el.offsetHeight;
    isExpanded.value = true;
    await nextTick();
    void el.offsetHeight;
    overrideMaxHeight.value = `${el.scrollHeight}px`;
  } else {
    // Expanded → collapsed: mirror sequence.
    overrideMaxHeight.value = `${el.scrollHeight}px`;
    await nextTick();
    void el.offsetHeight;
    isExpanded.value = false;
    overrideMaxHeight.value = `${collapsedPx}px`;
  }
}

/** Manual user toggle (chevron click): clears the registry's pending bulk
 * action; registry-driven expand/collapse call toggleExpand directly. */
function manualToggle() {
  notifyExpandableChanged();
  toggleExpand();
}

function onTransitionEnd(e: TransitionEvent) {
  if (e.propertyName !== "max-height") return;
  if (!contentRef.value) return;
  if (isExpanded.value) {
    // Post-expand: release the constraint so late-loading content (e.g. a
    // link preview unfurling) can freely reflow.
    overrideMaxHeight.value = "none";
  } else {
    // Post-collapse: drop the inline override so the .collapsed class
    // drives max-height via its declarative calc().
    overrideMaxHeight.value = null;
  }
}

onMounted(() => {
  if (props.expandable) {
    nextTick(measureOverflow);
  }
});

// Re-measure when the shown content changes (gallery rotation).
watch(
  () => props.contentKey,
  () => {
    if (!props.expandable) return;
    isExpanded.value = false;
    overrideMaxHeight.value = null;
    nextTick(measureOverflow);
  },
);

// Exposed for a parent transition callback (the rotating gallery) and for its
// pause-on-expanded logic: isExpanded lets a carousel avoid swapping content
// out mid-read.
defineExpose({ measureContent: measureOverflow, isExpanded });

// Register with the global expand/collapse-all registry ONLY while the
// content actually overflows (has something to collapse). Short entries must
// not register, or the ScrollNav "Свернуть все" button would appear on pages
// where nothing is collapsible. Overflow is measured async, so a watch
// registers/unregisters as it flips.
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

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Animations"

.bubble-card
  margin: 0

// Speech bubble. `position: relative` is REQUIRED for the tail
// pseudo-element (`::after`) AND the absolutely-positioned toggle
// button — without it both fall through to an unrelated ancestor.
//
// Size-invariant layout:
//   The min-height reserves enough space for 3 lines of content plus
//   the symmetric top/bottom padding. Every entry — 1-line short,
//   3-line short, 10-line collapsed — renders at exactly the same
//   height, so rotation in the gallery never causes the footer row or
//   the surrounding page blocks to shift. The toggle, when present, is
//   absolutely positioned INSIDE the bottom padding strip, so it costs
//   no extra height and the bubble's bottom gap stays exactly equal to
//   the top one (owner rule: the chevron must not make the bottom
//   space bigger).
//
// Short-case flex alignment: `justify-content: center` keeps short
// content centered in the taller min-height box. For the has-toggle
// case (long content, chevron visible) we switch to `flex-start` so
// the content pins to the top and the toggle sits in the bottom strip.
.bubble
  position: relative
  display: flex
  flex-direction: column
  justify-content: center
  box-sizing: border-box
  padding: $medium + $tiny $medium + $small
  margin-bottom: $small
  border-radius: $bubble-radius
  background-color: $bg-highlight-green
  color: $text-on-green
  // Budget: 3 lines of content + symmetric top/bottom padding.
  // Concrete numbers resolve to 72 + 18 + 18 = 108px at default
  // font-size, which is both short-case and long-collapsed size.
  min-height: calc(var(--line-height, 1.5) * var(--max-lines, 3) * 1em + 2 * ($medium + $tiny))

  // Long content with a chevron toggle — content pinned to top; the bottom
  // padding stays the base symmetric 18px (the chevron lives inside it,
  // absolutely positioned).
  //
  // IMPORTANT: gate on `.has-toggle` (stable while the chevron is visible),
  // NOT on `.collapsed`. The collapsed class flips at the START of the
  // expand/collapse animation — if alignment were tied to it, the bubble
  // would jump the instant the animation begins.
  &.has-toggle
    justify-content: flex-start

  .bubble-content
    position: relative
    overflow: hidden
    white-space: pre-wrap
    word-wrap: break-word
    line-height: var(--line-height, 1.5)
    // Smooth max-height transition on expand/collapse. The JS handler pins
    // concrete start/end pixel values so CSS can animate between them (it
    // cannot transition from px to `none`). Curve matches
    // TruncatedContent.vue — a single site-wide "reveal" feel.
    //
    // Direction-aware duration: the quint ease-out curve spends ~60% of its
    // time on the last 20% of the path. On expand that long tail is masked
    // by the text appearing; on collapse the nearly-closed bubble visibly
    // "crawls" to the finish. $expand-duration open / 0.4s close makes both
    // directions read at the same perceived tempo.
    transition: max-height $expand-duration $expand-easing

  // Declarative collapsed max-height — hard clip at 3 lines via
  // `overflow: hidden`. The JS handler overlays an inline max-height during
  // the transition so the animation has concrete start/end pixel values.
  &.collapsed .bubble-content
    max-height: calc(var(--line-height) * var(--max-lines) * 1em)
    transition-duration: 0.4s
    // Collapsed preview reads as a continuous excerpt: paragraph breaks
    // collapse to spaces so the clamp never shows an awkward empty line.
    // Full paragraph formatting returns on expand.
    white-space: normal

  // Speech bubble arrow (on the left side)
  &::after
    position: absolute
    top: 100%
    left: $medium
    content: ''
    border: solid 8px transparent
    border-top-color: $bg-highlight-green
    border-left-color: $bg-highlight-green

// Expand toggle — a real <button> absolutely positioned inside the bubble's
// bottom PADDING strip (18px), so it adds zero height and the bottom gap
// stays equal to the top one. Absolute positioning also keeps the bubble's
// outer height identical between the "short entry" and "long collapsed"
// cases, so gallery rotation never shifts the surrounding layout.
//
// `bottom: $tiny` lifts the 18px-tall button off the bubble's lower edge:
// flush with it, the chevron read as if it were falling out of the bubble.
// `left: 50% + translateX(-50%)` is the classic pixel-perfect horizontal
// centering pattern. A real <button> preserves native keyboard focus and
// ARIA state; the tap target is intentionally the visual 18px strip — the
// owner's symmetric-padding rule wins over the 24px guideline here.
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
  border-radius: $small
  background: transparent
  color: $text-on-green
  cursor: pointer
  opacity: 0.65
  transition: opacity 0.2s ease, background-color 0.2s ease

  &:hover
    opacity: 1
    background-color: $hover-overlay

  &:focus-visible
    outline: 2px solid $text-on-green
    outline-offset: -2px
    opacity: 1

.toggle-icon
  display: block
  font-size: 16px
  // Chevron rotation matches the content curve/duration exactly — a single
  // synchronized motion instead of two competing tempos.
  transition: transform 0.4s $expand-easing

  &.expanded
    transform: rotate(180deg)
    transition-duration: $expand-duration

// Block line, NOT flex — see the template comment for why.
.bubble-footer
  display: block
  margin-top: 18px  // Clear the speech bubble arrow

  // Contain the float without creating a BFC (which could change width
  // interaction with the bubble arrow) — classic clearfix.
  &::after
    content: ""
    display: block
    clear: both

// float:right rather than flex space-between: same "pinned to the right edge
// of the same line" geometry, but the span stays in the inline copy flow
// (floats do not force line breaks in Selection.toString()).
.bubble-footer-right
  float: right

// Zero-width preserved space: invisible in layout (font-size: 0) but
// Selection.toString() still emits it as a real " " between the inline parts
// of the footer line. See Tabs.vue for the visible-space variant.
.copy-space
  white-space: pre
  font-size: 0
</style>
