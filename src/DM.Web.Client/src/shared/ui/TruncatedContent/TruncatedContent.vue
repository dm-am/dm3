<script setup lang="ts">
/**
 * Unified "показать полностью" truncation primitive.
 *
 * Wraps the shared useContentTruncation composable with a consistent
 * template + style for every caller (Topic, GamePost, Comment, ChatMessage,
 * ProfileBestPost). Handles:
 *
 *   - overflow detection against a (reactive) max-height
 *   - symmetric whitespace trimming (leading AND trailing) via the composable
 *     so author-inserted blank lines never eat into the truncation budget
 *   - collapsed-state media shrinkage (img/video/iframe capped to a fraction
 *     of maxHeight via CSS var + :deep selector) so over-sized character
 *     portraits / maps never blow the budget or get clipped mid-image
 *   - "показать полностью" button rendered only when truncated & collapsed
 *   - SMOOTH max-height transition on expand/collapse (see below)
 *
 * Visual contract: the collapsed view is a clean hard cut at maxHeight.
 * No gradient/mask fade — it would degrade the last visible line and
 * duplicate the explicit "показать полностью" affordance below. This
 * matches the modern hard-cut convention used by Twitter, Reddit, Facebook,
 * GitHub, LinkedIn, etc.
 *
 * Smooth expansion (pattern):
 *   CSS cannot transition from a fixed px value to `none`, so the classic
 *   "let it grow to auto" recipe does not apply. Instead, on user click we:
 *     1. Read scrollHeight (the natural expanded height) inside the click
 *        handler — a DOM read is safe in an event callback, just not
 *        inside a computed (which must be pure).
 *     2. Animate max-height from the current clamped value to that
 *        concrete px. The CSS transition picks it up because both ends
 *        are concrete lengths.
 *     3. On `transitionend`, release the constraint entirely (`max-height:
 *        none`) so the content can reflow freely afterwards (e.g. if images
 *        finish loading post-expand).
 *   Collapse is the mirror: snapshot current px, force reflow, then animate
 *   down to the collapsed px.
 *
 * The expand control is a real <button> with aria-expanded, focus-visible,
 * and an enlarged tap target — accessible by default for keyboard, mouse,
 * touch, and assistive technology.
 *
 * Callers put the content into the default slot. The root is a flex column
 * so any number of block children compose naturally.
 */
import { computed, nextTick, onBeforeUnmount, ref } from "vue";
import { useContentTruncation } from "@/shared/lib/composables/useContentTruncation";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables";

const props = withDefaults(
  defineProps<{
    /** Max collapsed height in px. Reactive through the parent render cycle. */
    maxHeight?: number;
    /** Enable truncation. When false, content always renders fully. */
    truncatable?: boolean;
    /**
     * Arbitrary value watched for changes — when it updates, the composable
     * re-trims the content and re-checks overflow. Pass the raw HTML string
     * or any identity-stable reactive value representing the content.
     */
    watchKey?: unknown;
    /** Optional hook invoked after the content element is mounted/updated. */
    onContentMounted?: (el: HTMLElement) => void;
    /**
     * Fraction of maxHeight that media children (img/video/iframe) are
     * allowed to occupy in the collapsed view. Default 0.6 — leaves 40% of
     * the budget for text context around the media.
     */
    mediaBudget?: number;
  }>(),
  {
    maxHeight: 150,
    truncatable: false,
    watchKey: undefined,
    onContentMounted: undefined,
    mediaBudget: 0.6,
  },
);

// Wrap props in computeds so the composable's internal tracking stays
// reactive across prop updates (useContentTruncation reads .value on refs).
const maxHeightRef = computed(() => props.maxHeight);
const enabledRef = computed(() => props.truncatable);
const watchContentRef = computed(() => props.watchKey);

const {
  setContentRef,
  contentRef,
  needsTruncation,
  isExpanded,
  toggleExpand: toggleExpandRaw,
} = useContentTruncation({
  maxHeight: maxHeightRef,
  enabled: enabledRef,
  watchContent: watchContentRef,
  onContentMounted: props.onContentMounted,
});

// True while the user sees the truncated (collapsed) version.
const isCollapsed = computed(
  () => needsTruncation.value && !isExpanded.value,
);

// Inline max-height override driven by the click handler. Values:
//   null      — no override, use the declarative contentStyle below.
//   "<N>px"   — explicit height used during the transition (start or end).
//   "none"    — constraint released post-expand so the content can reflow.
//
// This override is ONLY written from toggleExpand and onTransitionEnd.
// Do NOT add a watch that resets it on state changes — that would fire
// synchronously during toggleExpand (because it flips isExpanded) and
// overwrite the animation's target value before Vue applies it to the
// DOM, producing the exact "expansion is instant" bug the old comment
// warned about. The scrollHeight-based pattern relies on keeping the
// override write order strictly linear.
const overrideMaxHeight = ref<string | null>(null);

// Content style: collapsed gets the max-height + CSS variables that drive
// media shrinkage; expanded releases the constraint entirely so images
// return to their natural size. Pure function of state — no DOM reads.
const contentStyle = computed(() => {
  const max = maxHeightRef.value;
  const mediaMax = Math.floor(max * props.mediaBudget);

  if (overrideMaxHeight.value !== null) {
    // Inline override wins during animation so transitions run smoothly.
    return {
      maxHeight: overrideMaxHeight.value,
      "--truncated-max-h": `${max}px`,
      "--truncated-media-max": `${mediaMax}px`,
    };
  }
  if (!needsTruncation.value) return {};
  if (isExpanded.value) return { maxHeight: "none" };

  return {
    maxHeight: `${max}px`,
    "--truncated-max-h": `${max}px`,
    "--truncated-media-max": `${mediaMax}px`,
  };
});

// Smooth expand/collapse via the scrollHeight pin-and-animate pattern.
//
// Why this is harder than it looks in Vue:
//   1. CSS cannot transition from a fixed `<length>` to `none` or vice
//      versa, so the naive `max-height: 150px <-> max-height: none`
//      swap is instant.
//   2. Vue coalesces synchronous ref writes into a single render pass —
//      setting `override` to "150px" and then "500px" in the same tick
//      only produces one DOM update (to "500px"), so the browser never
//      sees the intermediate value that the transition needs as its
//      starting point.
//   3. Therefore the click handler must pause (`await nextTick()`)
//      between the start-state write and the target-state write so Vue
//      commits the start state to the DOM first, and must force a
//      reflow (`void offsetHeight`) before the target write so the
//      browser registers the committed start as the transition origin.
async function toggleExpand() {
  const el = contentRef.value;
  if (!el || !needsTruncation.value) {
    toggleExpandRaw();
    return;
  }

  const collapsedPx = maxHeightRef.value;

  if (!isExpanded.value) {
    // ─── Collapsed → expanded ─────────────────────────────────────
    // Pin the start state to the current clamped height. Override is
    // checked before `needsTruncation && !isExpanded` in contentStyle,
    // so setting it here keeps the DOM at maxHeight: `${collapsedPx}px`
    // even after the next state flip.
    overrideMaxHeight.value = `${collapsedPx}px`;
    await nextTick();

    // Commit the start state to the browser rendering pipeline so it
    // is the "before-change" value of the upcoming transition.
    void el.offsetHeight;

    // Flip state and pin the end state to the measured natural height.
    // Vue will render a single diff (maxHeight: collapsedPx → scrollHeight)
    // which the browser animates.
    toggleExpandRaw();
    overrideMaxHeight.value = `${el.scrollHeight}px`;
  } else {
    // ─── Expanded → collapsed ─────────────────────────────────────
    // Snapshot the current natural height (post any late layout shifts
    // from image loads, etc.) and pin it as the start state. This
    // replaces the post-expand "none" override that was set by the
    // previous onTransitionEnd.
    overrideMaxHeight.value = `${el.scrollHeight}px`;
    await nextTick();

    void el.offsetHeight;

    toggleExpandRaw();
    overrideMaxHeight.value = `${collapsedPx}px`;
  }
}

function onTransitionEnd(e: TransitionEvent) {
  if (e.propertyName !== "max-height") return;
  if (!contentRef.value) return;
  if (isExpanded.value) {
    // Release the constraint post-expand so the element can reflow naturally
    // (e.g. late-loading images pushing the content taller).
    overrideMaxHeight.value = "none";
  } else {
    // Post-collapse: drop the inline override, let the declarative style
    // (maxHeight: <px>) take over.
    overrideMaxHeight.value = null;
  }
  notifyExpandableChanged();
}

// ───────────────────────────────────────────────────────────────────
// Global "expand all / collapse all" integration
// ───────────────────────────────────────────────────────────────────
// Register with the page-wide registry so the ScrollNav toggle button
// can expand/collapse every truncated block on the current route in
// one click. Only instances where truncation is enabled participate —
// non-truncatable wrappers don't register, preventing the ScrollNav
// toggle button from appearing on pages with no expandable content.
let unregisterExpand: (() => void) | null = null;

if (props.truncatable) {
  unregisterExpand = registerExpandable({
    id: Symbol("TruncatedContent"),
    isExpanded: () => !needsTruncation.value || isExpanded.value,
    expand: () => {
      if (needsTruncation.value && !isExpanded.value) {
        toggleExpand();
      }
    },
    collapse: () => {
      if (needsTruncation.value && isExpanded.value) {
        toggleExpand();
      }
    },
  });
}
onBeforeUnmount(() => unregisterExpand?.());
</script>

<template>
  <div
    class="truncated-content-wrapper"
    :class="{ 'is-collapsed': isCollapsed }"
  >
    <div
      :ref="setContentRef"
      class="truncated-content"
      :class="{ truncatable: needsTruncation }"
      :style="contentStyle"
      @transitionend="onTransitionEnd"
    >
      <slot />
    </div>
    <button
      v-if="isCollapsed"
      type="button"
      class="truncated-expand-button"
      :aria-expanded="isExpanded"
      @click="toggleExpand"
    >... <strong>показать полностью</strong></button>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.truncated-content-wrapper
  display: flex
  flex-direction: column
  min-width: 0

.truncated-content
  min-width: 0
  // Smooth max-height transition used by expand/collapse. ease-out-quart
  // (cubic-bezier(0.22, 1, 0.36, 1)) decelerates gently toward the end —
  // noticeably softer than Material's standard ease. Duration tuned so
  // the movement reads as deliberate, not jumpy, and matches the
  // testimonial bubble animation one-for-one.
  transition: max-height 0.55s cubic-bezier(0.22, 1, 0.36, 1)

  &.truncatable
    overflow: hidden

// Collapsed-state media shrinkage: cap img/video/iframe inside the slot
// to a fraction of the collapsed budget so over-sized assets (character
// portraits, maps, etc.) do not monopolize the view and never get clipped
// mid-image.
//
// Mechanism (no !important anywhere):
//   - BBCode parsers emit images without inline max-width/max-height.
//     Sizing for default images comes from the global `.bb-image` rule
//     reading `--bb-image-max-width` / `--bb-image-max-height` CSS vars.
//     Custom-size images wrap the <img> in a <span class="bb-image-frame">
//     that carries the vars via inline style.
//   - In the collapsed state we override the same CSS vars ON the <img>
//     element via a class selector. Class rule beats inherited value from
//     the wrapper span, and there is no inline style on the <img> itself
//     to fight. No !important required.
//   - Non-BBCode <img>/<video>/<iframe> inside the slot (rare — e.g. raw
//     embeds) get the same override through the generic element targets.
//
// :deep() is necessary because slot content is rendered in the parent's
// scope and wouldn't otherwise match scoped selectors — this is the
// documented, supported use of :deep(), not a hack.
//
// No mask/gradient fade — the explicit "показать полностью" button below
// is the truncation affordance, and a fade would degrade the readability
// of the last visible line. Modern hard-cut convention.
.truncated-content-wrapper.is-collapsed
  :deep(.bb-image),
  :deep(img),
  :deep(video),
  :deep(iframe)
    --bb-image-max-width: 100%
    --bb-image-max-height: var(--truncated-media-max, 90px)
    max-width: 100%
    max-height: var(--truncated-media-max, 90px)
    width: auto
    height: auto
    object-fit: contain

// Expand control:
//   - Real <button> for native focus/keyboard/AT semantics.
//   - Reset default button chrome so it visually reads as a link.
//   - Padding gives a 24px+ tap target (WCAG 2.5.5 Target Size).
//   - :focus-visible draws a clear keyboard outline; :hover handles mouse.
//   - align-self prevents stretching to the full wrapper width.
.truncated-expand-button
  align-self: flex-start
  display: inline-block
  margin-top: $tiny
  padding: $tiny 0
  border: none
  background: none
  font: inherit
  text-align: left
  color: $link
  // Matches link behaviour from Reset.sass: no underline in the resting
  // state, underline only on hover.
  text-decoration: none
  cursor: pointer
  user-select: none

  &:hover
    color: $link-hover
    text-decoration: underline

  &:focus-visible
    outline: 2px solid $link
    outline-offset: 2px
    border-radius: 2px
</style>
