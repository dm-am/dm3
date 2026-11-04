import { ref, type Ref } from "vue";

export interface PinnedDragOptions {
  /** Whether the block can be reordered now; a press does nothing otherwise. */
  enabled: Ref<boolean>;
  /** Takes the topic at `from` out and stands it at `to`. */
  move: (from: number, to: number) => boolean;
}

/** How far a pressed pin travels before the press counts as a drag. */
const DRAG_THRESHOLD = 4;

/** A press on a pin, kept from the press to its release. */
interface Press {
  index: number;
  pointerId: number;
  startY: number;
  rows: () => HTMLElement[];
  /** The press travelled far enough to hold its topic. */
  dragged: boolean;
  /** Escape let the held topic go; the press still waits for its release. */
  cancelled: boolean;
}

/** What the handlers of a drag share between a press and its release. */
function dragState() {
  return {
    /** The pinned topic held by a drag, as its place in the block. */
    held: ref<number | null>(null),
    /** Where a release would stand the held topic, as a gap between rows; null leaves the order. */
    target: ref<number | null>(null),
    pressed: null as Press | null,
    /**
     * Set when a drag ends: the browser follows the release with a click on the
     * pin, and that click would unpin the topic. Dropped by the next press and at
     * the end of the release's task.
     */
    dragClick: false,
  };
}

type Drag = ReturnType<typeof dragState>;

/**
 * The drag of a pinned topic by its pin, on pointer events: the row stays in
 * place while held, a line shows where it will stand. A release moves it.
 */
export function usePinnedDrag({ enabled, move }: PinnedDragOptions) {
  const drag = dragState();
  const { held, target } = drag;

  function onPointerDown(
    index: number,
    event: PointerEvent,
    rows: () => HTMLElement[],
  ) {
    if (!enabled.value || event.button !== 0) return;
    // No text selection starts under a drag.
    event.preventDefault();
    drag.dragClick = false;
    drag.pressed = {
      index,
      pointerId: event.pointerId,
      startY: event.clientY,
      rows,
      dragged: false,
      cancelled: false,
    };
    const handle = event.currentTarget;
    if (handle instanceof Element) handle.setPointerCapture?.(event.pointerId);
  }

  function onPointerUp(event: PointerEvent) {
    const { pressed } = drag;
    if (!pressed || event.pointerId !== pressed.pointerId) return;
    const from = held.value;
    const gap = target.value;
    // A press that became a drag ends in a click all the same; one that did not travel is the toggle.
    const dragged = pressed.dragged;
    endDrag(drag, event);
    if (dragged) expectDragClick(drag);
    if (from === null || gap === null) return;
    // A gap below the held row counts that row too.
    move(from, gap > from ? gap - 1 : gap);
  }

  return {
    held,
    target,
    onPointerDown,
    onPointerMove: (event: PointerEvent) => onPointerMove(drag, event),
    onPointerUp,
    onPointerCancel: (event: PointerEvent) => onPointerCancel(drag, event),
    cancelDrag: () => cancelDrag(drag),
    isDragClick: () => isDragClick(drag),
  };
}

function expectDragClick(drag: Drag) {
  drag.dragClick = true;
  setTimeout(() => {
    drag.dragClick = false;
  });
}

/**
 * Whether a click on a pin is the one that follows the release of a drag,
 * and not a press. Answers true once per drag.
 */
function isDragClick(drag: Drag): boolean {
  const was = drag.dragClick;
  drag.dragClick = false;
  return was;
}

/** The gap the pointer is nearest to, counted in rows of the block. */
function gapAt(y: number, rows: HTMLElement[]): number {
  return rows.filter((row) => {
    const box = row.getBoundingClientRect();
    return y > box.top + box.height / 2;
  }).length;
}

function onPointerMove({ pressed, held, target }: Drag, event: PointerEvent) {
  if (!pressed || event.pointerId !== pressed.pointerId) return;
  if (pressed.cancelled) return;
  if (held.value === null) {
    if (Math.abs(event.clientY - pressed.startY) < DRAG_THRESHOLD) return;
    held.value = pressed.index;
    pressed.dragged = true;
  }
  const gap = gapAt(event.clientY, pressed.rows());
  // The gaps on either side of the held row leave it where it is.
  target.value = gap === held.value || gap === held.value + 1 ? null : gap;
}

function endDrag(drag: Drag, event: PointerEvent) {
  const handle = event.currentTarget;
  if (handle instanceof Element && drag.pressed) {
    if (handle.hasPointerCapture?.(drag.pressed.pointerId)) {
      handle.releasePointerCapture(drag.pressed.pointerId);
    }
  }
  drag.pressed = null;
  drag.held.value = null;
  drag.target.value = null;
}

function onPointerCancel(drag: Drag, event: PointerEvent) {
  if (!drag.pressed || event.pointerId !== drag.pressed.pointerId) return;
  endDrag(drag, event);
}

/**
 * Escape lets the held topic go where it was. The press stays known until
 * its release, so the click that comes with the release is not taken for a
 * press of the pin.
 */
function cancelDrag(drag: Drag) {
  if (drag.pressed) drag.pressed.cancelled = true;
  drag.held.value = null;
  drag.target.value = null;
}
