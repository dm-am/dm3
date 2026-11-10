<script setup lang="ts">
import { LIVE_TAIL_POLL_INTERVAL_MS } from "@/shared/lib/utils/liveTail";
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
  useGlobalChatEventsStore,
  useGlobalChatStore,
  type GlobalChatMessage,
} from "@/entities/global-chat";
import { useAuthStore } from "@/entities/user";
import { reachesByRank } from "@/entities/moderation";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
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
const eventsStore = useGlobalChatEventsStore();
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
} = storeToRefs(globalChatStore);
const { liveEvent, eventDetails } = storeToRefs(eventsStore);
const { user } = storeToRefs(useAuthStore());

const feed = ref<InstanceType<typeof ChatFeed> | null>(null);

// Who may write: a signed-in reader, and during a closed event its
// participants. The page reads the details of a live event once into the
// shared cache: the banner wants its duration and description, and the
// participants decide whether the composer stands (MessageService refuses the rest).
watch(
  liveEvent,
  (ev) => {
    if (ev) {
      eventsStore.fetchEventDetails(ev.id);
    }
  },
  { immediate: true },
);

/**
 * A closed event runs and the reader is not among its participants: no field,
 * the event's line says why. The field stands until the details arrive, so a
 * participant does not see it vanish and come back.
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

const feedActions: ChatFeedActions = {
  send: (text) => globalChatStore.sendMessage(text),
  remove: (id) => globalChatStore.deleteMessage(id),
  like: (id) => globalChatStore.likeMessage(id),
  unlike: (id) => globalChatStore.unlikeMessage(id),
  // Through the envelope: off the top level the text was undefined.
  editText: async (id) => {
    const { data } = await globalChatApi.getMessageForEdit(id);
    return unwrapResource<GlobalChatMessage>(data)?.text || "";
  },
  saveEdit: (id, text) => globalChatStore.updateMessage(id, text),
};

const route = useRoute();

function linkOf(id: string): string {
  return `${permalinkOrigin()}${window.location.pathname}#msg-${id}`;
}

/** This page at the message's anchor, the day of the feed kept in the address. */
function linkTo(id: string): RouteLocationRaw {
  return { name: "global-chat", query: route.query, hash: `#msg-${id}` };
}

// Which part of the history the feed shows (useChatHistoryWindow, shared with
// the correspondence): live or archive by ?date; in live mode new messages
// append and the view follows them only for a reader at the bottom.
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

// Asked when it is needed: appending grows the list without a scroll event.
function atBottom(): boolean {
  return feed.value?.atBottom() ?? true;
}

// The window is the chat's tail: the only state where a new message is appended live.
const isAtLatest = computed(() => !selectedDate.value && !hasMoreAfter.value);

// The hub broadcasts NewGlobalChatMessage to every connection, guests' included,
// without the text, so the page appends through the store's dedupe by id, as
// the poll does. The page holds its own lease on the shared connection.
const {
  connect: connectChatSignalR,
  disconnect: disconnectChatSignalR,
  onNotification: onGlobalNotification,
  isConnected: isSignalRConnected,
} = useGlobalSignalR("global-chat-page");

function tailMessageId(): string | null {
  const held = messages.value;
  if (!held || held.length === 0) return null;
  return held[held.length - 1].id;
}

async function fetchNewMessages() {
  if (!isAtLatest.value) return;
  const tailBefore = tailMessageId();
  // Before the fetch: afterwards the reader no longer reads as at the bottom,
  // and one who scrolled up must not be yanked down.
  const wasAtBottom = atBottom();
  // The same measurement lets the store replace a window the reader drifted from.
  await globalChatStore.pollForNewer(wasAtBottom);
  // The last message, not the count: the store may replace a drifted window with
  // the latest page, and the count would read 50 against 50.
  if (tailMessageId() !== tailBefore && wasAtBottom) {
    scrollToBottom();
  }
}

function handleGlobalChatNotification(notification: SignalRNotification) {
  if (notification.eventType === NotificationType.NewGlobalChatMessage) {
    fetchNewMessages();
    return;
  }
  // The events strip rides the same broadcast; the store decides which pushes move it.
  eventsStore.refreshEventsOnNotification(notification.eventType);
}

let unsubscribeSignalR: (() => void) | null = null;

// Polling is the fallback: a no-op while the socket delivers, and only at the
// tail with the tab visible; the interval is the API client's shared one.
let pollTimer: ReturnType<typeof setInterval> | null = null;

async function pollForNewMessages() {
  if (document.hidden) return;
  if (isSignalRConnected.value) return;
  await fetchNewMessages();
}

/**
 * The tab back on the screen reads again in every case, a live socket included:
 * the hub replays nothing and a throttled background socket still reports
 * itself connected (API_DESIGN, Realtime). Hence a handler of its own, not the
 * tick, which steps aside for a live socket.
 */
function handleTabVisible() {
  if (document.hidden) return;
  fetchNewMessages();
  eventsStore.fetchEvents();
}

// A socket change catches up at once: on disconnect and on (re)connect. The
// events strip is read again on a connection too, since it moves only by
// pushes a broken socket did not deliver, unless the store says it was just read.
watch(isSignalRConnected, (connected) => {
  fetchNewMessages();
  if (!connected) return;
  eventsStore.fetchEventsIfFresh();
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

/** The page is gone: the open, still on its way, starts nothing. */
let gone = false;

onMounted(async () => {
  // The events, independent of the message load.
  eventsStore.fetchEvents();

  // The first window: the linked message, the day of the address, or the latest.
  const hashMsgId = messageIdOf(window.location.hash);
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
  // Left while the first window was on its way: onUnmounted has run, and the
  // poll, the lease and the listener started now would stay behind it.
  if (gone) return;

  // Everyone gets the push, on this page's lease of the shared connection; the
  // poll runs only while the socket is not connected.
  unsubscribeSignalR = onGlobalNotification(handleGlobalChatNotification);
  connectChatSignalR();
  startPolling();
  document.addEventListener("visibilitychange", handleTabVisible);
});

onUnmounted(() => {
  gone = true;
  stopPolling();
  unsubscribeSignalR?.();
  // Releases only this page's lease.
  disconnectChatSignalR();
  document.removeEventListener("visibilitychange", handleTabVisible);
});

// The feed brings the message the store landed on into view and marks it.
watch(highlightedMessageId, (newId) => {
  if (!newId) return;
  nextTick(() => {
    feed.value?.scrollToMessage(newId);
    globalChatStore.clearHighlight();
  });
});

// A link followed on this page changes the anchor only, so the feed lands here.
watch(
  () => route.hash,
  (hash) => {
    const id = messageIdOf(hash);
    if (id) void landOnMessage(id);
  },
);

// Moderators warn from the hover toolbar under messages of a lower rank, never
// an administrator's (canWarn); the dialog carries the author and a #msg-{id} link.
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

// After a load error: the window the address names.
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

  <!-- The search and "Перейти к дате", as over a correspondence; not over an empty chat. -->
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
    empty-text="Сообщений пока нет"
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
    :moderation="{ canWarn }"
    @jump-to-latest="jumpToLatest"
    @scroll="followToLive"
    @warn="handleWarn"
  >
    <!-- The events of the chat, at the top of the frame. -->
    <template #strip><ChatEventsPanel /></template>
    <!-- A guest's sign-in in place of the composer; outside a closed event nothing, the event's line says why. -->
    <template #closed>
      <LoginPrompt v-if="!user" action="отправлять сообщения" />
    </template>
  </ChatFeed>
</template>
