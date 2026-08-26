<script setup lang="ts">
/**
 * Unified "показать полностью" truncation primitive.
 *
 * Wraps the shared useContentTruncation composable with a consistent
 * template + style for every caller. Handles:
 *
 *   - overflow detection against a (reactive) max-height budget
 *   - LINE-SNAPPED clamp: the applied collapsed max-height is derived from
 *     the MEASURED line-height of the slotted content, never the raw px
 *     budget (see "Line-snapped clamp" below)
 *   - symmetric whitespace trimming (leading AND trailing) via the composable
 *     so author-inserted blank lines never eat into the truncation budget
 *   - collapsed-state media shrinkage (img/video/iframe capped to a fraction
 *     of maxHeight via CSS var + :deep selector) so over-sized character
 *     portraits / maps never blow the budget or get clipped mid-image
 *   - "показать полностью" button rendered only when truncated & collapsed
 *   - SMOOTH max-height transition on expand/collapse (see below)
 *
 * Visual contract: the collapsed view is a clean hard cut BETWEEN text
 * lines. `maxHeight` is a BUDGET, not the literal clamp — the applied
 * collapsed height is snapped down to a whole number of content lines so
 * the cut never slices a line in half. No gradient/mask fade — it would
 * degrade the last visible line and duplicate the explicit "показать
 * полностью" affordance below. This matches the modern hard-cut convention
 * used by Twitter, Reddit, Facebook, GitHub, LinkedIn, etc.
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
import { computed, nextTick, onBeforeUnmount, ref, watch } from "vue";
import { useContentTruncation } from "@/shared/lib/composables/useContentTruncation";
import {
  registerExpandable,
  notifyExpandableChanged,
  refreshExpandableStates,
} from "@/shared/lib/composables/useExpandableRegistry";

const props = withDefaults(
  defineProps<{
    /** Max collapsed height BUDGET in px. The applied clamp is snapped
     * down to a whole number of measured content lines (never applied
     * raw). Reactive through the parent render cycle. */
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

// ───────────────────────────────────────────────────────────────────
// Line-box snapped clamp
// ───────────────────────────────────────────────────────────────────
// INVARIANT (do not regress): the collapsed hard cut must NEVER slice a
// text line in half. `maxHeight` is only a budget; the applied clamp is
// snapped DOWN to the bottom of the last text line that fits ENTIRELY
// within the budget, taken from the REAL rendered line boxes of the
// slotted content — never assumed from a uniform grid.
//
// History: earlier versions modelled the content as one uniform line grid
// (offsetTop + n * lineHeight). That silently failed for multi-block bbcode:
// inter-paragraph margins and mixed line-heights shift the real line
// positions off the assumed grid, so the cut landed mid-line and a sliver of
// the next line peeked (e.g. a 150px budget snapped to 140 while the real
// line ran 130-150 -> 10px peek). Measuring the actual line-box bottoms is
// content-agnostic: margins, lists, mixed line-heights all just work. Never
// reintroduce a uniform-grid / hard-coded line-height assumption here.

interface LineSnap {
  /** Bottom of the last text line fully within the budget, px from the
   * content-box top — the exact clamp height (a real line boundary). */
  clamp: number;
  /** Bottom of the first text line that overflows the budget, or Infinity
   * when the whole content fits — drives the "needs truncation" decision. */
  firstOverflowBottom: number;
}

// Measured line grid of the slotted content. null until measured, or when
// there is no measurable text (media-only content, or a layout-less test
// environment) — the raw budget is used as-is in that case.
const lineSnap = ref<LineSnap | null>(null);

// Applied collapsed clamp: the last real line boundary within the budget.
const snappedMaxHeight = computed(() => {
  const snap = lineSnap.value;
  return snap ? snap.clamp : maxHeightRef.value;
});

// Overflow threshold for "needs truncation". Truncate only when a real text
// line lies beyond the clamp: a sub-line remainder (trailing margin/padding,
// rounding) renders fully rather than hiding half a line behind the button.
// The threshold sits halfway between the clamp and the first overflowing
// line's bottom, so the composable's `scrollHeight > threshold` check fires
// for a genuine next line but not for trailing whitespace; MAX_SAFE_INTEGER
// disables truncation when nothing overflows.
const truncationThreshold = computed(() => {
  const snap = lineSnap.value;
  if (!snap) return maxHeightRef.value;
  if (!Number.isFinite(snap.firstOverflowBottom))
    return Number.MAX_SAFE_INTEGER;
  return (snap.clamp + snap.firstOverflowBottom) / 2;
});

const {
  setContentRef,
  contentRef,
  needsTruncation,
  isExpanded,
  toggleExpand: toggleExpandRaw,
  checkOverflow,
} = useContentTruncation({
  maxHeight: truncationThreshold,
  enabled: enabledRef,
  watchContent: watchContentRef,
  onContentMounted: handleContentMounted,
});

/** Measure the line grid from the REAL rendered line boxes: walk every
 * non-empty text node, take each line box's bottom relative to the content
 * box, and record (a) the lowest bottom still within the budget — the clamp,
 * and (b) the first bottom beyond the budget — the overflow mark. Content-
 * agnostic: inter-block margins, lists and mixed line-heights all follow
 * their real geometry, so the cut always lands on a real line boundary.
 * Sets lineSnap to null when there is no measurable text (media-only content,
 * or a layout-less environment like jsdom). */
function measureLineSnap(): void {
  const el = contentRef.value;
  if (!el) {
    lineSnap.value = null;
    return;
  }
  const budget = maxHeightRef.value;
  const elTop = el.getBoundingClientRect().top;
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
  let clamp = 0;
  let firstOverflowBottom = Infinity;
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (!node.textContent?.trim()) continue;
    const range = document.createRange();
    range.selectNodeContents(node);
    let rects: DOMRectList;
    try {
      rects = range.getClientRects();
    } catch {
      // Range measurement unavailable (jsdom) — no snap, raw budget applies.
      lineSnap.value = null;
      return;
    }
    for (const rect of rects) {
      if (rect.height <= 0) continue; // display:none spoiler content etc.
      const bottom = rect.bottom - elTop;
      if (bottom <= budget + 0.5) {
        if (bottom > clamp) clamp = bottom;
      } else if (bottom < firstOverflowBottom) {
        firstOverflowBottom = bottom;
      }
    }
  }
  // No line fit within the budget (or no measurable text): fall back to the
  // raw budget rather than clamping to zero.
  lineSnap.value = clamp > 0 ? { clamp, firstOverflowBottom } : null;
}

/** Content-mounted hook: measure the line grid FIRST, then re-run the
 * overflow check — the composable's own initial check fires before this
 * callback, i.e. against the pre-measurement threshold. */
function handleContentMounted(el: HTMLElement): void {
  measureLineSnap();
  checkOverflow();
  props.onContentMounted?.(el);
  // Web fonts can swap in after mount and reflow the line grid; re-measure
  // once they settle so the clamp is never left on the fallback font's
  // metrics. measureLineSnap re-guards contentRef, so a post-unmount
  // resolution is a no-op.
  document.fonts?.ready.then(() => {
    measureLineSnap();
    checkOverflow();
  });
}

// Re-measure when the content or the budget changes (e.g. GamePost resizes
// its dynamic budget via a ResizeObserver on the meta column). Runs after
// the composable's own watchContent re-check, refreshing the grid and
// re-validating overflow against the up-to-date threshold.
watch([watchContentRef, maxHeightRef], () => {
  nextTick(() => {
    measureLineSnap();
    checkOverflow();
  });
});

// True while the user sees the truncated (collapsed) version.
const isCollapsed = computed(() => needsTruncation.value && !isExpanded.value);

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

// Content style: collapsed gets the LINE-SNAPPED max-height + CSS
// variables that drive media shrinkage; expanded releases the constraint
// entirely so images return to their natural size. Pure function of state
// — DOM measurements enter only via the lineSnap ref, which is written
// exclusively in lifecycle/watch handlers.
const contentStyle = computed(() => {
  const max = snappedMaxHeight.value;
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

  // The animation endpoints use the SNAPPED clamp — the same value the
  // collapsed declarative style applies — so expand starts from (and
  // collapse returns to) a whole-line cut, never the raw budget.
  const collapsedPx = snappedMaxHeight.value;

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

/** Manual user toggle (the "показать полностью" button): clears the
 * registry's pending bulk action. Registry-driven bulk expands/collapses
 * call toggleExpand directly and must NOT clear it — late-registering
 * blocks still need to sync with the bulk action. */
function manualToggle() {
  notifyExpandableChanged();
  toggleExpand();
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
  // Settle event: fires for bulk-driven animations too, so only refresh the
  // aggregate state — never clear the pending bulk action here.
  refreshExpandableStates();
}

// ───────────────────────────────────────────────────────────────────
// Global "expand all / collapse all" integration
// ───────────────────────────────────────────────────────────────────
// Register with the page-wide registry so the ScrollNav toggle button
// can expand/collapse every truncated block on the current route in one
// click. Register ONLY while the content actually needs truncation (has
// something to collapse) — registering a truncatable-but-short block would
// make the ScrollNav "Свернуть все" button appear on pages with nothing to
// collapse. `needsTruncation` is measured async, so a watch syncs it.
let unregisterExpand: (() => void) | null = null;

function syncRegistration() {
  const collapsible = props.truncatable && needsTruncation.value;
  if (collapsible && !unregisterExpand) {
    unregisterExpand = registerExpandable({
      id: Symbol("TruncatedContent"),
      isExpanded: () => isExpanded.value,
      expand: () => {
        if (!isExpanded.value) toggleExpand();
      },
      collapse: () => {
        if (isExpanded.value) toggleExpand();
      },
    });
  } else if (!collapsible && unregisterExpand) {
    unregisterExpand();
    unregisterExpand = null;
  }
}

watch(needsTruncation, syncRegistration, { immediate: true });
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
      @click="manualToggle"
    >
      ... <strong>показать полностью</strong>
    </button>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Animations" as *

.truncated-content-wrapper
  display: flex
  flex-direction: column
  min-width: 0

.truncated-content
  min-width: 0
  // Smooth max-height transition used by expand/collapse. The unified
  // $expand-duration/$expand-easing tokens (ease-out-quart) decelerate
  // gently toward the end — noticeably softer than Material's standard
  // ease. Shared with Testimonial, ExpandableList and the BBCode
  // spoiler/NSFW blocks so every reveal on the site moves in one tempo.
  transition: max-height $expand-duration $expand-easing

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

// Expand control: the shared "... показать полностью" idiom (SSOT mixin in
// Inputs.sass — link look, 24px+ tap target per WCAG 2.5.5, keyboard focus
// ring). Real <button> semantics live in the template above.
.truncated-expand-button
  +expand-toggle-button
</style>
