import { nextTick, onMounted, onUnmounted, type Ref } from "vue";
import { roomApi } from "@/entities/game";
import { LIVE_TAIL_POLL_INTERVAL_MS } from "@/shared/lib/utils/liveTail";
import type { CursorEnvelope, Message } from "@/shared/api/models/common";
import { initBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeInteractive";
import { isNearBottom, tailUpdate } from "@/shared/lib/utils/liveTail";
import type { ChatRoomAdmission } from "./useChatRoomAdmission";
import { PAGE_SIZE, type ChatRoomMessages } from "./useChatRoomMessages";

interface ChatRoomLiveTailOptions {
  chatRoomId: Ref<string | null>;
  container: Ref<HTMLElement | null>;
  admission: ChatRoomAdmission;
  roomRefused: Ref<boolean>;
  feed: ChatRoomMessages;
}

interface LiveTail extends ChatRoomLiveTailOptions {
  timer: ReturnType<typeof setInterval> | null;
  reading: boolean;
}

function isAtBottom(tail: LiveTail): boolean {
  const el = tail.container.value;
  if (!el) return true;
  return isNearBottom(el);
}

function tailMayBeRead(tail: LiveTail): boolean {
  const { admission, feed } = tail;
  if (document.hidden || !admission.readsRoom.value) return false;
  if (tail.roomRefused.value || admission.denied.value || feed.error.value)
    return false;
  if (feed.loading.value || feed.loadingOlder.value || feed.sending.value)
    return false;
  // The timer and the return of the tab can read at once and append twice.
  return !tail.reading;
}

// Another room, a window being replaced or a refusal meanwhile.
function answerIsStale(tail: LiveTail, roomId: string): boolean {
  return (
    tail.chatRoomId.value !== roomId ||
    tail.feed.loading.value ||
    tail.admission.denied.value ||
    tail.roomRefused.value ||
    !!tail.feed.error.value
  );
}

function takeTail(tail: LiveTail, data: CursorEnvelope<Message>) {
  const { feed } = tail;
  const arrived = data.resources;
  const update = tailUpdate(feed.messages.value, arrived);
  if (update.kind === "none") return;
  if (update.kind === "append" && !update.fresh.length) return;

  const wasAtBottom = isAtBottom(tail);
  if (update.kind === "gap") {
    // The page in hand is the window, without loadInitial and its skeletons;
    // under a reader up in the history it waits for his return to the tail.
    if (!wasAtBottom) return;
    feed.messages.value = arrived;
    feed.paging.value = data.paging;
  } else {
    // Paging stays: prevCursor points before the oldest message held.
    feed.messages.value = [...feed.messages.value, ...update.fresh];
  }
  nextTick(() => initBbcodeInteractive(tail.container.value));
  if (!wasAtBottom) return;
  feed.scrollToBottom();
  // Only for a reader the new messages have reached.
  feed.markRead();
}

async function pollForNewer(tail: LiveTail) {
  const roomId = tail.chatRoomId.value;
  if (!roomId || !tailMayBeRead(tail)) return;

  tail.reading = true;
  try {
    const { data, error: err } = await roomApi.getChatMessages(
      roomId,
      undefined,
      PAGE_SIZE,
    );
    if (answerIsStale(tail, roomId)) return;

    if (err) {
      // A refusal is taken as on the initial load; anything else is silent
      // and the next tick asks again.
      tail.admission.takeRefusal(err.status);
      return;
    }
    if (!data) return;
    takeTail(tail, data);
  } finally {
    tail.reading = false;
  }
}

function startPolling(tail: LiveTail) {
  if (tail.timer) return;
  tail.timer = setInterval(
    () => pollForNewer(tail),
    LIVE_TAIL_POLL_INTERVAL_MS,
  );
}

function stopPolling(tail: LiveTail) {
  if (tail.timer) {
    clearInterval(tail.timer);
    tail.timer = null;
  }
}

/**
 * The live tail of a chat room of a game, which has no push of its own: the
 * newest page is read again on the site's cadence while the tab is on screen,
 * the way API_DESIGN.md ("Realtime") states for every live list.
 */
export function useChatRoomLiveTail(options: ChatRoomLiveTailOptions): void {
  const tail: LiveTail = { ...options, timer: null, reading: false };

  function handleVisibilityChange() {
    if (document.hidden) {
      stopPolling(tail);
      return;
    }
    startPolling(tail);
    pollForNewer(tail);
  }

  onMounted(() => {
    if (!document.hidden) startPolling(tail);
    document.addEventListener("visibilitychange", handleVisibilityChange);
  });
  onUnmounted(() => {
    stopPolling(tail);
    document.removeEventListener("visibilitychange", handleVisibilityChange);
  });
}
