/**
 * useListWidth: how wide the list of conversations stands beside the
 * correspondence, and its narrow state, where it keeps the pictures and the
 * counts of unread alone.
 *
 * The list is as wide as the right column of the site at the most (its place
 * on the page), and the reader narrows it by dragging the edge between it and
 * the correspondence. Below the least width of a list with words in it, the
 * drag gives the narrow state instead; dragging back past the middle of the
 * two brings the words back. The width is the reader's own and outlives the
 * tab (browser storage, wrapped: a private window refuses it, and the list
 * then simply starts at its full width).
 */
import { computed, ref } from "vue";

/** The full width: the right column's 330 less its gutter. */
export const LIST_FULL_WIDTH = 298;
/** The least width a list with names and excerpts still reads at. */
export const LIST_LEAST_WIDTH = 200;
/** The narrow state: a 48 picture with 8 on either side. */
export const LIST_NARROW_WIDTH = 64;
/** Where a drag decides between the narrow state and a list with words. */
export const LIST_SNAP_WIDTH = (LIST_NARROW_WIDTH + LIST_LEAST_WIDTH) / 2;
/** One press of an arrow on the edge. */
export const LIST_KEY_STEP = 16;

export const LIST_WIDTH_KEY = "dm3-messenger-list-width";

/** A width the list may stand at: the narrow state, or a list with words. */
export function settleWidth(width: number): number {
  if (!Number.isFinite(width)) return LIST_FULL_WIDTH;
  if (width < LIST_SNAP_WIDTH) return LIST_NARROW_WIDTH;
  return Math.min(LIST_FULL_WIDTH, Math.max(LIST_LEAST_WIDTH, width));
}

function readStored(): number {
  try {
    const raw = localStorage.getItem(LIST_WIDTH_KEY);
    return raw === null ? LIST_FULL_WIDTH : settleWidth(Number(raw));
  } catch {
    return LIST_FULL_WIDTH;
  }
}

function store(width: number) {
  try {
    localStorage.setItem(LIST_WIDTH_KEY, String(width));
  } catch {
    // The width lasts for this page only.
  }
}

export function useListWidth() {
  const width = ref(readStored());
  const narrow = computed(() => width.value === LIST_NARROW_WIDTH);
  /** The last width with words, the one the list widens back to. */
  let wordsWidth = narrow.value ? LIST_FULL_WIDTH : width.value;

  function set(next: number) {
    const settled = settleWidth(next);
    if (settled === width.value) return;
    width.value = settled;
    if (settled !== LIST_NARROW_WIDTH) wordsWidth = settled;
    store(settled);
  }

  /** Back to the words, at the width the list last had them. */
  function widen() {
    set(wordsWidth);
  }

  // ─────────────────────────────────────────────────────────────
  // The drag of the edge. The list stands right of the edge, so moving the
  // pointer left widens it.
  // ─────────────────────────────────────────────────────────────
  const dragging = ref(false);
  let startX = 0;
  let startWidth = 0;

  function onPointerDown(event: PointerEvent) {
    if (event.button !== 0) return;
    event.preventDefault();
    dragging.value = true;
    startX = event.clientX;
    startWidth = width.value;
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
  }

  function onPointerMove(event: PointerEvent) {
    if (!dragging.value) return;
    set(startWidth - (event.clientX - startX));
  }

  function onPointerUp(event: PointerEvent) {
    if (!dragging.value) return;
    dragging.value = false;
    (event.currentTarget as HTMLElement).releasePointerCapture?.(
      event.pointerId,
    );
  }

  /**
   * The edge from the keyboard: the left arrow widens the list, the right one
   * narrows it, a step at a time; past the least width it goes narrow, and
   * from narrow the left arrow brings the words back at their least width.
   */
  function onKeydown(event: KeyboardEvent) {
    if (event.key === "ArrowLeft") {
      event.preventDefault();
      set(narrow.value ? LIST_LEAST_WIDTH : width.value + LIST_KEY_STEP);
    } else if (event.key === "ArrowRight") {
      event.preventDefault();
      const next = width.value - LIST_KEY_STEP;
      set(next < LIST_LEAST_WIDTH ? LIST_NARROW_WIDTH : next);
    }
  }

  return {
    width,
    narrow,
    dragging,
    widen,
    onPointerDown,
    onPointerMove,
    onPointerUp,
    onKeydown,
  };
}
