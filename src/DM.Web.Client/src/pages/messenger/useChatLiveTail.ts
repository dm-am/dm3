import { onMounted, onUnmounted, watch, type Ref } from "vue";
import { messagingApi, useMessagingStore } from "@/entities/message";
import type { ChatId, Message } from "@/entities/message";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { tailUpdate } from "@/shared/lib/utils/liveTail";
import {
  NotificationType,
  type SignalRNotification,
} from "@/shared/api/models/notifications";

/**
 * What the newest page of a correspondence does to the window on screen.
 * `reread`: the window is read again whole; `defer`: the same, once the reader
 * is back at the tail; `append`: the window as it becomes, and what in it is
 * new to the reader.
 */
type TailJoin =
  | { kind: "none" }
  | { kind: "reread" }
  | { kind: "defer" }
  | { kind: "append"; messages: Message[]; fresh: Message[] };

/**
 * The newest page against the window on screen, decided by the rule every
 * live list keeps (tailUpdate), for a reader who stood at the tail or did not
 * (`wasAtTail`, measured before the list grows).
 */
function joinTail(
  shown: readonly Message[],
  tail: readonly Message[],
  wasAtTail: boolean,
): TailJoin {
  const update = tailUpdate(shown, tail);
  if (update.kind === "none") return update;
  if (update.kind === "gap") {
    // Not one message of the newest page is on screen: appending would leave a
    // gap, so the window is read again whole, but not under a reader who has paged
    // up: that waits for his return to the tail.
    return { kind: wasAtTail ? "reread" : "defer" };
  }
  // The part both sides hold is taken in the server's order, which also picks
  // up edits; the reader's own message went to the end the moment the POST answered.
  const tailIds = new Set(tail.map((message) => message.id));
  return {
    kind: "append",
    messages: [...shown.filter((message) => !tailIds.has(message.id)), ...tail],
    fresh: update.fresh,
  };
}

/** What a read of the tail reads and writes. */
interface LiveTail {
  /** Where the reader stands and the way down, the feed's to measure. */
  feed: { isAtTail: () => boolean; scrollToBottom: () => void };
  store: ReturnType<typeof useMessagingStore>;
  pageSize: Ref<number>;
  readingTail: boolean;
  /** A trigger that came while the reader's own message was on its way out, spent once it lands. */
  missedTailRead: boolean;
  /** New messages not yet reported read; kept while the tab is hidden. */
  pendingReadReceipt: boolean;
  /** The window must be read again whole, but not while the reader is up in it. */
  deferredWindowRead: boolean;
}

// The header badge and the open correspondence answer the same push, so a
// message on screen in a visible tab is reported read. `atTail` is measured by
// the caller before the list grew; a reader parked higher up keeps the sign
// that something arrived.
async function markTailRead(t: LiveTail, atTail: boolean) {
  const chatId = t.store.selectedChat?.id;
  if (!t.pendingReadReceipt || !chatId) return;
  if (document.visibilityState !== "visible") return;
  if (!atTail) return;
  await t.store.markAsRead(chatId);
  t.pendingReadReceipt = false;
}

async function readTail(t: LiveTail) {
  const { store } = t;
  const chatId = store.selectedChat?.id;
  if (!chatId) return;
  // A reader parked in the history is not at the tail: appending would leave a hole.
  if (store.hasMoreAfter) return;
  // A send in flight brings the newest message with it; a racing read could show it
  // twice, so the trigger is remembered and spent when the send lands.
  if (store.sending) {
    t.missedTailRead = true;
    return;
  }
  // The window is being replaced right now, and that load brings the newest page.
  if (store.loadingMessages || t.readingTail) return;
  t.readingTail = true;
  try {
    const { data } = await messagingApi.getMessages(chatId, {
      limit: t.pageSize.value,
    });
    await takeTail(t, chatId, data?.resources ?? []);
  } finally {
    t.readingTail = false;
  }
}

/** The newest page, as it landed, taken into the window on screen. */
async function takeTail(t: LiveTail, chatId: ChatId, tail: Message[]) {
  const { store, feed } = t;
  // The window may have changed while the answer was in flight; hasMoreAfter is
  // asked again, since a jump or a trimmed window puts the reader off the tail.
  if (
    !tail.length ||
    store.selectedChat?.id !== chatId ||
    store.loadingMessages ||
    store.hasMoreAfter
  ) {
    return;
  }
  // One measurement before the list grows decides both the scroll down and the receipt.
  const wasAtTail = feed.isAtTail();
  const join = joinTail(store.messagesList, tail, wasAtTail);
  if (join.kind === "none") return;
  if (join.kind === "defer") {
    // readDeferredWindow reads again when he gets there.
    t.deferredWindowRead = true;
    return;
  }
  if (join.kind === "reread") {
    await store.fetchMessages(chatId);
    t.pendingReadReceipt = true;
    feed.scrollToBottom();
    await markTailRead(t, true);
    return;
  }
  store.messagesList = join.messages;
  if (join.fresh.length > 0) {
    t.pendingReadReceipt = true;
    if (wasAtTail) feed.scrollToBottom();
    await markTailRead(t, wasAtTail);
  }
}

/** The reader back at the end: a deferred replacement is made and the tail read again. */
function readDeferredWindow(t: LiveTail) {
  if (!t.deferredWindowRead || !t.feed.isAtTail()) return;
  t.deferredWindowRead = false;
  readTail(t);
}

function handleMessagingNotification(
  t: LiveTail,
  notification: SignalRNotification,
) {
  if (notification.eventType !== NotificationType.NewMessage) return;
  const chatId = t.store.selectedChat?.id;
  // Every correspondence pushes this same event; the payload's chat tells them apart.
  const pushedChatId = notification.payload?.chatId as string | undefined;
  if (!chatId || pushedChatId !== chatId) return;
  readTail(t);
}

async function handleVisibilityChange(t: LiveTail) {
  // Events of a background tab are not replayed, so coming back starts with a read.
  if (document.visibilityState !== "visible") return;
  await readTail(t);
  // What was appended while hidden is read now, for a reader at the tail: one
  // left parked in the history comes back to the same place.
  await markTailRead(t, t.feed.isAtTail());
}

/**
 * The live tail of the open correspondence: a push is a hint, the truth is in
 * REST (API_DESIGN.md, "Realtime"), so every trigger ends in the same tail read,
 * deduplicated by id. No lease on the socket: App.vue holds it for a signed-in
 * reader, and this page only listens.
 */
export function useChatLiveTail(feed: LiveTail["feed"]) {
  const t: LiveTail = {
    feed,
    store: useMessagingStore(),
    pageSize: usePaging().pageSize,
    readingTail: false,
    missedTailRead: false,
    pendingReadReceipt: false,
    deferredWindowRead: false,
  };
  const { onNotification, isConnected } = useGlobalSignalR();
  let unsubscribeSignalR: (() => void) | null = null;
  const onPush = (notification: SignalRNotification) =>
    handleMessagingNotification(t, notification);
  const onVisibility = () => handleVisibilityChange(t);

  // A fresh connection starts by reading what it missed.
  watch(isConnected, (connected) => {
    if (connected) readTail(t);
  });

  // The read a send stood in the way of, spent once the created message is in the
  // list, so neither message shows twice nor the other side's is lost.
  watch(
    () => t.store.sending,
    (isSending) => {
      if (isSending || !t.missedTailRead) return;
      t.missedTailRead = false;
      readTail(t);
    },
  );

  onMounted(() => {
    unsubscribeSignalR = onNotification(onPush);
    document.addEventListener("visibilitychange", onVisibility);
  });

  onUnmounted(() => {
    unsubscribeSignalR?.();
    unsubscribeSignalR = null;
    document.removeEventListener("visibilitychange", onVisibility);
  });

  return { readDeferredWindow: () => readDeferredWindow(t) };
}
