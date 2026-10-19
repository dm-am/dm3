import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { GlobalChatMessage } from "./types";
import type { CursorPaging } from "@/shared/api/models/common";
import globalChatApi from "../api/globalChatApi";
import { unwrapResource } from "@/shared/api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { fromSiteInput } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores";

// The window of messages; the events are useGlobalChatEventsStore.
export const useGlobalChatStore = defineStore("globalChat", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const messages = ref<GlobalChatMessage[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const loadingBefore = ref(false);
  const loadingAfter = ref(false);
  const sending = ref(false);
  const hasMoreBefore = ref(true);
  const hasMoreAfter = ref(false);
  const highlightedMessageId = ref<string | null>(null);
  // Apart from `error`, so a failed page leaves the messages on screen.
  const errorBefore = ref<string | null>(null);
  const errorAfter = ref<string | null>(null);

  const prevCursor = ref<string | null>(null);
  const nextCursor = ref<string | null>(null);

  const messagesById = new Map<string, GlobalChatMessage>();

  const MAX_MESSAGES = 500;

  // A short answer means the server had nothing more: a quiet chat, not a drifted window.
  const LATEST_PAGE_SIZE = 50;

  const LOAD_FAILURE = "Не удалось загрузить сообщения";

  // Bumped on every replacement of the window: an answer for the old one must not merge in.
  const windowGuard = createRequestGuard();

  // One catch-up at a time: a push and a timer tick would otherwise read the same page twice.
  let pollInFlight = false;
  let pollQueued = false;
  // The position the last caller measured is the one the queued run answers.
  let queuedAtTail = true;

  function syncMessagesMap() {
    messagesById.clear();
    for (const msg of messages.value) {
      messagesById.set(msg.id, msg);
    }
  }

  const reanchorBefore = ref<string | null>(null);

  // The old cursor would skip what the trim dropped, so the next scroll up re-anchors on a message.
  function trimOldMessages() {
    if (messages.value.length > MAX_MESSAGES) {
      messages.value = messages.value.slice(-MAX_MESSAGES);
      hasMoreBefore.value = true;
      prevCursor.value = null;
      reanchorBefore.value = messages.value[0]?.id ?? null;
      syncMessagesMap();
    }
  }

  function showPage(
    resources: GlobalChatMessage[],
    paging: CursorPaging | null | undefined,
  ) {
    windowGuard.next();
    messages.value = resources;
    prevCursor.value = paging?.prevCursor ?? null;
    nextCursor.value = paging?.nextCursor ?? null;
    hasMoreBefore.value = paging?.hasPrev ?? false;
    hasMoreAfter.value = paging?.hasNext ?? false;
    syncMessagesMap();
  }

  async function fetchMessages() {
    loading.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessages({
        limit: LATEST_PAGE_SIZE,
      });
      if (apiError) {
        error.value = LOAD_FAILURE;
        return;
      }
      showPage(data?.resources || [], data?.paging);
    } finally {
      loading.value = false;
    }
  }

  async function fetchMoreBefore() {
    if (loadingBefore.value || !hasMoreBefore.value) return;
    if (!prevCursor.value && !reanchorBefore.value) return;
    loadingBefore.value = true;
    errorBefore.value = null;
    try {
      const { data, error: apiError } = prevCursor.value
        ? await globalChatApi.getMessagesBefore(
            prevCursor.value,
            LATEST_PAGE_SIZE,
          )
        : await globalChatApi.getMessagesAround(
            reanchorBefore.value!,
            LATEST_PAGE_SIZE,
          );
      if (apiError) {
        // hasMoreBefore stays, so the sentinel can retry.
        errorBefore.value = LOAD_FAILURE;
        return;
      }
      // A re-anchored page is centred on a message on screen, so its newer half goes.
      const older = data
        ? data.resources.filter((message) => !messagesById.has(message.id))
        : [];

      if (older.length > 0) {
        messages.value = [...older, ...messages.value];
        prevCursor.value = data?.paging?.prevCursor ?? null;
        hasMoreBefore.value = data?.paging?.hasPrev ?? false;
        reanchorBefore.value = null;
        syncMessagesMap();
      } else {
        hasMoreBefore.value = false;
      }
    } finally {
      loadingBefore.value = false;
    }
  }

  async function fetchMoreAfter() {
    if (loadingAfter.value || !hasMoreAfter.value || !nextCursor.value) return;
    loadingAfter.value = true;
    errorAfter.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessagesAfter(
        nextCursor.value,
        LATEST_PAGE_SIZE,
      );
      if (apiError) {
        errorAfter.value = LOAD_FAILURE;
        return;
      }
      if (data && data.resources.length > 0) {
        messages.value = [...messages.value, ...data.resources];
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        syncMessagesMap();
        trimOldMessages();
      } else {
        hasMoreAfter.value = false;
      }
    } finally {
      loadingAfter.value = false;
    }
  }

  // A full page of unknown messages means the window drifted off and the page replaces it;
  // appended, it would leave a hole nothing on screen reaches.
  async function readLatestPage(atTail: boolean) {
    const heldWindow = windowGuard.current();
    const { data } = await globalChatApi.getMessages({
      limit: LATEST_PAGE_SIZE,
    });
    if (!windowGuard.isCurrent(heldWindow)) return;
    if (!data || data.resources.length === 0) return;

    const fresh = data.resources.filter((m) => !messagesById.has(m.id));
    if (fresh.length === LATEST_PAGE_SIZE) {
      // A reader scrolled up keeps his page until he returns to the tail.
      if (!atTail && messages.value.length > 0) return;
      showPage(data.resources, data.paging);
      nextCursor.value = null;
      hasMoreAfter.value = false;
      return;
    }
    if (fresh.length > 0) {
      messages.value = [...messages.value, ...fresh];
      syncMessagesMap();
      trimOldMessages();
    }
  }

  /** atTail is measured before the call: growing the feed moves its bottom away. */
  async function pollForNewer(atTail = true) {
    if (pollInFlight) {
      pollQueued = true;
      queuedAtTail = atTail;
      return;
    }
    pollInFlight = true;
    try {
      await readLatestPage(atTail);
      while (pollQueued) {
        pollQueued = false;
        await readLatestPage(queuedAtTail);
      }
    } catch {
      // The next tick retries.
    } finally {
      pollInFlight = false;
      pollQueued = false;
    }
  }

  async function navigateToMessage(messageId: string) {
    loading.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessagesAround(
        messageId,
        LATEST_PAGE_SIZE,
      );
      if (apiError) {
        error.value = LOAD_FAILURE;
        return;
      }
      if (data && data.resources.length > 0) {
        showPage(data.resources, data.paging);
        highlightedMessageId.value = messageId;
      }
    } finally {
      loading.value = false;
    }
  }

  // False when the jump fell back to the latest messages, which are not the picked day.
  const landedOnRequestedDate = ref(false);

  async function navigateToDate(date: string) {
    loading.value = true;
    error.value = null;
    landedOnRequestedDate.value = false;
    try {
      // Midnight on the site's clock, the zone the chat's dates are shown in.
      const timestampUtc = fromSiteInput(`${date}T00:00`);
      if (!timestampUtc) {
        await fetchMessages();
        return;
      }
      const { data, error: apiError } = await globalChatApi.getMessagesNearDate(
        timestampUtc,
        LATEST_PAGE_SIZE,
      );
      if (apiError) {
        error.value = LOAD_FAILURE;
        return;
      }
      if (data && data.resources.length > 0) {
        showPage(data.resources, data.paging);
        // No highlight: the feed's history window finds the first message of the day.
        landedOnRequestedDate.value = true;
      } else {
        await fetchMessages();
      }
    } catch {
      await fetchMessages();
    } finally {
      loading.value = false;
    }
  }

  async function jumpToLatest() {
    await fetchMessages();
    highlightedMessageId.value = null;
  }

  function clearHighlight() {
    highlightedMessageId.value = null;
  }

  /** The error goes back, so the caller can restore the text. */
  async function sendMessage(text: string) {
    if (!text.trim()) return { error: null };
    sending.value = true;
    try {
      const { data, error } = await globalChatApi.sendMessage(text);
      if (error) return { error };
      // Keyed by id, so addMessage knows the copy the hub pushes back.
      const sent = unwrapResource<GlobalChatMessage>(data);
      if (sent) {
        if (hasMoreAfter.value) {
          await jumpToLatest();
        }
        messages.value.push(sent);
        messagesById.set(sent.id, sent);
      }
    } finally {
      sending.value = false;
    }
    return { error: null };
  }

  function addMessage(message: GlobalChatMessage) {
    // Only while the latest messages are on screen.
    if (messagesById.has(message.id) || hasMoreAfter.value) return;
    messages.value.push(message);
    messagesById.set(message.id, message);
  }

  /** The error goes back, so the editor can stay open. */
  async function updateMessage(id: string, text: string) {
    const { data, error } = await globalChatApi.updateMessage(id, text);
    const updated = unwrapResource<GlobalChatMessage>(data);
    if (!error && updated) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = updated;
        messagesById.set(id, updated);
      }
    }
    return { error };
  }

  // Struck through only on success: a refusal must not look like a deletion.
  async function deleteMessage(id: string) {
    const { error } = await globalChatApi.deleteMessage(id);
    if (!error) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        const updated = {
          ...messages.value[index],
          isRemoved: true,
          deletedBy: currentUser.value ?? null,
          deletedUtc: new Date().toISOString(),
        };
        messages.value[index] = updated;
        messagesById.set(id, updated);
      }
    }
    return { error };
  }

  async function likeMessage(id: string) {
    const { data, error } = await globalChatApi.likeMessage(id);
    const liked = unwrapResource<GlobalChatMessage>(data);
    if (liked) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = liked;
        messagesById.set(id, liked);
      }
    }
    return { error };
  }

  async function unlikeMessage(id: string) {
    const { error } = await globalChatApi.unlikeMessage(id);
    // The answer has no body, so the likes are changed here.
    if (!error && currentUser.value) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        const msg = messages.value[index];
        const updated = {
          ...msg,
          likes: msg.likes.filter(
            (u) => u.username !== currentUser.value?.username,
          ),
        };
        messages.value[index] = updated;
        messagesById.set(id, updated);
      }
    }
    return { error };
  }

  return {
    messages,
    loading,
    error,
    loadingBefore,
    loadingAfter,
    errorBefore,
    errorAfter,
    sending,
    hasMoreBefore,
    hasMoreAfter,
    highlightedMessageId,
    landedOnRequestedDate,
    prevCursor,
    nextCursor,
    fetchMessages,
    fetchMoreBefore,
    fetchMoreAfter,
    pollForNewer,
    navigateToMessage,
    navigateToDate,
    jumpToLatest,

    clearHighlight,
    sendMessage,
    addMessage,
    updateMessage,
    deleteMessage,
    likeMessage,
    unlikeMessage,
  };
});
