/**
 * FLIP reorder animation composable
 * @module shared/lib/composables/useFlipReorder
 *
 * Shared FLIP (First-Last-Invert-Play) mechanics for "active-first" strips
 * (profile Tabs, forum BoardNavigation): when the owning component reorders
 * its keyed items, each moved element animates from its old bounding box to
 * the new one instead of teleporting.
 *
 * How it hooks in: registers `onBeforeUpdate` / `onUpdated` on the CALLING
 * component, so it must run inside that component's `setup()`. The map is
 * keyed by DOM element — Vue's keyed diff reuses the same node for the same
 * `:key` across renders (it just moves the node to a new sibling position),
 * which is exactly what makes the measure-before / measure-after correlation
 * work.
 *
 * Focus restoration: Vue's keyed diff physically moves the minimal set of
 * elements; if the FOCUSED element is among the moved ones, the browser
 * drops focus to <body> on detach. When `focusSelector` is provided and
 * focus was inside `root` before the patch, it is restored onto the first
 * element matching that selector after the patch.
 *
 * Animation: Web Animations API — deterministic, decoupled from the CSS
 * transition pipeline (no race with the cascade or with the items' own
 * color transitions). In-flight animations are tracked per element so a
 * rapid reorder cancels the stale one and starts fresh — no stuck
 * transforms, no animation pile-up.
 */
import { onBeforeUpdate, onUpdated, type Ref } from "vue";

export interface FlipReorderOptions {
  /** Container element holding the animated items. */
  root: Ref<HTMLElement | null>;
  /** Selector matching the items to FLIP-animate inside `root`. */
  itemSelector: string;
  /**
   * Selector of the element to refocus when the patch dropped focus out of
   * `root` (e.g. the active item). Omit to skip focus restoration.
   */
  focusSelector?: string;
  /** Animation duration in ms. */
  durationMs?: number;
  /** Animation easing. */
  easing?: string;
}

const DEFAULT_DURATION_MS = 280;
const DEFAULT_EASING = "cubic-bezier(0.4, 0, 0.2, 1)";

export function useFlipReorder(options: FlipReorderOptions): void {
  const duration = options.durationMs ?? DEFAULT_DURATION_MS;
  const easing = options.easing ?? DEFAULT_EASING;

  // `onBeforeUpdate` runs while the DOM is still in the OLD layout — rects
  // are snapshotted there. `onUpdated` runs after Vue commits the new
  // layout — measure again and animate the delta back to identity.
  let prevRects: Map<HTMLElement, DOMRect> | null = null;

  // Whether focus was inside the root before the patch (see module doc).
  let hadFocusInRoot = false;

  // Track in-flight animations so a rapid reorder cancels the stale one.
  const activeAnims = new WeakMap<HTMLElement, Animation>();

  onBeforeUpdate(() => {
    const root = options.root.value;
    if (!root) return;
    hadFocusInRoot = root.contains(document.activeElement);
    prevRects = new Map();
    for (const el of root.querySelectorAll<HTMLElement>(options.itemSelector)) {
      prevRects.set(el, el.getBoundingClientRect());
    }
  });

  onUpdated(() => {
    const root = options.root.value;

    // Focus restoration must not depend on whether any rects were captured.
    if (
      options.focusSelector &&
      hadFocusInRoot &&
      root &&
      !root.contains(document.activeElement)
    ) {
      root
        .querySelector<HTMLElement>(options.focusSelector)
        ?.focus({ preventScroll: true });
    }
    hadFocusInRoot = false;

    if (!prevRects || !root) return;
    for (const el of root.querySelectorAll<HTMLElement>(options.itemSelector)) {
      const oldRect = prevRects.get(el);
      if (!oldRect) continue;
      const newRect = el.getBoundingClientRect();
      const dx = oldRect.left - newRect.left;
      const dy = oldRect.top - newRect.top;
      if (dx === 0 && dy === 0) continue;

      // Cancel any animation still running on this element from a previous
      // rapid reorder. Otherwise we'd stack interpolations.
      activeAnims.get(el)?.cancel();

      // The element is at its NEW logical position after Vue's patch; play
      // it FROM the inverted offset TO identity.
      const anim = el.animate(
        [
          { transform: `translate(${dx}px, ${dy}px)` },
          { transform: "translate(0, 0)" },
        ],
        { duration, easing, fill: "none" },
      );
      activeAnims.set(el, anim);
      anim.finished
        .then(() => {
          if (activeAnims.get(el) === anim) activeAnims.delete(el);
        })
        .catch(() => {
          /* cancelled — already replaced by a fresh animation */
        });
    }
    prevRects = null;
  });
}
