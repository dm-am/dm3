import { defineStore, storeToRefs } from "pinia";
import { computed, ref } from "vue";
import type {
  GlobalChatEvent,
  GlobalChatEventSummary,
  GlobalChatMessage,
} from "./types";
import globalChatApi from "../api/globalChatApi";
import { useAuthStore } from "@/shared/stores";

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
  /** Errors from history pagination (fetchMoreBefore/fetchMoreAfter) — kept
   * separate from `error` (initial-load error) so a failed page-2 request
   * doesn't wipe already-rendered messages; surfaced inline at the sentinel
   * with a retry, per the site's stale-content-stays-visible convention. */
  const errorBefore = ref<string | null>(null);
  const errorAfter = ref<string | null>(null);

  // Cursors for pagination
  const prevCursor = ref<string | null>(null);
  const nextCursor = ref<string | null>(null);

  // Internal Map for O(1) message lookup by ID
  const messagesById = new Map<string, GlobalChatMessage>();

  /** Max messages in memory to prevent unbounded growth during long scrolling sessions */
  const MAX_MESSAGES = 500;

  // Sync Map when messages array changes
  function syncMessagesMap() {
    messagesById.clear();
    for (const msg of messages.value) {
      messagesById.set(msg.id, msg);
    }
  }

  /** Message to re-anchor "load older" on after a trim dropped the page it pointed at */
  const reanchorBefore = ref<string | null>(null);

  /**
   * Trim messages to MAX_MESSAGES, keeping the most recent.
   *
   * The cursor the server handed out points before the oldest message we HELD,
   * and trimming drops exactly those — so after a trim it would fetch a page
   * older than what stays on screen and leave a gap. A message id is not a
   * cursor either: the cursor is an opaque server value, and the id put here
   * before failed to decode, which the server answers by returning the latest
   * page — 50 newest messages prepended to the top of the scrollback.
   *
   * So the cursor is dropped and the new first message is remembered instead:
   * the next scroll up re-anchors on it.
   */
  function trimOldMessages() {
    if (messages.value.length > MAX_MESSAGES) {
      messages.value = messages.value.slice(-MAX_MESSAGES);
      hasMoreBefore.value = true; // There are definitely older messages now
      prevCursor.value = null;
      reanchorBefore.value = messages.value[0]?.id ?? null;
      syncMessagesMap();
    }
  }

  // Initial load - fetches the latest messages
  async function fetchMessages() {
    loading.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessages({
        limit: 50,
      });
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
      messages.value = data?.resources || [];
      prevCursor.value = data?.paging?.prevCursor ?? null;
      nextCursor.value = data?.paging?.nextCursor ?? null;
      hasMoreBefore.value = data?.paging?.hasPrev ?? false;
      hasMoreAfter.value = data?.paging?.hasNext ?? false;
      syncMessagesMap();
    } finally {
      loading.value = false;
    }
  }

  // Load older messages (scroll up)
  async function fetchMoreBefore() {
    if (loadingBefore.value || !hasMoreBefore.value) return;
    if (!prevCursor.value && !reanchorBefore.value) return;
    loadingBefore.value = true;
    errorBefore.value = null;
    try {
      const { data, error: apiError } = prevCursor.value
        ? await globalChatApi.getMessagesBefore(prevCursor.value, 50)
        : await globalChatApi.getMessagesAround(reanchorBefore.value!, 50);
      if (apiError) {
        // Network/server failure — keep hasMoreBefore as-is so the sentinel
        // stays mounted and the user can retry, instead of silently
        // disabling pagination forever.
        errorBefore.value = "Не удалось загрузить сообщения";
        return;
      }
      // The re-anchored page is a window centred on a message that is already on
      // screen, so its newer half has to go; a cursor page is entirely older.
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

  // Load newer messages (scroll down)
  async function fetchMoreAfter() {
    if (loadingAfter.value || !hasMoreAfter.value || !nextCursor.value) return;
    loadingAfter.value = true;
    errorAfter.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessagesAfter(
        nextCursor.value,
        50,
      );
      if (apiError) {
        errorAfter.value = "Не удалось загрузить сообщения";
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

  // Poll for messages that arrived after the last load, regardless of
  // hasMoreAfter/nextCursor — those reflect the initial page load's cursor
  // (which is null/false once we're caught up to "latest"), so they can't be
  // reused to detect messages that arrived afterwards. Used by guest polling
  // (no SignalR access) as a fallback for realtime updates. No-ops silently
  // on failure — this runs unattended on a timer, not user-initiated.
  //
  // Asks for the latest page outright. It used to pass the last message's raw
  // GUID as a cursor and rely on the server failing to decode it and answering
  // with the latest page anyway — the same result, resting on an error path.
  // Its paging metadata describes the latest page rather than a position, so it
  // still must not touch nextCursor/hasMoreAfter (owned by fetchMessages and
  // fetchMoreAfter), and only messages not already held are appended.
  async function pollForNewer() {
    try {
      const { data } = await globalChatApi.getMessages({ limit: 50 });
      if (data && data.resources.length > 0) {
        const fresh = data.resources.filter((m) => !messagesById.has(m.id));
        if (fresh.length > 0) {
          messages.value = [...messages.value, ...fresh];
          syncMessagesMap();
          trimOldMessages();
        }
      }
    } catch {
      // Silent — next poll tick will retry.
    }
  }

  // Navigate to a specific message (loads messages around it)
  async function navigateToMessage(messageId: string) {
    loading.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessagesAround(
        messageId,
        50,
      );
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
      if (data && data.resources.length > 0) {
        messages.value = data.resources;
        prevCursor.value = data.paging?.prevCursor ?? null;
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        highlightedMessageId.value = messageId;
        syncMessagesMap();
      }
    } finally {
      loading.value = false;
    }
  }

  /** True when navigateToDate actually landed on the requested archive date
   * (vs falling back to the latest messages — no history near that date, or
   * a request failure). Callers use this to avoid presenting the latest
   * window as if it were the picked day. */
  const landedOnRequestedDate = ref(false);

  // Navigate to messages for a specific date
  async function navigateToDate(date: string) {
    loading.value = true;
    error.value = null;
    landedOnRequestedDate.value = false;
    try {
      // Anchor from LOCAL midnight (not UTC midnight) so "Перейти к дате"
      // lands on the day the guest actually picked in their own timezone —
      // a UTC anchor resolves to the previous local day for users east of
      // UTC (and the next local day for some users west of it near DST
      // edges). The exact first-of-day message is then resolved client-side
      // (GlobalChatPage.loadArchiveDate) once results are in, since the
      // backend's nearest-cursor window can still start slightly before the
      // requested day.
      const timestampUtc = new Date(`${date}T00:00:00`).toISOString();
      const { data, error: apiError } = await globalChatApi.getMessagesNearDate(
        timestampUtc,
        50,
      );
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
      if (data && data.resources.length > 0) {
        messages.value = data.resources;
        prevCursor.value = data.paging?.prevCursor ?? null;
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        // Highlighting/scroll target resolution is owned by the page
        // (GlobalChatPage.loadArchiveDate resolves the exact first-of-day
        // message client-side and highlights it directly) — no store-level
        // highlightedMessageId here, avoiding a race with that logic.
        landedOnRequestedDate.value = true;
        syncMessagesMap();
      } else {
        // No messages found near the date, load latest
        await fetchMessages();
      }
    } catch {
      // Error - load latest
      await fetchMessages();
    } finally {
      loading.value = false;
    }
  }

  // Jump to the latest messages
  async function jumpToLatest() {
    await fetchMessages();
    highlightedMessageId.value = null;
  }

  async function fetchMessageById(
    id: string,
  ): Promise<GlobalChatMessage | null> {
    try {
      const { data } = await globalChatApi.getMessage(id);
      return data || null;
    } catch {
      return null;
    }
  }

  function findMessageInLoaded(id: string): GlobalChatMessage | null {
    return messagesById.get(id) ?? null;
  }

  function clearHighlight() {
    highlightedMessageId.value = null;
  }

  async function sendMessage(text: string) {
    if (!text.trim()) return;
    sending.value = true;
    try {
      const { data } = await globalChatApi.sendMessage(text);
      if (data) {
        // If we're not at the latest, jump to latest first
        if (hasMoreAfter.value) {
          await jumpToLatest();
        }
        messages.value.push(data);
        messagesById.set(data.id, data);
      }
    } finally {
      sending.value = false;
    }
  }

  function addMessage(message: GlobalChatMessage) {
    if (messagesById.has(message.id) || hasMoreAfter.value) return;
    // Only add if we're viewing the latest messages
    messages.value.push(message);
    messagesById.set(message.id, message);
  }

  async function updateMessage(id: string, text: string) {
    const { data } = await globalChatApi.updateMessage(id, text);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data;
        messagesById.set(id, data);
      }
    }
  }

  async function deleteMessage(id: string) {
    await globalChatApi.deleteMessage(id);
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

  async function likeMessage(id: string) {
    const { data } = await globalChatApi.likeMessage(id);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data;
        messagesById.set(id, data);
      }
    }
  }

  // ─────────────────────────────────────────────────────────────
  // Chat events (shared by the announcement banner and the input hint)
  // ─────────────────────────────────────────────────────────────
  const events = ref<GlobalChatEventSummary[]>([]);
  /** Full event details cache, keyed by event id (lazy-loaded on demand). */
  const eventDetails = ref<Record<string, GlobalChatEvent>>({});
  const eventDetailsLoading = ref<Record<string, boolean>>({});

  /** The single Live event, if one is running (backend allows at most one). */
  const liveEvent = computed(
    () => events.value.find((e) => e.status === "Live") ?? null,
  );

  /** Scheduled (not yet started) events, soonest first. Feeds the
   * "Ближайший эвент" row and its expandable section above the chat; the
   * Live event is deliberately excluded — it lives in the in-frame live
   * banner instead. */
  const upcomingEvents = computed(() =>
    events.value
      .filter((e) => e.status === "Scheduled")
      .slice()
      .sort((a, b) => (a.startsUtc < b.startsUtc ? -1 : 1)),
  );

  // Silent on failure — events are supplementary, not primary content.
  async function fetchEvents() {
    const { data, error: apiError } = await globalChatApi.getEvents();
    if (apiError) return;
    events.value = data?.resources ?? [];
  }

  async function fetchEventDetails(
    id: string,
    force = false,
  ): Promise<GlobalChatEvent | null> {
    if (!force && eventDetails.value[id]) return eventDetails.value[id];
    if (eventDetailsLoading.value[id]) return null;
    eventDetailsLoading.value[id] = true;
    try {
      const { data, error: apiError } = await globalChatApi.getEvent(id);
      if (apiError || !data?.resource) return null;
      eventDetails.value[id] = data.resource;
      return data.resource;
    } finally {
      eventDetailsLoading.value[id] = false;
    }
  }

  async function unlikeMessage(id: string) {
    await globalChatApi.unlikeMessage(id);
    // Backend returns 204 No Content, so update likes locally
    if (currentUser.value) {
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
    fetchMessageById,
    findMessageInLoaded,
    clearHighlight,
    sendMessage,
    addMessage,
    updateMessage,
    deleteMessage,
    likeMessage,
    unlikeMessage,
    events,
    eventDetails,
    liveEvent,
    upcomingEvents,
    fetchEvents,
    fetchEventDetails,
  };
});
