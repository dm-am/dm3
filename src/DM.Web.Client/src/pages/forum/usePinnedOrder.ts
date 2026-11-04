import { computed, ref, watch, type Ref } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import type { Topic } from "@/entities/forum";
import { usePinnedDrag } from "./usePinnedDrag";

export interface PinnedOrderOptions {
  /** The pinned block as the board holds it, in the order the server keeps. */
  pinned: Ref<Topic[] | null>;
  /** Whether the block can be reordered now: a board moderator, the block on its own at the head. */
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

/** One save out at a time; only the last order waiting goes after it. */
function orderSaving({ pinned, send, refused }: PinnedOrderOptions) {
  /** The order on screen while a save is out or waiting, else null. */
  const local = ref<string[] | null>(null);
  const saving = ref(false);
  let waiting: string[] | null = null;

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
        // The block changed under the wait; the waiting order names other topics.
        if (!sameSet(next, idsOf(pinned.value ?? []))) break;
        const { error } = await send(next);
        if (error) {
          drop();
          refused(error);
          return;
        }
      }
      // The block read after the save says the server's order.
      drop();
    } finally {
      saving.value = false;
    }
  }

  /** The block left the screen or the right to order it went: a waiting order is not sent. */
  function abandon() {
    waiting = null;
    if (!saving.value) local.value = null;
  }

  return { local, saving, save, abandon };
}

/**
 * The order of the pinned topics, changed on the listing by drag or arrows and
 * shown at once. One save out at a time and only the last waiting one after
 * it; a refusal puts back the server's order and drops what waited.
 */
export function usePinnedOrder(options: PinnedOrderOptions) {
  const { pinned, enabled } = options;

  const { local, saving, save, abandon } = orderSaving(options);
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

  const drag = usePinnedDrag({ enabled, move });
  const { held, cancelDrag } = drag;

  /** The arrows on a pin move its topic a place; returns its id, for the caret. */
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

  // A drag in hand ends, and a waiting order is not sent.
  watch(enabled, (isEnabled) => {
    if (isEnabled) return;
    cancelDrag();
    abandon();
  });

  return {
    order,
    saving,
    held,
    target: drag.target,
    announcement,
    move,
    onPointerDown: drag.onPointerDown,
    onPointerMove: drag.onPointerMove,
    onPointerUp: drag.onPointerUp,
    onPointerCancel: drag.onPointerCancel,
    onKeydown,
    isDragClick: drag.isDragClick,
  };
}
