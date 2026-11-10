import globalChatApi from "../api/globalChatApi";
import {
  LATEST_PAGE_SIZE,
  showPage,
  syncMessagesMap,
  trimOldMessages,
  type ChatWindow,
} from "./chatWindow";

// A full page of unknown messages means the window drifted off and the page replaces it;
// appended, it would leave a hole nothing on screen reaches.
async function readLatestPage(w: ChatWindow, atTail: boolean) {
  const heldWindow = w.windowGuard.current();
  const { data } = await globalChatApi.getMessages({
    limit: LATEST_PAGE_SIZE,
  });
  if (!w.windowGuard.isCurrent(heldWindow)) return;
  if (!data || data.resources.length === 0) return;

  const fresh = data.resources.filter((m) => !w.messagesById.has(m.id));
  if (fresh.length === LATEST_PAGE_SIZE) {
    // A reader scrolled up keeps his page until he returns to the tail.
    if (!atTail && w.messages.value.length > 0) return;
    showPage(w, data.resources, data.paging);
    w.nextCursor.value = null;
    w.hasMoreAfter.value = false;
    return;
  }
  if (fresh.length > 0) {
    w.messages.value = [...w.messages.value, ...fresh];
    syncMessagesMap(w);
    trimOldMessages(w);
  }
}

export function useChatCatchUp(w: ChatWindow) {
  // One catch-up at a time: a push and a timer tick would otherwise read the same page twice.
  let pollInFlight = false;
  let pollQueued = false;
  // The position the last caller measured is the one the queued run answers.
  let queuedAtTail = true;

  /** atTail is measured before the call: growing the feed moves its bottom away. */
  async function pollForNewer(atTail = true) {
    if (pollInFlight) {
      pollQueued = true;
      queuedAtTail = atTail;
      return;
    }
    pollInFlight = true;
    try {
      await readLatestPage(w, atTail);
      while (pollQueued) {
        pollQueued = false;
        await readLatestPage(w, queuedAtTail);
      }
    } catch {
      // The next tick retries.
    } finally {
      pollInFlight = false;
      pollQueued = false;
    }
  }

  return { pollForNewer };
}
