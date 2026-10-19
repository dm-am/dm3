import { computed, nextTick, ref, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useBoardsStore } from "@/entities/forum";
import { userIsAdmin } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import { notifyFailure, refusalHere } from "@/shared/lib/errors";
import { usePinnedOrder } from "./usePinnedOrder";
import type { DisplayTopic } from "./topicColumns";

export interface TopicPinsOptions {
  /** Whether the pinned block stands on its own at the head of the page. */
  showPinnedBlock: Ref<boolean>;
  /** The box of the table, where the pins of the rows are found. */
  table: Ref<HTMLElement | null>;
}

/** The pins of the listing: the toggle, its refusal under the row, and the order of the block. */
export function useTopicPins({ showPinnedBlock, table }: TopicPinsOptions) {
  const store = useBoardsStore();
  const { attachedTopics, selectedBoard } = storeToRefs(store);
  const { user } = storeToRefs(useAuthStore());

  // The pins are for those the server lets pin (BoardIntentionResolver.AdministratesTopics):
  // an administrator and this board's moderators.
  const canPin = computed(() => {
    if (!user.value) return false;
    if (userIsAdmin(user.value)) return true;
    const boardModerators = selectedBoard.value?.moderators ?? [];
    return boardModerators.some((m) => m.username === user.value?.username);
  });

  // The block is reordered only when it stands on its own on page 1 unfiltered,
  // and holds two topics.
  const canReorder = computed(
    () =>
      canPin.value &&
      showPinnedBlock.value &&
      (attachedTopics.value?.length ?? 0) > 1,
  );

  const pinnedOrder = usePinnedOrder({
    pinned: attachedTopics,
    enabled: canReorder,
    send: (topicIds) => store.reorderPinnedTopics(topicIds),
    refused: (error) =>
      notifyFailure(error, "Не удалось упорядочить закрепленные топики"),
  });

  const gestures = pinGestures(canReorder, pinnedOrder, table);
  const toggle = pinToggle(store, pinnedOrder, table, gestures.isMovable);

  return {
    canPin,
    canReorder,
    pinnedOrder,
    pinRefusal: toggle.pinRefusal,
    pinHint: toggle.pinHint,
    isMovable: gestures.isMovable,
    dropMark: gestures.dropMark,
    onPinClick: toggle.onPinClick,
    onPinPointerDown: gestures.onPinPointerDown,
    onPinKeydown: gestures.onPinKeydown,
  };
}

/** The pins of a listing, as its title cells take them. */
export type TopicPins = ReturnType<typeof useTopicPins>;

type PinnedOrder = ReturnType<typeof usePinnedOrder>;

/** The press that pins or unpins a topic, and its refusal under the row. */
function pinToggle(
  store: ReturnType<typeof useBoardsStore>,
  pinnedOrder: PinnedOrder,
  table: Ref<HTMLElement | null>,
  isMovable: (row: DisplayTopic, index: number) => boolean,
) {
  const pinningTopicId = ref<string | null>(null);

  /** The last refused pin, under the row of its topic (DataTable `detailsFor`). */
  const pinRefusal = ref<{ id: DisplayTopic["id"]; text: string } | null>(null);

  async function handleTogglePin(row: DisplayTopic) {
    // Not while an order is being saved: a pin taken on or off would change the topics it names.
    if (pinningTopicId.value || pinnedOrder.saving.value) return;

    const topicId = String(row.id);
    // Read before the request: on success the row is replaced, and the refusal names the attempt.
    const wasPinned = row.isPinned;
    pinningTopicId.value = topicId;
    pinRefusal.value = null;
    try {
      const { error } = await store.togglePinTopic(topicId);
      if (error) {
        const text = refusalHere(
          error,
          wasPinned
            ? "Не удалось открепить топик"
            : "Не удалось закрепить топик",
        );
        if (text) pinRefusal.value = { id: row.id, text };
      }
    } finally {
      pinningTopicId.value = null;
    }
  }

  /**
   * Pins or unpins the topic, unless it is the click that follows a drag. From
   * the keyboard the pin keeps the caret, which a moved node would drop to <body>.
   */
  async function onPinClick(row: DisplayTopic, event: MouseEvent) {
    if (pinnedOrder.isDragClick()) return;
    const fromKeyboard = event.detail === 0;
    await handleTogglePin(row);
    if (!fromKeyboard) return;
    await nextTick();
    table.value
      ?.querySelector<HTMLElement>(`[data-pin-toggle="${row.id}"]`)
      ?.focus();
  }

  /** What the pin of a title does, under the pointer. */
  function pinHint(row: DisplayTopic, index: number): string {
    if (!row.isPinned) return "Закрепить топик";
    return isMovable(row, index)
      ? "Открепить топик. Перетащите, чтобы изменить порядок"
      : "Открепить топик";
  }

  return { pinRefusal, onPinClick, pinHint };
}

/** The order of the pinned topics, by drag or by arrows. */
function pinGestures(
  canReorder: Ref<boolean>,
  pinnedOrder: PinnedOrder,
  table: Ref<HTMLElement | null>,
) {
  /** Whether a row is a topic of the pinned block that can be moved. */
  function isMovable(row: DisplayTopic, index: number): boolean {
    return (
      canReorder.value && row.isPinned && index < pinnedOrder.order.value.length
    );
  }

  /** A press on a pin starts a drag only on a topic of the block. */
  function onPinPointerDown(
    row: DisplayTopic,
    index: number,
    event: PointerEvent,
  ) {
    if (!isMovable(row, index)) return;
    pinnedOrder.onPointerDown(index, event, pinnedRows);
  }

  /** The rows of the pinned block on screen, in their order. */
  function pinnedRows(): HTMLElement[] {
    const handles =
      table.value?.querySelectorAll<HTMLElement>("[data-pin-handle]") ?? [];
    return [...handles]
      .map((handle) => handle.closest("tr"))
      .filter((row): row is HTMLTableRowElement => row !== null);
  }

  /** Where the line of a drag stands at a row: above it, or below the last row. */
  function dropMark(row: DisplayTopic, index: number): string | null {
    if (!isMovable(row, index)) return null;
    const gap = pinnedOrder.target.value;
    if (gap === null) return null;
    if (gap === index) return "drop-before";
    const last = pinnedOrder.order.value.length - 1;
    return gap === last + 1 && index === last ? "drop-after" : null;
  }

  // The pin keeps the caret: the row is drawn anew and focus would fall to <body>.
  async function onPinKeydown(
    row: DisplayTopic,
    index: number,
    event: KeyboardEvent,
  ) {
    if (!isMovable(row, index)) return;
    const moved = pinnedOrder.onKeydown(index, event);
    if (!moved) return;
    await nextTick();
    table.value
      ?.querySelector<HTMLElement>(`[data-pin-handle="${moved}"]`)
      ?.focus();
  }

  return { isMovable, onPinPointerDown, dropMark, onPinKeydown };
}
