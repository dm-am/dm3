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
import { Tooltip } from "@/shared/ui";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { useUserStore } from "@/entities/user";
import { userIsAdmin } from "@/entities/user";
import { registerExpandable } from "@/shared/lib/composables";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import dayjs from "dayjs";

const props = withDefaults(
  defineProps<{
    testimonial: WebsiteTestimonial;
    /** Enable expand/collapse for long text (gallery mode) */
    expandable?: boolean;
    /** Show admin delete controls */
    controls?: boolean;
    /** Search query for highlighting */
    searchQuery?: string;
  }>(),
  {
    expandable: false,
    controls: false,
    searchQuery: undefined,
  },
);

const userStore = useUserStore();
const testimonialStore = useTestimonialStore();

const canAdministrate = computed(
  () => props.controls && userIsAdmin(userStore.user),
);
const loading = ref(false);

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

function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}

function formatFullDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY [в] HH:mm");
}

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
    // target (natural scrollHeight). The ellipsis pseudo-element on
    // `.testimonial-content::after` fades out in parallel via a CSS
    // `opacity` transition with the SAME 0.55s / cubic-bezier curve
    // — both animations start on the same Vue render and finish on
    // the same frame, so there is no pop and no delay.
    overrideMaxHeight.value = `${collapsedPx}px`;
    await nextTick();
    void el.offsetHeight;
    isExpanded.value = true;
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

async function remove() {
  loading.value = true;
  await testimonialStore.removeTestimonial(props.testimonial.id);
  loading.value = false;
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

// Expose for parent transition callback (RandomTestimonials gallery).
defineExpose({ measureContent: measureOverflow });

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
      id: Symbol("Testimonial"),
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
        @click="toggleExpand"
      >
        <SvgIcon
          name="chevronDown"
          class="toggle-icon"
          :class="{ expanded: isExpanded }"
        />
      </button>
    </div>
    <div class="testimonial-footer">
      <user-link
        :user="testimonial.author"
        :search-query="searchQuery"
        :hide-badge="true"
      />
      <span class="testimonial-right">
        <Tooltip :text="formatFullDate(testimonial.createdUtc)">
          <secondary-text class="testimonial-date">
            {{ formatDate(testimonial.createdUtc) }}
          </secondary-text>
        </Tooltip>
        <secondary-text v-if="canAdministrate" class="testimonial-controls">
          <a v-if="!loading" @click="remove"> {{ symbols.close }} Удалить </a>
          <span v-else>...</span>
        </secondary-text>
      </span>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.testimonial
  margin: 0

// Speech bubble. `position: relative` is REQUIRED for the tail
// pseudo-element (`::after`) AND the absolutely-positioned toggle
// button — without it both fall through to an unrelated ancestor.
//
// Size-invariant layout:
//   The min-height reserves enough space for 3 lines of content,
//   the full top/bottom padding, AND the toggle strip (24px button
//   + 2px breath = 26px). Every testimonial — 1-line short, 3-line
//   short, 10-line collapsed — renders at exactly the same height,
//   so testimonial rotation in the gallery never causes the author
//   row or the surrounding page blocks to shift. The toggle, when
//   present, is absolutely positioned inside the reserved bottom
//   strip so its height cost is already budgeted into min-height
//   and does NOT grow the bubble.
//
// Short-case flex alignment: `justify-content: center` keeps
// short content centered in the taller min-height box. For the
// has-toggle case (long testimonial, chevron visible) we switch
// to `flex-start` + `padding-bottom: 0` so the content pins to
// the top and the absolutely-positioned toggle sits in the
// reserved bottom strip.
.testimonial-text
  position: relative
  display: flex
  flex-direction: column
  justify-content: center
  box-sizing: border-box
  padding: $medium + $tiny $medium + $small
  margin-bottom: $small
  border-radius: 20px
  background-color: $bg-highlight-green
  color: $text-on-green
  // Budget: 3 lines of content + full top padding + reserved
  // bottom strip (24px button + 2px breath below the chevron).
  // Concrete numbers resolve to 72 + 18 + 26 = 116px at default
  // font-size, which is both short-case and long-collapsed size.
  min-height: calc(var(--line-height, 1.5) * var(--max-lines, 3) * 1em + ($medium + $tiny) + 26px)

  // Long testimonial with a chevron toggle — content pinned to top,
  // `padding-bottom: 26px` = the reserved toggle strip (24px button
  // + 2px breath). The absolutely-positioned toggle lives inside the
  // padding, so it never overlaps the last line of expanded text. In
  // the collapsed state the math degenerates to exactly min-height:
  // 3 lines (72px) + 18px top + 26px bottom = 116px.
  //
  // IMPORTANT: gate on `.has-toggle` (stable while the chevron is
  // visible), NOT on `.collapsed`. The collapsed class flips at the
  // START of the expand/collapse animation — if padding/alignment
  // were tied to it, the bubble would jump by the padding delta the
  // instant the animation begins, making collapse feel abrupt and
  // out of tempo with expand. With the stable gate both directions
  // animate pure max-height — perfectly symmetric motion.
  &.has-toggle
    justify-content: flex-start
    padding-bottom: 26px

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
    // than expand at the same duration. 0.55s open / 0.4s close makes
    // both directions read at the same perceived tempo. The browser
    // picks the duration from the element's NEW state, and `.collapsed`
    // flips at the start of the animation — so this declaration drives
    // the expand direction, the override below drives collapse.
    transition: max-height 0.55s cubic-bezier(0.22, 1, 0.36, 1)

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
// bubble's reserved bottom strip (the `+ 22px` in `.testimonial-text`
// min-height). Absolute positioning keeps the bubble's outer height
// identical between the "short testimonial" and "long collapsed"
// cases, so gallery rotation never causes the surrounding layout to
// shift by the toggle's height.
//
// `bottom: $tiny` gives a 2px breathing gap between the chevron and
// the rounded bubble bottom edge. `left: 50% + translateX(-50%)` is
// the classic pixel-perfect horizontal centering pattern. Using a
// real <button> preserves native keyboard focus, ARIA state, and a
// 24px+ tap target (WCAG 2.5.5).
.testimonial-toggle
  position: absolute
  left: 50%
  bottom: $tiny
  transform: translateX(-50%)
  display: inline-flex
  align-items: center
  justify-content: center
  min-width: 36px
  min-height: 24px
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
    background-color: rgba(0, 0, 0, 0.06)

  &:focus-visible
    outline: 2px solid $text-on-green
    outline-offset: 2px
    opacity: 1

.toggle-icon
  display: block
  font-size: 20px
  // Chevron rotation matches the content curve/duration exactly —
  // a single synchronized motion instead of two competing tempos.
  // Direction-aware duration mirrors .testimonial-content below.
  transition: transform 0.4s cubic-bezier(0.22, 1, 0.36, 1)

  &.expanded
    transform: rotate(180deg)
    transition-duration: 0.55s

.testimonial-footer
  display: flex
  align-items: center
  justify-content: space-between
  gap: $small
  margin-top: 18px  // Clear the speech bubble arrow

.testimonial-right
  display: flex
  align-items: center
  gap: $small

.testimonial-date
  font-size: $font-size
  cursor: help

.testimonial-controls
  display: flex
  gap: $small
</style>
