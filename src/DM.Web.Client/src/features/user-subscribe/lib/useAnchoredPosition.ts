import { nextTick, onBeforeUnmount, onMounted, ref, type Ref } from "vue";
import { POPOVER_GAP, VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";

/**
 * Where a panel that drops from an anchor stands, as fixed coordinates.
 *
 * Fixed and not absolute, because the anchors this serves stand in boxes that
 * cut what sticks out of them: the row of actions of a profile scrolls sideways
 * on a phone, and a panel positioned inside it was clipped to the row's height.
 * The arithmetic is the date field's (DateInput): under the anchor with the gap
 * every dropdown keeps, above it when there is no room below, and pushed back
 * inside the window at either side, so on a phone the panel stands against
 * the right edge instead of running off it. Re-read on every scroll and resize
 * while the panel is up, and whenever the panel changes its size.
 */
export function useAnchoredPosition(
  anchor: () => HTMLElement | null,
  panel: Ref<HTMLElement | null>,
) {
  const style = ref<Record<string, string>>({});

  function update() {
    const from = anchor();
    const el = panel.value;
    if (!from || !el) return;
    const rect = from.getBoundingClientRect();
    const width = el.offsetWidth;
    const height = el.offsetHeight;
    const vw = document.documentElement.clientWidth;
    const vh = document.documentElement.clientHeight;

    let top = rect.bottom + POPOVER_GAP;
    if (top + height > vh - VIEWPORT_EDGE) {
      const above = rect.top - POPOVER_GAP - height;
      top =
        above >= VIEWPORT_EDGE
          ? above
          : Math.max(VIEWPORT_EDGE, vh - VIEWPORT_EDGE - height);
    }

    let left = rect.left;
    if (left + width > vw - VIEWPORT_EDGE) left = vw - VIEWPORT_EDGE - width;
    if (left < VIEWPORT_EDGE) left = VIEWPORT_EDGE;

    style.value = { top: `${top}px`, left: `${left}px` };
  }

  let resizeObserver: ResizeObserver | null = null;

  onMounted(() => {
    update();
    nextTick(update);
    // Passive: the listener only reads geometry, and a blocking one on the
    // scroll path makes the browser wait for it before every frame.
    document.addEventListener("scroll", update, {
      passive: true,
      capture: true,
    });
    window.addEventListener("resize", update);
    if (typeof ResizeObserver !== "undefined" && panel.value) {
      resizeObserver = new ResizeObserver(() => update());
      resizeObserver.observe(panel.value);
    }
  });

  onBeforeUnmount(() => {
    document.removeEventListener("scroll", update, { capture: true });
    window.removeEventListener("resize", update);
    resizeObserver?.disconnect();
  });

  return { style, update };
}
