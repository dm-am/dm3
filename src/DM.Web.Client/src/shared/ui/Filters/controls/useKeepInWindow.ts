import { onBeforeUnmount, watch, type Ref } from "vue";

/**
 * How far a panel stands from the edge of the window at the least: the side
 * margin of the page on a phone ($medium), so a moved panel lines up with the
 * column it was opened from.
 */
export const WINDOW_EDGE = 16;

/**
 * Keeps a panel that drops from a button of a filter bar inside the window.
 *
 * A panel hangs from its button's right edge (=dropdown-panel), which holds
 * as long as the button stands at the right of the bar. On a phone it did
 * not: the "Фильтры" of the games list stood at the left edge and its panel
 * ran 114px off the left of the screen, and a bar that hangs its panel from
 * the left instead would run it off the right. The panel is left where its
 * stylesheet puts it and then moved sideways, by the `translate` property so
 * that a transform of its own is not overwritten, as far as it takes to stand
 * clear of both edges. It is measured again whenever its size changes (a level
 * of the panel is wider than the one before) and when the window does.
 */
export function useKeepInWindow(panel: Ref<HTMLElement | null>) {
  function place() {
    const el = panel.value;
    if (!el) return;
    el.style.translate = "";
    const box = el.getBoundingClientRect();
    const room = document.documentElement.clientWidth;
    let shift = 0;
    if (box.right > room - WINDOW_EDGE) shift = room - WINDOW_EDGE - box.right;
    // A panel wider than the room stands from the left edge: its start, with
    // the heading and the way back, is the part that must be seen.
    if (box.left + shift < WINDOW_EDGE) shift = WINDOW_EDGE - box.left;
    if (shift) el.style.translate = `${shift}px 0`;
  }

  let observer: ResizeObserver | null = null;

  watch(
    panel,
    (el) => {
      observer?.disconnect();
      observer = null;
      if (!el) return;
      place();
      if (typeof ResizeObserver !== "undefined") {
        observer = new ResizeObserver(() => place());
        observer.observe(el);
      }
    },
    { immediate: true, flush: "post" },
  );

  window.addEventListener("resize", place);

  onBeforeUnmount(() => {
    observer?.disconnect();
    window.removeEventListener("resize", place);
  });

  return { place };
}
