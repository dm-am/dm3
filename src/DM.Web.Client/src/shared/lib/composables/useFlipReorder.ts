/**
 * FLIP animation of an "active-first" strip (profile Tabs, BoardNavigation):
 * a moved keyed element animates from its old box. Registers its hooks on the
 * calling component, so it runs in setup; elements are keyed by DOM node,
 * which the keyed diff reuses. Focus a move dropped is restored onto
 * `focusSelector`. Web Animations, with a stale flight cancelled per element.
 */
import { onBeforeUpdate, onUpdated, type Ref } from "vue";
import { prefersReducedMotion } from "@/shared/lib/utils/motion";

export interface FlipReorderOptions {
  /** Container element holding the animated items. */
  root: Ref<HTMLElement | null>;
  /** Selector matching the items to FLIP-animate inside `root`. */
  itemSelector: string;
  /** Refocused when the patch dropped focus out of root; omit to skip. */
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

  // Rects before the patch in onBeforeUpdate, after it in onUpdated.
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

    // Reduced motion: the Web Animations API is outside the CSS rule of
    // Reset.sass, so it asks; focus restoration runs either way.
    if (prefersReducedMotion()) {
      prevRects = null;
      return;
    }

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
