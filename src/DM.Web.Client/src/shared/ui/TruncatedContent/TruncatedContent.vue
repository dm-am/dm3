<script setup lang="ts">
// `maxHeight` is a budget: the collapsed cut is snapped to the bottom of the last real line box that fits,
// so it never slices a line, and no fade is drawn over it. Expanding animates max-height between concrete px.
import { computed, nextTick, onBeforeUnmount, ref, toRef, watch } from "vue";
import { useContentTruncation } from "@/shared/lib/composables/useContentTruncation";
import {
  registerExpandable,
  notifyExpandableChanged,
  refreshExpandableStates,
} from "@/shared/lib/composables/useExpandableRegistry";

const props = withDefaults(
  defineProps<{
    /** The budget in px; the applied clamp is snapped down to a whole line. */
    maxHeight?: number;
    /** Enable truncation. When false, content always renders fully. */
    truncatable?: boolean;
    /** Watched: a change re-trims the content and re-checks the overflow. */
    watchKey?: unknown;
    /** Optional hook invoked after the content element is mounted/updated. */
    onContentMounted?: (el: HTMLElement) => void;
  }>(),
  {
    maxHeight: 150,
    truncatable: false,
    watchKey: undefined,
    onContentMounted: undefined,
  },
);

/** The share of the budget media may take in the collapsed view, leaving room for the text. */
const MEDIA_SHARE = 0.6;

// Measured from the real line boxes, never a uniform grid: margins and mixed line-heights put lines off it,
// and the cut landed mid-line.

interface LineSnap {
  /** Bottom of the last text line fully within the budget, px from the
   * content-box top — the exact clamp height (a real line boundary). */
  clamp: number;
  /** Bottom of the first text line that overflows the budget, or Infinity
   * when the whole content fits — drives the "needs truncation" decision. */
  firstOverflowBottom: number;
}

// null without measurable text (media alone, or no layout in a test): the raw budget applies.
const lineSnap = ref<LineSnap | null>(null);

const snappedMaxHeight = computed(() => {
  const snap = lineSnap.value;
  return snap ? snap.clamp : props.maxHeight;
});

// Halfway between the clamp and the first overflowing line: a sub-line remainder renders, not a button.
const truncationThreshold = computed(() => {
  const snap = lineSnap.value;
  if (!snap) return props.maxHeight;
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
  enabled: toRef(props, "truncatable"),
  watchContent: toRef(props, "watchKey"),
  onContentMounted: handleContentMounted,
});

/** The lowest line-box bottom within the budget and the first beyond it; null without measurable text. */
function measureLineSnap(): void {
  const el = contentRef.value;
  if (!el) {
    lineSnap.value = null;
    return;
  }
  const budget = props.maxHeight;
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
      // No range measurement in jsdom: the raw budget applies.
      lineSnap.value = null;
      return;
    }
    for (const rect of rects) {
      if (rect.height <= 0) continue;
      const bottom = rect.bottom - elTop;
      if (bottom <= budget + 0.5) clamp = Math.max(clamp, bottom);
      else firstOverflowBottom = Math.min(firstOverflowBottom, bottom);
    }
  }
  // No line fit within the budget: the raw budget, not zero.
  lineSnap.value = clamp > 0 ? { clamp, firstOverflowBottom } : null;
}

/** Measures first, then re-checks: the composable's own first check ran before this callback. */
function handleContentMounted(el: HTMLElement): void {
  measureLineSnap();
  checkOverflow();
  props.onContentMounted?.(el);
  // A web font swapped in after mount reflows the lines.
  document.fonts?.ready.then(() => {
    measureLineSnap();
    checkOverflow();
  });
}

// The content or the budget changed (GamePost resizes its budget by a ResizeObserver).
watch([() => props.watchKey, () => props.maxHeight], () => {
  nextTick(() => {
    measureLineSnap();
    checkOverflow();
  });
});

const isCollapsed = computed(() => needsTruncation.value && !isExpanded.value);

// Written only by toggleExpand and onTransitionEnd: a watch resetting it would overwrite the animation's
// target before Vue applies it. null leaves it to contentStyle, "none" releases it after an expand.
const overrideMaxHeight = ref<string | null>(null);

// A pure function of state: measurements enter only through lineSnap.
const contentStyle = computed(() => {
  const max = snappedMaxHeight.value;
  const mediaMax = Math.floor(max * MEDIA_SHARE);

  if (overrideMaxHeight.value !== null) {
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

// CSS cannot animate a px max-height to none, and Vue coalesces two writes in one tick: the start is
// pinned, committed with nextTick and a reflow, and only then the target written.
async function toggleExpand() {
  const el = contentRef.value;
  if (!el || !needsTruncation.value) {
    toggleExpandRaw();
    return;
  }

  // The snapped clamp, as the collapsed style applies it.
  const collapsedPx = snappedMaxHeight.value;

  if (!isExpanded.value) {
    overrideMaxHeight.value = `${collapsedPx}px`;
    await nextTick();

    // The committed start of the transition.
    void el.offsetHeight;

    toggleExpandRaw();
    overrideMaxHeight.value = `${el.scrollHeight}px`;
  } else {
    overrideMaxHeight.value = `${el.scrollHeight}px`;
    await nextTick();

    void el.offsetHeight;

    toggleExpandRaw();
    overrideMaxHeight.value = `${collapsedPx}px`;
  }
}

/** A press of the reader's own clears the registry's pending bulk action; a bulk one must not. */
function manualToggle() {
  notifyExpandableChanged();
  toggleExpand();
}

function onTransitionEnd(e: TransitionEvent) {
  if (e.propertyName !== "max-height") return;
  if (!contentRef.value) return;
  if (isExpanded.value) {
    // Released, so late images can push the content taller.
    overrideMaxHeight.value = "none";
  } else {
    overrideMaxHeight.value = null;
  }
  // Fires for bulk animations too: refresh the aggregate state, never clear the pending action.
  refreshExpandableStates();
}

// Registered only while something needs truncation, or "Свернуть все" would show with nothing to collapse.
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
@use "@/assets/styles/Links" as *
@use "@/assets/styles/Animations" as *

.truncated-content-wrapper
  display: flex
  flex-direction: column
  min-width: 0

.truncated-content
  min-width: 0
  // The site's one reveal tempo.
  transition: max-height $expand-duration $expand-easing

  &.truncatable
    overflow: hidden

// Media shrink in the collapsed view: the class rule sets the image variables on the <img> itself, over the
// wrapper's, and max-width itself, which an attachment's inline variable does not reach. :deep for slot content.
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

// The shared "... показать полностью" idiom (_Links.sass).
.truncated-expand-button
  +expand-toggle-button
</style>
