import { computed, ref, watch, type Ref } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import type { Topic } from "@/entities/forum";

export interface PinnedOrderOptions {
  /** The pinned block as the board holds it, in the order the server keeps. */
  pinned: Ref<Topic[] | null>;
  /**
   * Whether the block can be reordered now: a moderator of the board, with the
   * block standing on its own at the head of the listing.
   */
  enabled: Ref<boolean>;
  /** Sends the whole new order. */
  send: (topicIds: string[]) => Promise<{ error?: GeneralError | null }>;
  /** Says a refused save: the block is back in the server's order by then. */
  refused: (error: GeneralError) => void;
}

const idsOf = (topics: readonly Topic[]): string[] =>
  topics.map((topic) => String(topic.id));

const sameSet = (a: readonly string[], b: readonly string[]): boolean =>
  a.length === b.length && [...a].sort().join() === [...b].sort().join();

/** How far a pressed pin travels before the press counts as a drag. */
const DRAG_THRESHOLD = 4;

/**
 * The order of the pinned topics, changed on the listing itself: a pinned
 * topic is dragged by its pin, or moved a place at a time with the arrow keys
 * on it, and the new order goes to the server the moment the topic is let go.
 *
 * The block shows the new order at once, before the server answers. A save
 * sent while another is out waits for it, and only the last order waiting is
 * sent: two saves are never out together, and the one that lands last is the
 * one made last. A refusal puts the block back in the order the server keeps
 * and drops whatever waited behind it.
 */
export function usePinnedOrder(options: PinnedOrderOptions) {
  const { pinned, enabled } = options;

  /** The order on screen while a save is out or waiting, else null. */
  const local = ref<string[] | null>(null);
  const saving = ref(false);
  let waiting: string[] | null = null;
  /** What the last move did, for a screen reader. */
  const announcement = ref("");

  /** The pinned block as it stands on screen. */
  const order = computed<Topic[]>(() => {
    const block = pinned.value ?? [];
    const ids = local.value;
    if (!ids) return block;
    const byId = new Map(block.map((topic) => [String(topic.id), topic]));
    const placed = ids
      .map((id) => byId.get(id))
      .filter((topic): topic is Topic => topic !== undefined);
    const rest = block.filter((topic) => !ids.includes(String(topic.id)));
    return [...placed, ...rest];
  });

  function drop() {
    waiting = null;
    local.value = null;
  }

  async function save(ids: string[]) {
    if (saving.value) {
      waiting = ids;
      return;
    }
    saving.value = true;
    try {
      for (let next: string[] | null = ids; next; next = waiting) {
        waiting = null;
        // The block changed under the wait: a pin taken on or off, another
        // board. The order that waited names other topics than it holds.
        if (!sameSet(next, idsOf(pinned.value ?? []))) break;
        const { error } = await options.send(next);
        if (error) {
          drop();
          options.refused(error);
          return;
        }
      }
      // The server keeps the last order sent, and the block read after it says
      // so: the copy on screen has nothing left to add.
      drop();
    } finally {
      saving.value = false;
    }
  }

  /** Takes the topic at `from` out and stands it at `to`, and saves the order. */
  function move(from: number, to: number): boolean {
    const ids = idsOf(order.value);
    if (from === to || from < 0 || to < 0) return false;
    if (from >= ids.length || to >= ids.length) return false;
    const [id] = ids.splice(from, 1);
    ids.splice(to, 0, id);
    local.value = ids;
    const topic = order.value[to];
    announcement.value = `${topic?.title ?? ""}: место ${to + 1} из ${ids.length}`;
    void save(ids);
    return true;
  }

  // ---------------------------------------------------------------------------
  // Drag. Pointer events rather than the browser's drag and drop: the row stays
  // where it is while it is held, a line between the rows shows where it will
  // stand, and the cursor is the page's to set for the whole of the drag.
  // ---------------------------------------------------------------------------

  /** The pinned topic held by a drag, as its place in the block. */
  const held = ref<number | null>(null);
  /**
   * Where a release would stand the held topic: before the row of that place,
   * or after the last row when it equals the length of the block. Null while
   * a release would leave the order as it is.
   */
  const target = ref<number | null>(null);

  let pressed: {
    index: number;
    pointerId: number;
    startY: number;
    rows: () => HTMLElement[];
    /** The press travelled far enough to hold its topic. */
    dragged: boolean;
    /** Escape let the held topic go; the press still waits for its release. */
    cancelled: boolean;
  } | null = null;

  /**
   * Set when a drag ends: the browser follows the release with a click on the
   * pin it started on, and a click on that pin takes the pin off its topic.
   * Taken by that click, and dropped by the next press and at the end of the
   * task the release came in, so a click that never came does not eat the next
   * one, from the keyboard included.
   */
  let dragClick = false;

  function expectDragClick() {
    dragClick = true;
    setTimeout(() => {
      dragClick = false;
    });
  }

  /**
   * Whether a click on a pin is the one that follows the release of a drag,
   * and not a press. Answers true once per drag.
   */
  function isDragClick(): boolean {
    const was = dragClick;
    dragClick = false;
    return was;
  }

  function onPointerDown(
    index: number,
    event: PointerEvent,
    rows: () => HTMLElement[],
  ) {
    if (!enabled.value || event.button !== 0) return;
    // A second finger on a touch screen is not a second drag.
    if (event.isPrimary === false) return;
    // No text selection starts under a drag.
    event.preventDefault();
    dragClick = false;
    pressed = {
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

  /** The gap the pointer is nearest to, counted in rows of the block. */
  function gapAt(y: number, rows: HTMLElement[]): number {
    return rows.filter((row) => {
      const box = row.getBoundingClientRect();
      return y > box.top + box.height / 2;
    }).length;
  }

  function onPointerMove(event: PointerEvent) {
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

  function endDrag(event: PointerEvent) {
    const handle = event.currentTarget;
    if (handle instanceof Element && pressed) {
      if (handle.hasPointerCapture?.(pressed.pointerId)) {
        handle.releasePointerCapture(pressed.pointerId);
      }
    }
    pressed = null;
    held.value = null;
    target.value = null;
  }

  function onPointerUp(event: PointerEvent) {
    if (!pressed || event.pointerId !== pressed.pointerId) return;
    const from = held.value;
    const gap = target.value;
    // A press that became a drag ends in a click all the same, let go where
    // it stood or taken back with Escape included; a press that did not travel
    // is a press, and its click is the toggle of the pin.
    const dragged = pressed.dragged;
    endDrag(event);
    if (dragged) expectDragClick();
    if (from === null || gap === null) return;
    // A gap below the held row counts that row too.
    move(from, gap > from ? gap - 1 : gap);
  }

  function onPointerCancel(event: PointerEvent) {
    if (!pressed || event.pointerId !== pressed.pointerId) return;
    endDrag(event);
  }

  /**
   * Escape lets the held topic go where it was. The press stays known until
   * its release, so the click that comes with the release is not taken for a
   * press of the pin.
   */
  function cancelDrag() {
    if (pressed) pressed.cancelled = true;
    held.value = null;
    target.value = null;
  }

  /**
   * The arrow keys on a pin move its topic a place up or down. Returns the id
   * of the topic moved, for the caller to keep the caret on it.
   */
  function onKeydown(index: number, event: KeyboardEvent): string | null {
    if (event.key === "Escape" && held.value !== null) {
      event.preventDefault();
      cancelDrag();
      return null;
    }
    const delta =
      event.key === "ArrowUp" ? -1 : event.key === "ArrowDown" ? 1 : 0;
    if (!delta || !enabled.value || held.value !== null) return null;
    event.preventDefault();
    const id = String(order.value[index]?.id ?? "");
    return move(index, index + delta) ? id : null;
  }

  // The block left the screen or the moderator lost the right to order it: a
  // drag in hand ends, and an order waiting to be sent is not sent.
  watch(enabled, (isEnabled) => {
    if (isEnabled) return;
    cancelDrag();
    waiting = null;
    if (!saving.value) local.value = null;
  });

  return {
    order,
    saving,
    held,
    target,
    announcement,
    move,
    onPointerDown,
    onPointerMove,
    onPointerUp,
    onPointerCancel,
    onKeydown,
    isDragClick,
  };
}
