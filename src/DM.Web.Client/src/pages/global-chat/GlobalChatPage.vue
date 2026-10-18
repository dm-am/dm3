<script setup lang="ts">
import { LIVE_TAIL_POLL_INTERVAL_MS } from "@/shared/api/client";
import {
  onMounted,
  onUnmounted,
  ref,
  reactive,
  nextTick,
  computed,
  watch,
} from "vue";
import { useModal } from "vue-final-modal";
import { useRoute, type RouteLocationRaw } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGlobalChatStore,
  type GlobalChatMessage,
} from "@/entities/global-chat";
import { useAuthStore } from "@/entities/user";
import { reachesByRank } from "@/entities/moderation";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { globalChatApi } from "@/entities/global-chat";
import { unwrapResource } from "@/shared/api";
import {
  ChatFeed,
  ChatSearchRow,
  useChatHistoryWindow,
  type ChatFeedActions,
} from "@/widgets/chat-feed";
import ChatEventsPanel from "./ChatEventsPanel.vue";
import { WarningDialog } from "@/features/moderation-actions";
import { LoginPrompt } from "@/features/auth";
import { permalinkOrigin } from "@/shared/config/site";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";

const globalChatStore = useGlobalChatStore();
const userStore = useAuthStore();
const {
  messages,
  loading,
  error,
  errorBefore,
  errorAfter,
  sending,
  hasMoreBefore,
  hasMoreAfter,
  highlightedMessageId,
  liveEvent,
  eventDetails,
} = storeToRefs(globalChatStore);
const { user } = storeToRefs(userStore);

/** The feed: the frame, the list, the toolbar and the composer (ChatFeed). */
const feed = ref<InstanceType<typeof ChatFeed> | null>(null);

// ─────────────────────────────────────────────────────────────
// Who may write: a signed-in reader, and during a closed event its participants
// ─────────────────────────────────────────────────────────────
// The in-frame live banner needs full details for ANY live event (duration
// for "идет до HH:mm" + the expandable description), and during a Live event
// with isOpen=false the backend rejects messages from non-participants (see
// MessageService) — the participants list from the same details response
// decides whether the composer stands at all. Single owner: the page fetches
// once into the shared store cache, the banner and the composer both read it.
watch(
  liveEvent,
  (ev) => {
    if (ev) {
      globalChatStore.fetchEventDetails(ev.id);
    }
  },
  { immediate: true },
);

/**
 * A closed event is running and the reader is not among its participants. The
 * server refuses his messages, so there is no field to write them in: the
 * event's own line in the frame says what is going on (owner, 5 October 2026,
 * ACC-364: no line of refusal over an open field). Until the details arrive
 * the field stands, so a participant does not see it vanish and come back.
 */
const closedToMe = computed(() => {
  const ev = liveEvent.value;
  if (!ev || ev.isOpen || !user.value) return false;
  const details = eventDetails.value[ev.id];
  if (!details) return false;
  return !details.participants?.some(
    (p) => p.user.username === user.value?.username,
  );
});

const canWrite = computed(() => Boolean(user.value) && !closedToMe.value);

// The store's answers to what the reader does in the feed.
const feedActions: ChatFeedActions = {
  send: (text) => globalChatStore.sendMessage(text),
  remove: (id) => globalChatStore.deleteMessage(id),
  like: (id) => globalChatStore.likeMessage(id),
  unlike: (id) => globalChatStore.unlikeMessage(id),
  // Through the envelope: read off the top level the text was always
  // undefined, and the editor opened empty over a message that has text.
  editText: async (id) => {
    const { data } = await globalChatApi.getMessageForEdit(id);
    return htmlToBbcode(unwrapResource<GlobalChatMessage>(data)?.text || "");
  },
  saveEdit: (id, text) => globalChatStore.updateMessage(id, text),
};

const route = useRoute();

/** A message of the global chat is found by its anchor on this page. */
function linkOf(id: string): string {
  return `${permalinkOrigin()}${window.location.pathname}#msg-${id}`;
}

/**
 * The link over a message in the feed: this page at the message's anchor. The
 * day the feed stands on stays in the address, so following the link keeps
 * the window the reader is in.
 */
function linkTo(id: string): RouteLocationRaw {
  return { name: "global-chat", query: route.query, hash: `#msg-${id}` };
}

// ─────────────────────────────────────────────────────────────
// Which part of the history the feed shows
// ─────────────────────────────────────────────────────────────
// LIVE or ARCHIVE by the ?date of the address, the line explaining a window
// the reader did not ask for, the landing on a message or a day: the one logic
// both feeds share (useChatHistoryWindow). In LIVE mode new messages (the
// SignalR push, the poll fallback) append to the tail, and the view follows
// them only for a reader at the bottom; the button back to the latest
// messages is there once the reader went a whole viewport up.
const history = useChatHistoryWindow({
  feed,
  location: () => ({ name: "global-chat" }),
  messages: () => messages.value ?? [],
  loading: () => loading.value,
  failed: () => Boolean(error.value),
  hasMoreAfter: () => hasMoreAfter.value,
  landedOnRequestedDate: () => globalChatStore.landedOnRequestedDate,
  load: {
    latest: () => globalChatStore.jumpToLatest(),
    date: (date) => globalChatStore.navigateToDate(date),
    message: (id) => globalChatStore.navigateToMessage(id),
  },
});
const {
  selectedDate,
  notice: feedNotice,
  today,
  hasHistory,
  pickDate,
  landOnMessage,
  jumpToLatest,
  followToLive,
} = history;

// Where the reader stands is the feed's to measure (ChatFeed.atBottom), and it
// is measured when it is asked: appending a page grows the list without a
// scroll event, so a remembered answer goes stale exactly when it matters.
function atBottom(): boolean {
  return feed.value?.atBottom() ?? true;
}

// ─────────────────────────────────────────────────────────────
// Realtime updates
// ─────────────────────────────────────────────────────────────
// True when the currently loaded window is the tail of the chat (no archive
// date selected and no newer page to fetch) — the only state where a
// pushed/polled new message should be appended live instead of silently
// dropped (store.addMessage already no-ops otherwise).
const isAtLatest = computed(() => !selectedDate.value && !hasMoreAfter.value);

// SignalR push: the backend broadcasts NotificationType.NewGlobalChatMessage to
// every open connection — guests included (the hub accepts anonymous
// connections as receive-only broadcast listeners). The payload
// intentionally omits message text (BBCode rendering stays server-side), so
// on receipt we just fetch/append via the store's existing dedupe-by-id
// path, same as the polling fallback below.
//
// The page holds its own lease on the shared connection: for guests it is
// the sole owner (created on mount, released on unmount); for authenticated
// users connect() reuses the App.vue-owned socket and releasing the page
// lease on unmount leaves that socket untouched.
const {
  connect: connectChatSignalR,
  disconnect: disconnectChatSignalR,
  onNotification: onGlobalNotification,
  isConnected: isSignalRConnected,
} = useGlobalSignalR("global-chat-page");

/** Which message the feed currently ends on, or null when it holds none. */
function tailMessageId(): string | null {
  const held = messages.value;
  if (!held || held.length === 0) return null;
  return held[held.length - 1].id;
}

async function fetchNewMessages() {
  if (!isAtLatest.value) return;
  const tailBefore = tailMessageId();
  // Measured BEFORE the fetch: appending grows the list without a scroll
  // event, so afterwards the reader would no longer read as at the bottom.
  // Follow the tail only if they were there — readers who scrolled up must
  // not be yanked down (state machine, LIVE).
  const wasAtBottom = atBottom();
  // The same measurement decides both things: whether to follow the tail here,
  // and whether the store may replace a window the reader has drifted away from.
  await globalChatStore.pollForNewer(wasAtBottom);
  // The last message, not the count: when more than a page went by unseen the
  // store drops the drifted-off window and shows the latest one instead, and
  // that window holds a full page just as the replaced one often did. Counting
  // reads 50 against 50 and calls the feed unmoved, while every line on screen
  // has in fact been replaced by a later one, leaving a reader who was at the
  // tail somewhere in the middle of a conversation he has not read.
  if (tailMessageId() !== tailBefore && wasAtBottom) {
    scrollToBottom();
  }
}

function handleGlobalChatNotification(notification: SignalRNotification) {
  if (notification.eventType === NotificationType.NewGlobalChatMessage) {
    fetchNewMessages();
    return;
  }
  // The events strip rides the same broadcast: an event starting or ending
  // changes what it shows, and the store decides which pushes those are.
  globalChatStore.refreshEventsOnNotification(notification.eventType);
}

let unsubscribeSignalR: (() => void) | null = null;

// Polling is demoted to a fallback transport: the tick is a no-op while the
// SignalR socket is delivering pushes and only performs fetches when the
// socket is down — and even then only while at the tail of the chat (avoids
// competing with archive/history browsing) and when the tab is visible. The
// interval is the shared one from the API client, so every live list on the
// site ticks at the same pace.
let pollTimer: ReturnType<typeof setInterval> | null = null;

async function pollForNewMessages() {
  if (document.hidden) return;
  if (isSignalRConnected.value) return;
  await fetchNewMessages();
}

/**
 * The tab coming back to the screen.
 *
 * Read again in every case, live socket included: the hub keeps no queue and
 * replays nothing, a socket a background tab kept open still reports itself
 * connected after the browser throttled what it delivered, and the API
 * contract asks for a re-read on return outright (API_DESIGN, Realtime). This
 * is why the handler is its own function rather than the timer tick: the tick
 * steps aside for a live socket on purpose, and it used to be what the return
 * of the tab ran, so the one moment the reader looks at the page again was the
 * one moment nothing was checked.
 */
function handleTabVisible() {
  if (document.hidden) return;
  fetchNewMessages();
  globalChatStore.fetchEvents();
}

// Socket state transitions: catch up immediately instead of waiting for the
// next 30s tick — on disconnect (messages may arrive while the socket is
// down) and on (re)connect (messages may have been missed while it was down).
//
// The events strip is caught up on a fresh connection too. It is read once, on
// mount, and afterwards only a push about an event starting or ending moves it
// — which is exactly what a broken socket does not deliver, so the strip came
// back from a reconnect still describing the chat as it stood at page load.
// Only on connect: while the socket is down there is nothing to read it from,
// and going down cannot itself have changed the list.
// Unless the list has only just been read, which the store answers: the first
// connection normally lands a moment after mount, which read it already, and
// the two requests would answer the same thing. Skipping the first connection
// outright is not the same rule: a connection that arrives after a long series
// of retries is far outside that window, and that is exactly the case the
// re-read exists for.
watch(isSignalRConnected, (connected) => {
  fetchNewMessages();
  if (!connected) return;
  globalChatStore.fetchEventsIfFresh();
});

function startPolling() {
  if (pollTimer) return;
  pollTimer = setInterval(pollForNewMessages, LIVE_TAIL_POLL_INTERVAL_MS);
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}

onMounted(async () => {
  // Chat events (topbar banner + the closed-event rule) — independent of the
  // message load, fire and forget.
  globalChatStore.fetchEvents();

  // The first window: the message a link names, the day the address names,
  // or the latest messages.
  const hashMsgId = getHashMessageId();
  const dateQuery = history.dateInRoute();
  if (hashMsgId) {
    await history.landOnMessage(hashMsgId);
  } else if (dateQuery) {
    await history.loadDate(dateQuery);
  } else {
    await globalChatStore.fetchMessages();
    scrollToBottom();
    nextTick(() => feed.value?.setupInfiniteScroll());
  }

  // Realtime: everyone — guests included — gets the SignalR push. The page
  // acquires its own lease on the shared connection (reused if App.vue
  // already connected it for an authenticated user, created anonymously
  // otherwise). Polling runs alongside purely as the fallback while the
  // socket is not connected — see pollForNewMessages.
  unsubscribeSignalR = onGlobalNotification(handleGlobalChatNotification);
  connectChatSignalR();
  startPolling();
  document.addEventListener("visibilitychange", handleTabVisible);
});

onUnmounted(() => {
  stopPolling();
  unsubscribeSignalR?.();
  // Releases only this page's lease: closes the socket when the page was
  // its sole owner (guest), leaves the App.vue-owned one running otherwise.
  disconnectChatSignalR();
  document.removeEventListener("visibilitychange", handleTabVisible);
});

// A message the store was asked to land on (a link, a search hit): the feed
// brings it into view and marks its arrival.
watch(highlightedMessageId, (newId) => {
  if (!newId) return;
  nextTick(() => {
    feed.value?.scrollToMessage(newId);
    globalChatStore.clearHighlight();
  });
});

// The link of a message followed on this page: the address takes the anchor
// and nothing else changes, so the feed lands on the message here, as it does
// on the way in.
watch(
  () => route.hash,
  (hash) => {
    const id = messageIdOf(hash);
    if (id) void landOnMessage(id);
  },
);

// --- Moderator warning (doc 4.2.4.1 / 4.2.2.4) ---
// Global-chat messages are public, so moderators get an "Оставить
// предупреждение" action in the hover toolbar, under the messages of a rank
// below their own and never an administrator's (canWarn, the server's rank
// rule). The dialog is prefilled with the message author and a #msg-{id}
// permalink; entityType "Message" lets the backend link the warning to the
// offending message.
const warnUsername = ref("");
const warnEntityId = ref<string | undefined>(undefined);
const warnEntityLink = ref<string | undefined>(undefined);

const { open: openWarnDialog, close: closeWarnDialog } = useModal({
  component: WarningDialog,
  attrs: reactive({
    username: warnUsername,
    entityId: warnEntityId,
    entityType: "Message",
    entityLink: warnEntityLink,
    onSuccess: () => closeWarnDialog(),
    onCancel: () => closeWarnDialog(),
  }),
});

/** A warning reaches only a rank below the reader's, never an administrator. */
function canWarn(msg: GlobalChatMessage): boolean {
  return reachesByRank(user.value, msg.author);
}

function handleWarn(msg: GlobalChatMessage) {
  const username = msg.author?.username;
  if (!username) return;
  warnUsername.value = username;
  warnEntityId.value = msg.id;
  warnEntityLink.value = linkOf(msg.id);
  openWarnDialog();
}

function getHashMessageId(): string | null {
  return messageIdOf(window.location.hash);
}

/** The message an anchor of the address names (#msg-<id>), if any. */
function messageIdOf(hash: string | undefined): string | null {
  if (hash && hash.startsWith("#msg-")) {
    return hash.slice(5);
  }
  return null;
}

function scrollToBottom() {
  nextTick(() => feed.value?.scrollToBottom());
}

// Retry after a load error: the load of the window the address names.
async function retryLoad() {
  const dateQuery = history.dateInRoute();
  if (dateQuery) {
    await history.loadDate(dateQuery);
    return;
  }
  await globalChatStore.fetchMessages();
  scrollToBottom();
  nextTick(() => feed.value?.setupInfiniteScroll());
}
</script>

<template>
  <page-title v-once>Глобальный чат</page-title>

  <!-- The row above the frame, the same as over a correspondence: the
       search of the global chat and "Перейти к дате". Not over a chat with
       nothing in it. -->
  <ChatSearchRow
    v-if="hasHistory"
    scope="global"
    :selected-date="selectedDate"
    :max-date="today"
    @jump="landOnMessage"
    @date-picked="pickDate"
  />

  <ChatFeed
    ref="feed"
    :messages="messages ?? []"
    :loading="loading"
    :error="error"
    :retry="retryLoad"
    :empty-text="
      user ? 'Сообщений пока нет. Начните общение!' : 'Сообщений пока нет'
    "
    :notice="feedNotice"
    label="Сообщения чата"
    context="message"
    :has-more-before="hasMoreBefore"
    :has-more-after="hasMoreAfter"
    :error-before="errorBefore"
    :error-after="errorAfter"
    :load-older="() => globalChatStore.fetchMoreBefore()"
    :load-newer="() => globalChatStore.fetchMoreAfter()"
    :actions="feedActions"
    :sending="sending"
    :can-write="canWrite"
    :draft-key="composerDraftKey('global-chat', 'message')"
    :link-to="linkTo"
    :can-warn="canWarn"
    @jump-to-latest="jumpToLatest"
    @scroll="followToLive"
    @warn="handleWarn"
  >
    <!-- The events of the chat: the focal event line and its floating
         layers, at the top of the frame. -->
    <template #strip><ChatEventsPanel /></template>
    <!-- A guest is invited to sign in where the composer would be; a reader
         outside a closed event has nothing there, the event's line says why. -->
    <template #closed>
      <LoginPrompt v-if="!user" action="отправлять сообщения" />
    </template>
  </ChatFeed>
</template>
