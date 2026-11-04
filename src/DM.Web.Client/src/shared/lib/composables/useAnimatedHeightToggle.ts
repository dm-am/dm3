import { nextTick, onScopeDispose, ref, type Ref } from "vue";

/**
 * Height animation for a container whose content is swapped by a flip: pin the
 * real height (max-height would snap on collapse), flip, measure, transition,
 * release. The caller binds `pinnedHeight` inline with the .expand-zone
 * transition and wires `onTransitionEnd` to transitionend and transitioncancel.
 * Called in setup: nothing it starts outlives the component.
 */
export function useAnimatedHeightToggle(
  el: Ref<HTMLElement | null>,
  setState: (next: boolean) => void,
) {
  /** Inline height pin; null when no animation is in flight. */
  const pinnedHeight = ref<string | null>(null);

  let releaseTimer: ReturnType<typeof setTimeout> | undefined;
  /** The component is gone: a flip in flight stops where it stands. */
  let gone = false;

  /** Drop the pin so the content can reflow freely afterwards. */
  function release() {
    clearTimeout(releaseTimer);
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

    clearTimeout(releaseTimer);

    // Pinned before the flip, so the patch starts from the old height.
    pinnedHeight.value = `${node.offsetHeight}px`;
    setState(next);
    await nextTick();
    if (gone) return;

    // Committed as the start of the transition, then to the natural height.
    void node.offsetHeight;
    pinnedHeight.value = `${node.scrollHeight}px`;

    // Released even when no transition event arrives (reduced motion, jsdom).
    releaseTimer = setTimeout(release, transitionDurationMs(node) + 150);
  }

  /** Both events: an interrupted transition never fires transitionend. */
  function onTransitionEnd(e: TransitionEvent) {
    if (e.propertyName !== "height") return;
    release();
  }

  // A release left pending would fire after the page is gone, the test
  // environment's included, whose globals are torn down by then.
  onScopeDispose(() => {
    gone = true;
    clearTimeout(releaseTimer);
  });

  return { pinnedHeight, toggle, onTransitionEnd };
}
