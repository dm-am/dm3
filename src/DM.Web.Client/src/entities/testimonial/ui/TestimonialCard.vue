<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  nextTick,
  onMounted,
  onBeforeUnmount,
} from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import type { UserRef } from "@/shared/api/models/common";
import { UserLink } from "@/entities/user/@x/testimonial";
import { Tooltip } from "@/shared/ui/Tooltip";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    testimonial: WebsiteTestimonial;
    /** Enable expand/collapse for long text (gallery mode) */
    expandable?: boolean;
    /** Search query for highlighting */
    searchQuery?: string;
    /**
     * Who the testimonial is ABOUT — set by both profile recommendation
     * lists, received and written. Extends the footer to
     * "<author> о <recipient>": the author keeps the regular footer link
     * treatment (the person "speaking" in the bubble stays primary), the
     * recipient renders as a muted link. The line is composed here and
     * nowhere else: a caller says whether there is a recipient at all,
     * never how the two names are joined.
     * When unset, the footer names only the author — the
     * /about/testimonials gallery and the home page show reviews of the
     * site itself, which have no recipient.
     */
    about?: UserRef;
  }>(),
  {
    expandable: false,
    searchQuery: undefined,
    about: undefined,
  },
);

// Single source of truth for the collapsed-state budget. These values
// are shared between the JS measurement and the CSS variables so the
// visual clip and the overflow detection can never drift.
const MAX_COLLAPSED_LINES = 3;
const LINE_HEIGHT = 1.5;

// Expand/collapse state
const isExpanded = ref(false);
const isOverflowing = ref(false);
const contentRef = ref<HTMLElement | null>(null);

// Inline max-height applied during transitions. null = no inline override
// (the declarative CSS max-height via .collapsed class takes over).
const overrideMaxHeight = ref<string | null>(null);

// Trimmed original text — removes accidental leading/trailing blank lines
// so they never eat into the 3-line truncation budget.
const displayText = computed(() => props.testimonial.text.trim());

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
  // content height, measure, then restore. `display` is always `block`
  // now that the pseudo-element ellipsis replaced `-webkit-line-clamp`,
  // so no display-mode override is needed.
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
    // transition start, let Vue render it, force reflow so the
    // pinned value becomes the browser's committed "before" state
    // for the max-height transition, then flip state and pin the
    // target (natural scrollHeight).
    //
    // The target MUST be measured only after the `.collapsed` class
    // removal has committed: `.collapsed` also switches `white-space`
    // (normal ↔ pre-wrap), and for multi-paragraph text the natural
    // height differs between the two modes. Measuring before the flip
    // yields an undersized target — the animation lands short and the
    // remaining pixels pop in at transitionend as a visible jump.
    overrideMaxHeight.value = `${collapsedPx}px`;
    await nextTick();
    void el.offsetHeight;
    isExpanded.value = true;
    await nextTick();
    void el.offsetHeight;
    overrideMaxHeight.value = `${el.scrollHeight}px`;
  } else {
    // Expanded → collapsed: mirror sequence. Pin start to natural
    // height, flip state (adding `.collapsed`, which simultaneously
    // triggers the max-height transition toward the clamped budget
    // AND the opacity transition that fades the ellipsis pseudo-
    // element from 0 to 1). Both finish on the same frame.
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
    // Post-expand: release the constraint so late-loading content (e.g.
    // link preview unfurls) can freely reflow.
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

// Re-measure when the displayed testimonial changes (gallery rotation).
watch(
  () => props.testimonial.id,
  () => {
    if (!props.expandable) return;
    isExpanded.value = false;
    overrideMaxHeight.value = null;
    nextTick(measureOverflow);
  },
);

// Expose for parent transition callback (RandomTestimonials gallery) and
// for pause-on-expanded logic in rotating galleries (contract: isExpanded
// lets a parent carousel avoid swapping out a testimonial mid-read).
defineExpose({ measureContent: measureOverflow, isExpanded });

// Register with the global expand/collapse-all registry ONLY while the
// testimonial actually overflows (has something to collapse). Short ones
// must not register, or the ScrollNav "Свернуть все" button would appear on
// pages where nothing is collapsible. Overflow is measured async, so a watch
// registers/unregisters as it flips.
let unregister: (() => void) | null = null;

function syncRegistration() {
  const collapsible = props.expandable && isOverflowing.value;
  if (collapsible && !unregister) {
    unregister = registerExpandable({
      id: Symbol("TestimonialCard"),
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
onBeforeUnmount(() => unregister?.());
</script>

<template>
  <article class="testimonial">
    <div
      class="testimonial-text"
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
      <!-- Plain text only, NO BBCode. The collapsed clamp is applied via
           CSS max-height (calc based on line-height × lines × em), driven
           by the .collapsed class. During transitions the JS handler pins
           an inline max-height override so the animation has concrete
           start and end values. -->
      <div
        ref="contentRef"
        class="testimonial-content"
        :style="
          overrideMaxHeight !== null
            ? { maxHeight: overrideMaxHeight }
            : undefined
        "
        @transitionend="onTransitionEnd"
      >
        <span
          v-if="searchQuery"
          v-html="highlightMatch(displayText, searchQuery)"
        /><template v-else>{{ displayText }}</template>
      </div>
      <button
        v-if="expandable && isOverflowing"
        type="button"
        class="testimonial-toggle"
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
    <div class="testimonial-footer">
      <!-- The author — the person "speaking" in the bubble — always leads.
           With `about` set (both profile recommendation lists) the line extends
           to "<author> о <recipient>", recipient as a muted link. Spaces
           around "о" are real text nodes ({{ " " }}) so the whole line
           copies as plain text (same pattern as Tabs.vue).

           The footer itself is a block line (author inline, date/controls
           in a float:right span) — NOT a flex row: Chrome's selection
           serializer emits a newline between element flex items, so a flex
           footer copied as "author\ndate". Inline flow + float keeps the
           same one-line "author left, date right" geometry while copying
           as a single line. The .copy-space spans are zero-width
           (font-size: 0) preserved-space text nodes: invisible, but
           Selection.toString() still yields a real space between parts. -->
      <span class="testimonial-authorship">
        <user-link
          :user="testimonial.author"
          :search-query="searchQuery"
          :hide-badge="true"
        /><template v-if="about"
          >{{ " " }}<span class="testimonial-about-connector">о</span>{{ " "
          }}<user-link
            class="testimonial-about-link"
            :user="about"
            :search-query="searchQuery"
            :hide-badge="true"
        /></template> </span
      ><span class="copy-space">{{ " " }}</span
      ><span class="testimonial-right"
        ><Tooltip :text="formatDateFull(testimonial.createdUtc)" focusable
          ><secondary-text class="testimonial-date">
            {{ formatDate(testimonial.createdUtc) }}
          </secondary-text></Tooltip
        ><slot name="controls"></slot
      ></span>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Animations"

.testimonial
  margin: 0

// Speech bubble. `position: relative` is REQUIRED for the tail
// pseudo-element (`::after`) AND the absolutely-positioned toggle
// button — without it both fall through to an unrelated ancestor.
//
// Size-invariant layout:
//   The min-height reserves enough space for 3 lines of content plus
//   the symmetric top/bottom padding. Every testimonial — 1-line
//   short, 3-line short, 10-line collapsed — renders at exactly the
//   same height, so testimonial rotation in the gallery never causes
//   the author row or the surrounding page blocks to shift. The
//   toggle, when present, is absolutely positioned INSIDE the bottom
//   padding strip, so it costs no extra height and the bubble's
//   bottom gap stays exactly equal to the top one (owner rule: the
//   chevron must not make the bottom space bigger).
//
// Short-case flex alignment: `justify-content: center` keeps
// short content centered in the taller min-height box. For the
// has-toggle case (long testimonial, chevron visible) we switch
// to `flex-start` so the content pins to the top and the
// absolutely-positioned toggle sits in the bottom padding strip.
.testimonial-text
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

  // Long testimonial with a chevron toggle — content pinned to top;
  // the bottom padding stays the base symmetric 18px (the chevron
  // lives inside it, absolutely positioned).
  //
  // IMPORTANT: gate on `.has-toggle` (stable while the chevron is
  // visible), NOT on `.collapsed`. The collapsed class flips at the
  // START of the expand/collapse animation — if alignment were tied
  // to it, the bubble would jump the instant the animation begins.
  &.has-toggle
    justify-content: flex-start

  .testimonial-content
    position: relative
    overflow: hidden
    white-space: pre-wrap
    word-wrap: break-word
    line-height: var(--line-height, 1.5)
    // Smooth max-height transition on expand/collapse. The JS handler
    // pins concrete start/end pixel values so CSS can animate between
    // them (it cannot transition from px to `none`). Curve matches
    // TruncatedContent.vue — a single site-wide "reveal" feel.
    //
    // Direction-aware duration: the quint ease-out curve spends ~60%
    // of its time on the last 20% of the path. On expand that long
    // tail is masked by the text appearing; on collapse the nearly-
    // closed bubble visibly "crawls" to the finish and feels slower
    // than expand at the same duration. $expand-duration open / 0.4s
    // close makes both directions read at the same perceived tempo. The
    // browser picks the duration from the element's NEW state, and
    // `.collapsed` flips at the start of the animation — so this
    // declaration drives the expand direction, the override below drives
    // collapse. Tokens shared site-wide via _Animations.sass.
    transition: max-height $expand-duration $expand-easing

  // Declarative collapsed max-height — hard clip at 3 lines via
  // `overflow: hidden`. The JS handler overlays an inline max-height
  // during the expand/collapse transition so the animation has
  // concrete start/end pixel values (CSS cannot transition from a
  // fixed length to `none`).
  &.collapsed .testimonial-content
    max-height: calc(var(--line-height) * var(--max-lines) * 1em)
    transition-duration: 0.4s
    // Collapsed preview reads as a continuous excerpt: paragraph breaks
    // (blank lines from the source \n\n) collapse to spaces so the clamp
    // never shows an awkward empty line. Full paragraph formatting returns
    // on expand (base .testimonial-content keeps white-space: pre-wrap).
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

// Expand toggle — a real <button> absolutely positioned inside the
// bubble's bottom PADDING strip (18px), so it adds zero height and
// the bottom gap stays equal to the top one. Absolute positioning
// also keeps the bubble's outer height identical between the "short
// testimonial" and "long collapsed" cases, so gallery rotation never
// causes the surrounding layout to shift by the toggle's height.
//
// `bottom: 0` pins the 18px-tall button exactly over the padding
// strip (no overlap with the last text line). `left: 50% +
// translateX(-50%)` is the classic pixel-perfect horizontal
// centering pattern. A real <button> preserves native keyboard
// focus and ARIA state; the tap target is intentionally the visual
// 18px strip — the owner's symmetric-padding rule wins over the
// 24px guideline here.
.testimonial-toggle
  position: absolute
  left: 50%
  bottom: 0
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
  // Chevron rotation matches the content curve/duration exactly —
  // a single synchronized motion instead of two competing tempos.
  // Direction-aware duration mirrors .testimonial-content below.
  transition: transform 0.4s $expand-easing

  &.expanded
    transform: rotate(180deg)
    transition-duration: $expand-duration

// Block line, NOT flex: element flex items copy with newlines between them
// (Chrome selection serializer), so the old flex footer copied as
// "author\ndate". Inline authorship + float:right date keeps the identical
// single-line geometry with clean one-line copy.
.testimonial-footer
  display: block
  margin-top: 18px  // Clear the speech bubble arrow

  // Contain the float without creating a BFC (which could change width
  // interaction with the bubble arrow) — classic clearfix.
  &::after
    content: ""
    display: block
    clear: both

// Zero-width preserved space: invisible in layout (font-size: 0) but
// Selection.toString() still emits it as a real " " between the inline
// parts of the footer line. See Tabs.vue for the visible-space variant.
.copy-space
  white-space: pre
  font-size: 0

// Recipient link in the "<author> о <recipient>" footer line (both profile
// recommendation lists): recedes to the muted treatment so the
// author — the person "speaking" in the bubble — stays the visually
// primary link. :deep is required for the <a>: UserLink's root <span>
// receives this component's scope attribute via class fallthrough, but
// the link inside it does not.
.testimonial-about-link
  :deep(a)
    +muted-link

// The "о" connector shares the muted treatment of the recipient link —
// the whole "о <recipient>" clause recedes as one quiet unit.
.testimonial-about-connector
  color: $text-muted

// float:right replaces the old flex space-between: same "pinned to the
// right edge of the same line" geometry, but the span stays in the inline
// copy flow (floats do not force line breaks in Selection.toString()).
.testimonial-right
  float: right

// secondary-text renders a block <div>; inside the float's inline copy
// flow both must be inline, or Chrome emits a newline at their block
// boundaries and the footer copies as "author\ndate" again.
.testimonial-date
  display: inline
  font-size: $font-size
  cursor: help
</style>
