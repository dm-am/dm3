import { nextTick, ref, type Ref } from "vue";

/**
 * Smooth height animation for a container whose CONTENT is swapped by a
 * state flip (e.g. the digest boards teaser <-> full grid). Pin-and-animate:
 * pin the current rendered height, flip the state, measure the new natural
 * height, transition between the two concrete values, release the
 * constraint when the transition ends.
 *
 * The pin is the REAL `height`, not max-height: after the flip the old
 * height must genuinely hold the box in BOTH directions. A max-height pin
 * only constrains when the new content is taller (expand) — on collapse
 * the box would snap down instantly and the "animation" would run over an
 * already-shrunk element. (TruncatedContent animates max-height because its
 * content never changes; here the content swaps.)
 *
 * The caller owns the CSS: bind `pinnedHeight` as an inline height,
 * `transition: height $expand-duration $expand-easing` on the element (the
 * unified site tempo — the global `.expand-zone` class from Reset.sass) and
 * `overflow: hidden` while `pinnedHeight` is non-null; wire
 * `onTransitionEnd` to both @transitionend and @transitioncancel.
 */
export function useAnimatedHeightToggle(
  el: Ref<HTMLElement | null>,
  setState: (next: boolean) => void,
) {
  /** Inline height pin; null when no animation is in flight. */
  const pinnedHeight = ref<string | null>(null);

  let releaseTimer: number | undefined;

  /** Drop the pin so the content can reflow freely afterwards. */
  function release() {
    window.clearTimeout(releaseTimer);
    pinnedHeight.value = null;
  }

  /** Longest transition duration on the element, ms; 0 when transitions
   * are disabled (prefers-reduced-motion) or unavailable (jsdom). */
  function transitionDurationMs(node: HTMLElement): number {
    const raw = getComputedStyle(node).transitionDuration || "0s";
    const longest = Math.max(...raw.split(",").map((d) => parseFloat(d) || 0));
    return longest * 1000;
  }

  async function toggle(next: boolean) {
    const node = el.value;
    if (!node) {
      setState(next);
      return;
    }

    window.clearTimeout(releaseTimer);

    // Pin BEFORE the flip so the patch that swaps the content already
    // carries the old height as the constrained start state. A zone that is
    // currently empty (closed tool form) legitimately pins at 0px.
    pinnedHeight.value = `${node.offsetHeight}px`;
    setState(next);
    await nextTick();

    // Commit the pinned start state to the rendering pipeline so it is the
    // "before-change" value of the upcoming transition, then animate to the
    // new content's natural height.
    void node.offsetHeight;
    pinnedHeight.value = `${node.scrollHeight}px`;

    // Safety net: release even when no transition event ever arrives —
    // zero-duration transitions (reduced motion, jsdom) fire nothing, and
    // a stuck pin would clip later natural reflows of the content.
    releaseTimer = window.setTimeout(release, transitionDurationMs(node) + 150);
  }

  /** Wire to BOTH @transitionend and @transitioncancel: an interrupted
   * transition (mid-flight reverse toggle, element hidden) never fires
   * transitionend. */
  function onTransitionEnd(e: TransitionEvent) {
    if (e.propertyName !== "height") return;
    release();
  }

  return { pinnedHeight, toggle, onTransitionEnd };
}
