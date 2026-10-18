import { defineStore, storeToRefs } from "pinia";
import { computed, ref } from "vue";
import type {
  GlobalChatEvent,
  GlobalChatEventSummary,
  GlobalChatMessage,
} from "./types";
import type {
  ApiResult,
  Envelope,
  GeneralError,
} from "@/shared/api/models/common";
import globalChatApi from "../api/globalChatApi";
import { unwrapResource } from "@/shared/api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { createFreshness } from "@/shared/lib/utils/keyedCache";
import { fromSiteInput } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores";
import { NotificationType } from "@/shared/api/models/notifications";

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

  /** How many messages "the latest page" holds, asked for and read back: a
   * short answer means the server had nothing more to give, which is how
   * pollForNewer tells a quiet chat from a window that has drifted off. */
  const LATEST_PAGE_SIZE = 50;

  /** The held window, guarded. Every wholesale replacement of it (a fresh
   * load, a jump to a message or a date, a catch-up that found the window
   * drifted off) bumps the guard. A request that started before the bump
   * describes a window nobody is looking at any more, so its answer is dropped
   * rather than merged into the one now on screen: appending a tail read
   * against the old window would put its messages after ones that are newer
   * than they are. The shared guard rather than a counter of this store's own,
   * because a store that keeps its own renders whichever reply lands last. */
  const windowGuard = createRequestGuard();

  /** Catch-up reads are coalesced: one is in flight at a time, and the calls
   * that arrive meanwhile are served by one more run after it. A push and a
   * timer tick otherwise start two reads of the same page at once, and the
   * slower answer applies on top of the faster one. */
  let pollInFlight = false;
  let pollQueued = false;
  /** Where the reader stood when the queued call was made. The run it asks for
   * happens after the one in flight, so the position measured by the caller
   * that arrived last is the one it is answered with. */
  let queuedAtTail = true;

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
        limit: LATEST_PAGE_SIZE,
      });
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
      windowGuard.next();
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
  //
  // Unless the page and the held window no longer touch. A quiet socket, a
  // slept tab or a long enough outage lets more than a page go by, and then not
  // one line of the answer is recognised. Appending it there would join two
  // windows with a hole between them, and the hole is unreachable: the cursors
  // on either side belong to the edges of a window that is now a splice, and
  // nothing on screen says a stretch of the conversation is missing. So a full
  // page of strangers is not caught up to: it is read as "the window has
  // drifted off", and the page in hand IS the window to show, applied here
  // with its own paging instead of being asked for a second time. The
  // second read used to go through fetchMessages, which raises `loading`: the
  // feed blinked into skeletons, the sentinels went with it, and a failure of
  // that second request replaced a feed that was perfectly readable with an
  // error. A SHORT page of strangers means the opposite: the server gave
  // everything it had, so there is nothing between it and what is held,
  // however little the two share. The same replacement serves a feed that is
  // still empty, because a first load that failed leaves no cursors behind and
  // a page appended over that leaves the top sentinel with nowhere to go.
  async function readLatestPage(atTail: boolean) {
    const heldWindow = windowGuard.current();
    const { data } = await globalChatApi.getMessages({
      limit: LATEST_PAGE_SIZE,
    });
    // The window was replaced while this was in flight. The answer describes
    // the window that used to be there.
    if (!windowGuard.isCurrent(heldWindow)) return;
    if (!data || data.resources.length === 0) return;

    const fresh = data.resources.filter((m) => !messagesById.has(m.id));
    if (fresh.length === LATEST_PAGE_SIZE) {
      // A reader who has scrolled up is reading, and the window under him is
      // the page he is reading: replacing it takes that page away and leaves
      // him at the end of a conversation he never asked to be taken to. So the
      // replacement waits for his return to the tail, and the read that finds
      // him there makes it. A window that holds nothing has no reader in it to
      // take anything away from, which is the case a first load that failed
      // leaves behind.
      if (!atTail && messages.value.length > 0) return;
      windowGuard.next();
      messages.value = data.resources;
      prevCursor.value = data.paging?.prevCursor ?? null;
      hasMoreBefore.value = data.paging?.hasPrev ?? false;
      // The latest page has nothing after it by definition, whatever the
      // paging says about the direction nobody asked for.
      nextCursor.value = null;
      hasMoreAfter.value = false;
      syncMessagesMap();
      return;
    }
    if (fresh.length > 0) {
      messages.value = [...messages.value, ...fresh];
      syncMessagesMap();
      trimOldMessages();
    }
  }

  /**
   * Catch up with what arrived since the last read.
   *
   * `atTail` is where the reader stands in the feed, which only the page can
   * measure, and it is what decides whether a window that has drifted off may
   * be replaced under him. It is measured BEFORE the call, for the same reason
   * the page measures it before appending: growing the feed moves its bottom
   * away from a reader standing at it, so a measurement taken afterwards
   * answers "no" for everybody. A caller that holds no feed of its own — and
   * so has no reader to move — is at the tail by definition.
   */
  async function pollForNewer(atTail = true) {
    if (pollInFlight) {
      pollQueued = true;
      queuedAtTail = atTail;
      return;
    }
    pollInFlight = true;
    try {
      await readLatestPage(atTail);
      // Whatever asked while the read was in flight is served by one more run
      // after it, in order, rather than by a second read alongside it.
      while (pollQueued) {
        pollQueued = false;
        await readLatestPage(queuedAtTail);
      }
    } catch {
      // Silent — next poll tick will retry.
    } finally {
      pollInFlight = false;
      pollQueued = false;
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
        windowGuard.next();
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
      // Anchor from midnight on the site's clock (siteTime: the account's
      // zone for a signed-in reader, the device's for a guest), so "Перейти к
      // дате" lands on the day the reader picked by the dates the chat shows
      // them. The device's midnight was a day off for a reader whose account
      // keeps another zone, and a UTC anchor for everyone east of UTC. The
      // exact first-of-day message is then resolved client-side
      // (useChatHistoryWindow.loadDate) once results are in, since the
      // backend answers with a window around the first message at or after
      // that midnight, and up to half of the window stands before the day.
      const timestampUtc = fromSiteInput(`${date}T00:00`);
      if (!timestampUtc) {
        // Not a day at all: the latest messages, as a failed jump lands.
        await fetchMessages();
        return;
      }
      const { data, error: apiError } = await globalChatApi.getMessagesNearDate(
        timestampUtc,
        50,
      );
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
      if (data && data.resources.length > 0) {
        windowGuard.next();
        messages.value = data.resources;
        prevCursor.value = data.paging?.prevCursor ?? null;
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        // The scroll target is resolved by the feed's history window
        // (useChatHistoryWindow.loadDate resolves the exact first-of-day
        // message client-side and scrolls to it) — no store-level
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
      // The answer is an envelope; returning it as the message handed the
      // caller an object with no id and no text, typed as if it had both.
      return unwrapResource<GlobalChatMessage>(data);
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

  /** Returns the error when the send failed, so the caller can restore the text. */
  async function sendMessage(text: string) {
    if (!text.trim()) return { error: null };
    sending.value = true;
    try {
      const { data, error } = await globalChatApi.sendMessage(text);
      if (error) return { error };
      // Out of the envelope. Taken as it came, one's own line appeared blank
      // in the chat, and it was keyed in messagesById under undefined, so the
      // duplicate guard in addMessage no longer recognised the copy the hub
      // pushed back and the same line arrived twice.
      const sent = unwrapResource<GlobalChatMessage>(data);
      if (sent) {
        // If we're not at the latest, jump to latest first
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
    if (messagesById.has(message.id) || hasMoreAfter.value) return;
    // Only add if we're viewing the latest messages
    messages.value.push(message);
    messagesById.set(message.id, message);
  }

  /** Returns the error when the edit failed, so the editor can stay open. */
  async function updateMessage(id: string, text: string) {
    const { data, error } = await globalChatApi.updateMessage(id, text);
    // Out of the envelope: the edited line was replaced by the wrapper both on
    // screen and in the map the jump-to-message link reads.
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

  /**
   * Returns the error when the delete failed. Striking the message through on
   * a refusal is the worst of both: the moderator reads the deleted-message
   * placeholder beside the line that says it was not deleted, and leaves
   * believing the text is gone for everyone else.
   */
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
    // Out of the envelope: a like erased the line it was given to.
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

  /** Every read of the list is tagged by the shared guard, and an answer that
   * is not the newest tag is dropped. Two reads overlap easily (the page asks
   * on mount, the socket coming up asks again, a push about an event lands on
   * top), and whichever answer is slower would otherwise be the one that stays,
   * putting an older list on screen than the one already read. */
  const eventsGuard = createRequestGuard();

  /** How recently the list has to have been read for a caller that has just
   * had it read to leave it alone. Wide enough to cover the gap between the
   * read a page makes on open and the connection that follows it, short enough
   * that a reconnection after any real outage falls outside and does re-read. */
  const EVENTS_FRESH_MS = 5000;

  /** Whether the list has been read recently enough to be left alone. The
   * moment and the arithmetic over it are one thing and live in one place: the
   * moment used to be written here and subtracted from the clock on the page,
   * which is a rule nothing can read whole and nothing can check. */
  const eventsFreshness = createFreshness(EVENTS_FRESH_MS);

  /**
   * How the newest read of the list ended, for the strip above the feed: an
   * empty list says "no events" only once a read has answered with one.
   * "pending" until the first answer, so the strip says nothing yet;
   * "failed" when the newest read was refused, so it says the events did not
   * load and offers to read again, instead of claiming there are none. A
   * refused re-read keeps the list it had.
   */
  const eventsLoad = ref<"pending" | "loaded" | "failed">("pending");

  // No toast on failure: events are supplementary, not primary content, and
  // the strip says it in its own line (eventsLoad).
  async function fetchEvents() {
    const request = eventsGuard.next();
    // Marked read before the request rather than on its answer: a caller that
    // asks while this one is still in flight would otherwise get no sign that
    // the question is already being asked, and ask it again. That is what a
    // socket coming up during the read on mount used to do.
    eventsFreshness.touch();
    const { data, error: apiError } = await globalChatApi.getEvents();
    if (!eventsGuard.isCurrent(request)) return;
    if (apiError) {
      // A read that failed is not a read: the list is as old as it was, and the
      // next caller has to go and get it.
      eventsFreshness.clear();
      eventsLoad.value = "failed";
      return;
    }
    events.value = data?.resources ?? [];
    eventsLoad.value = "loaded";
  }

  /** Read the list unless it has only just been read. */
  async function fetchEventsIfFresh() {
    if (eventsFreshness.isFresh()) return;
    await fetchEvents();
  }

  /**
   * The events list on a realtime push.
   *
   * The list is read once, when the chat opens, and nothing polls it: an event
   * starting or ending is the only thing that can make it stale while the tab
   * stays open, and until that arrived the strip above the feed went on
   * describing the state of the chat at page load. Which pushes those are is
   * knowledge about this list rather than about the screen that renders it, so
   * it lives here and re-reads through the one fetch path above. Every other
   * push on the socket (a new message above all) is somebody else's business:
   * re-reading on those would put a request behind every line anybody types.
   */
  async function refreshEventsOnNotification(eventType: NotificationType) {
    if (
      eventType !== NotificationType.GlobalChatEventStarted &&
      eventType !== NotificationType.GlobalChatEventEnded
    ) {
      return;
    }
    await fetchEvents();
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

  /**
   * One event action, whichever it is.
   *
   * All four endpoints answer with the whole event, so the details cache is
   * filled from the response instead of being read a second time. The strip
   * above the feed is drawn from the summary list, and the two fields these
   * actions change (status and participantCount) live there and nowhere else —
   * so the list is re-read as well, or a started event goes on saying "Скоро"
   * on the very line its organizer has just acted on.
   *
   * The refusal is returned rather than swallowed. `Api` resolves on a 403 the
   * same way it resolves on a 200, so an action nobody was allowed to take
   * looks exactly like one that went through until the caller says otherwise.
   */
  async function runEventAction(
    id: string,
    act: () => Promise<ApiResult<Envelope<GlobalChatEvent>>>,
  ): Promise<{ error: GeneralError | null }> {
    const { data, error } = await act();
    if (error) return { error };
    if (data?.resource) eventDetails.value[id] = data.resource;
    await fetchEvents();
    return { error: null };
  }

  /** Sign up for an open event. */
  const joinEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.joinEvent(id));

  /** Give up a place in an event (organizers cannot: the API refuses them). */
  const leaveEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.leaveEvent(id));

  /** Take a scheduled event live. Organizer only. */
  const startEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.startEvent(id));

  /** Close a live event. Organizer only. */
  const endEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.endEvent(id));

  async function unlikeMessage(id: string) {
    const { error } = await globalChatApi.unlikeMessage(id);
    // Backend returns 204 No Content, so update likes locally
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
    eventsLoad,
    eventDetails,
    liveEvent,
    upcomingEvents,
    fetchEvents,
    fetchEventsIfFresh,
    refreshEventsOnNotification,
    fetchEventDetails,
    joinEvent,
    leaveEvent,
    startEvent,
    endEvent,
  };
});
