<script setup lang="ts">
/**
 * ChatView: a private correspondence, drawn as the global chat is: the feed
 * of widgets/chat-feed with its composer under the frame, the search row and
 * "Перейти к дате", the history window of useChatHistoryWindow.
 *
 * The line at the top of the frame says who the correspondence is with
 * (ConversationLine): his name and whether he is on the site, "поиск", which
 * opens the search row under the line, and "…" with the menu of the
 * correspondence. The clearing of the history and the leaving of a group ask
 * first, in the site's window of a question.
 */
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter, type RouteLocationRaw } from "vue-router";
import { storeToRefs } from "pinia";
import { useMessagingStore } from "@/entities/message";
import { useAuthStore } from "@/entities/user";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import {
  ChatFeed,
  ChatSearchRow,
  useChatHistoryWindow,
  type ChatFeedActions,
} from "@/widgets/chat-feed";
import type { ChatId, Message, MessageId } from "@/entities/message";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { messagingApi } from "@/entities/message";
import { unwrapResource } from "@/shared/api";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatSiteTime } from "@/shared/lib/utils/datetime";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import {
  NotificationType,
  type SignalRNotification,
} from "@/shared/api/models/notifications";
import { usePaging } from "@/shared/lib/composables/usePaging";
import ConversationLine from "./ConversationLine.vue";
import { listPageQuery } from "./listPage";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { user: currentUser } = storeToRefs(useAuthStore());
const {
  selectedChat,
  loadingChat,
  chatMissing,
  chatError,
  messagesList,
  messagesError,
  sending,
  interlocutor,
  hasMoreBefore,
  hasMoreAfter,
  errorBefore,
  errorAfter,
  loadingMessages,
  highlightedMessageId,
  firstMessageUtc,
} = storeToRefs(messagingStore);
const { messagesPerPage } = usePaging();

// The interlocutor first: three messenger routes shared one tab name, and the
// name of the person is the only thing that tells two correspondences apart.
useDocumentTitle(() =>
  joinTitleSegments(
    selectedChat.value?.type === "Group"
      ? selectedChat.value.title || undefined
      : interlocutor.value?.username,
    "Личные сообщения",
  ),
);

/** The feed: the frame, the list, the toolbar and the composer (ChatFeed). */
const feed = ref<InstanceType<typeof ChatFeed> | null>(null);

// The field is the server's answer about the viewer, asked by the code that
// refuses the message: a participant, and not the one whom the other side of a
// conversation of two keeps on a blacklist with private messages closed. That
// one used to type a message and be told after it that it could not be sent.
// The ordinary ban does not reach private correspondence, and the answer says
// so too. It is the answer about the correspondence on screen, so none is
// offered while another one is on its way in its place.
const canWrite = computed(
  () =>
    Boolean(currentUser.value) &&
    !loadingChat.value &&
    selectedChat.value?.viewer?.canWrite === true,
);

/**
 * The feed has nothing to show yet: the correspondence or its first window is
 * on its way. The skeleton of the global chat stands there, never "Переписка
 * не найдена" or "Сообщений пока нет", which are answers and not waiting.
 */
const feedLoading = computed(
  () =>
    loadingChat.value || (loadingMessages.value && !messagesList.value.length),
);

// The store's answers to what the reader does in the feed.
const feedActions: ChatFeedActions = {
  send: (text) =>
    selectedChat.value
      ? messagingStore.sendMessage(selectedChat.value.id, text)
      : Promise.resolve({ error: null }),
  remove: (id) => messagingStore.deleteMessage(id),
  like: (id) => messagingStore.likeMessage(id),
  unlike: (id) => messagingStore.unlikeMessage(id),
  // Through the envelope: read off the top level the text was always
  // undefined, and the editor opened empty over a message that has text.
  editText: async (id) => {
    const { data } = await messagingApi.getMessageForEdit(id as MessageId);
    return htmlToBbcode(unwrapResource<Message>(data)?.text || "");
  },
  saveEdit: (id, text) => messagingStore.updateMessage(id, text),
};

/**
 * A message of a correspondence opens the correspondence on it, the list
 * beside it on the page it stands at (listPage.ts).
 */
function linkTo(id: string): RouteLocationRaw {
  return {
    name: "chat",
    params: { id: String(route.params.id) },
    query: { ...listPageQuery(route.query), msg: id },
  };
}

/** Where the reader stands, measured by the feed when asked. */
function isAtTail(): boolean {
  return feed.value?.atBottom() ?? true;
}

function scrollToBottom() {
  nextTick(() => feed.value?.scrollToBottom());
}

/** The correspondence the address names. */
const chatId = () => String(route.params.id) as ChatId;

// ─────────────────────────────────────────────────────────────
// Which part of the history the feed shows
// ─────────────────────────────────────────────────────────────
// The global chat's logic, the one both feeds share (useChatHistoryWindow):
// the day in the address (?date), the way back to the latest messages and the
// catch-up at the bottom of an archive window, the line over a window the
// reader did not ask for. A message link of a correspondence is ?msg. The
// page of the list beside the feed rides along with ?date.
const history = useChatHistoryWindow({
  feed,
  location: () => ({
    name: "chat",
    params: { id: chatId() },
    query: listPageQuery(route.query),
  }),
  chat: chatId,
  messages: () => messagesList.value,
  loading: () => feedLoading.value,
  failed: () => Boolean(messagesError.value),
  hasMoreAfter: () => hasMoreAfter.value,
  landedOnRequestedDate: () => messagingStore.landedOnRequestedDate,
  load: {
    latest: () => messagingStore.jumpToLatest(),
    date: (date) => messagingStore.navigateToDate(chatId(), date),
    message: (id) =>
      messagingStore.navigateToMessage(chatId(), id as MessageId),
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

/** The first day of the correspondence, the first the calendar offers. */
const firstDay = computed(() =>
  firstMessageUtc.value
    ? formatSiteTime(firstMessageUtc.value, "YYYY-MM-DD")
    : "",
);

// ─────────────────────────────────────────────────────────────
// The search under the line
// ─────────────────────────────────────────────────────────────
// "поиск" of the line opens the row of the global chat under it: the search of
// this correspondence and "Перейти к дате". The row is there to be typed in,
// so it opens with the caret in its field. Ctrl+F opens it the same way; once
// it is open, the row takes the key itself (ChatSearchRow).
const searchOpen = ref(false);
const searchRow = ref<InstanceType<typeof ChatSearchRow> | null>(null);

async function openSearch() {
  searchOpen.value = true;
  await nextTick();
  searchRow.value?.focus();
}

function toggleSearch() {
  if (searchOpen.value) searchOpen.value = false;
  else void openSearch();
}

function openSearchOnFind(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== "f") {
    return;
  }
  if (searchOpen.value || !hasHistory.value || !currentUser.value) return;
  event.preventDefault();
  void openSearch();
}

// A correspondence emptied for the reader has nothing to search.
watch(hasHistory, (has) => {
  if (!has) searchOpen.value = false;
});

/** The correspondence as the line describes it: none while one is on its way. */
const lineChat = computed(() =>
  loadingChat.value ? null : selectedChat.value,
);

// ─────────────────────────────────────────────────────────────
// The menu's two questions
// ─────────────────────────────────────────────────────────────
const isGroup = computed(() => selectedChat.value?.type === "Group");
const askingClear = ref(false);
const askingLeave = ref(false);

/** What the clearing does, said before it is done. */
const clearMessage = computed(() =>
  isGroup.value
    ? "Сообщения, написанные до этой минуты, перестанут показываться вам, а у других участников останутся"
    : "Сообщения, написанные до этой минуты, перестанут показываться вам, а у собеседника останутся",
);

// The windows run the requests themselves and print a refusal in place, so
// the requests say they own it.
const OWN_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

function clearHistory() {
  return messagingStore.clearHistory(chatId(), OWN_REFUSAL);
}

function leaveGroup() {
  return messagingStore.leaveChat(chatId(), OWN_REFUSAL);
}

/** Out of a group the reader has left: back to the list. */
function afterLeave() {
  router.push({ name: "messenger" });
}

/**
 * Opens the correspondence the address names, on the window the address asks
 * for: a message (?msg, a link or a search elsewhere), a day (?date), or the
 * latest messages, which are then read.
 */
async function loadChat() {
  const id = chatId();
  history.forget();
  searchOpen.value = false;
  await messagingStore.selectChat(id);
  if (!selectedChat.value) return;
  const jumpMsgId =
    typeof route.query.msg === "string" ? route.query.msg : null;
  const date = history.dateInRoute();
  if (jumpMsgId || date) {
    if (jumpMsgId) await landOnMessage(jumpMsgId);
    else if (date) await history.loadDate(date);
    // The reader opened the correspondence, at whichever place of it.
    if (!messagesError.value) await messagingStore.markAsRead(id);
    return;
  }
  await messagingStore.fetchMessages(id);
  // A window that did not come is not a window read.
  if (messagesError.value) return;
  await messagingStore.markAsRead(id);
  scrollToBottom();
  nextTick(() => feed.value?.setupInfiniteScroll());
}

/** The retry of a window that did not load. */
async function retryMessages() {
  const id = selectedChat.value?.id;
  if (!id) return;
  const date = history.dateInRoute();
  if (date) {
    await history.loadDate(date);
    return;
  }
  await messagingStore.fetchMessages(id);
  if (messagesError.value) return;
  await messagingStore.markAsRead(id);
  scrollToBottom();
  nextTick(() => feed.value?.setupInfiniteScroll());
}

/** The feed scrolled: an archive window caught up, a replacement it held up. */
function onFeedScroll() {
  followToLive();
  readDeferredWindow();
}

// A message the store was asked to land on (?msg=): the feed brings it into
// view and marks its arrival.
watch(highlightedMessageId, (id) => {
  if (!id) return;
  nextTick(() => {
    feed.value?.scrollToMessage(id);
    messagingStore.clearHighlight();
  });
});

// Another correspondence: opened whole, on the window its address asks for.
// Another message of the same one (?msg moved, a link followed): landed on.
// ?msg leaving the address, when a day is picked or the archive is left,
// moves nothing; the day is the history window's to follow.
watch(
  () => [route.params.id, route.query.msg] as const,
  ([id, msg], previous) => {
    if (!previous || id !== previous[0]) {
      loadChat();
      return;
    }
    if (typeof msg === "string" && msg && msg !== previous[1]) {
      landOnMessage(msg);
    }
  },
  { immediate: true },
);

// ─────────────────────────────────────────────────────────────
// Live tail of the open correspondence
// ─────────────────────────────────────────────────────────────
// A push is a hint and the truth is in REST (API_DESIGN.md, "Realtime"): the
// hub queues nothing, its payload carries no text, and the same event may
// arrive twice or out of order. So every trigger below ends in one and the
// same tail read, deduplicated by id: a repeated push then costs a request
// and changes nothing on screen.
//
// No lease is taken on the shared socket: a private correspondence exists only
// for a signed-in reader, and App.vue holds the connection for as long as they
// are signed in. This page only listens on it.
const { onNotification: onGlobalNotification, isConnected: isSocketConnected } =
  useGlobalSignalR();

let unsubscribeSignalR: (() => void) | null = null;
let readingTail = false;
// A trigger that arrived while the reader's own message was on its way out.
// The send ends in a tail of its own, so the read waits for it instead of
// racing it, and is spent once when it lands.
let missedTailRead = false;
// New messages sit in the feed, and the server has not been told they were
// read. Kept when the tab is hidden: nobody has looked at them yet, so the
// receipt travels with the reader and is sent when the tab comes back.
let pendingReadReceipt = false;
// More than a page went by while the reader was up in the history, so the
// window has to be re-read whole — and it is not, while he is reading it.
// Spent when he comes back down to the tail.
let deferredWindowRead = false;

// The unread badge in the header and the open correspondence answer the same
// push: App.vue re-reads the counter while this page appends the message, and
// a badge over a message the reader is looking at contradicts the screen. A
// message on screen in a visible tab is read, so the page says so.
//
// `atTail` is measured by the caller, before the list grew: appending to a list
// moves its bottom away from a reader standing at it, so a measurement taken
// afterwards answers "no" for everybody. A reader parked higher up has the new
// message on the page and below the fold, and a receipt sent for him takes away
// the one sign that anything arrived, here and in the header count both.
async function markTailRead(atTail: boolean) {
  const chatId = selectedChat.value?.id;
  if (!pendingReadReceipt || !chatId) return;
  if (document.visibilityState !== "visible") return;
  if (!atTail) return;
  await messagingStore.markAsRead(chatId);
  pendingReadReceipt = false;
}

async function readTail() {
  const chatId = selectedChat.value?.id;
  if (!chatId) return;
  // A reader parked in the middle of the history, landed there from search or
  // paged up far enough for the window to be trimmed, is not at the tail.
  // Appending the newest page under an older window would leave a hole between
  // the two, and nobody asked to be taken to the end of the correspondence.
  if (hasMoreAfter.value) return;
  // A send in flight brings the newest message with it: the store appends what
  // the POST answered with. A read racing that append can carry the same
  // message in ahead of it and put it on screen twice, so the trigger is
  // remembered and spent when the send has landed.
  if (sending.value) {
    missedTailRead = true;
    return;
  }
  // The window is being replaced right now (chat switch, jump to a message):
  // that load brings the newest page with it.
  if (loadingMessages.value || readingTail) return;
  readingTail = true;
  try {
    const { data } = await messagingApi.getMessages(chatId, {
      limit: messagesPerPage.value,
    });
    const tail = data?.resources ?? [];
    // The window may have been switched, re-read or left behind while the
    // answer was in flight, and this tail was deduplicated against the
    // previous one. hasMoreAfter is asked again for the same reason as before
    // the request: a jump to a message or a trimmed window puts the reader off
    // the tail, and this page would take away what they are reading.
    if (
      !tail.length ||
      selectedChat.value?.id !== chatId ||
      loadingMessages.value ||
      hasMoreAfter.value
    ) {
      return;
    }
    const shown = new Set(messagesList.value.map((message) => message.id));
    const fresh = tail.filter((message) => !shown.has(message.id));
    // Measured before the list grows, and one measurement serves both of the
    // things that follow from it: the feed goes down to the new message for a
    // reader who was at the end of it, and the receipt goes out only for a
    // reader the message has actually reached. Split, the two disagreed: the
    // message was appended below the fold and reported read all the same.
    const wasAtTail = isAtTail();
    if (messagesList.value.length > 0 && fresh.length === tail.length) {
      // Not one message of the newest page is on screen: more went by than a
      // page holds, so appending it would leave a gap between what is shown
      // and what arrived. The window is re-read whole instead.
      if (!wasAtTail) {
        // Except under a reader who has paged up: the window he is reading is
        // the page he is reading, and replacing it takes that page away and
        // leaves him at the end of a correspondence he has not read. The
        // replacement waits for his return to the tail: readDeferredWindow
        // reads again when he gets there, and that read makes it.
        deferredWindowRead = true;
        return;
      }
      await messagingStore.fetchMessages(chatId);
      pendingReadReceipt = true;
      scrollToBottom();
      await markTailRead(true);
      return;
    }
    // The part both sides hold is taken in the server's order rather than left
    // as it stands. A message of the reader's own goes to the end of the list
    // the moment the POST answers, so a message written before it by the other
    // side sat above it here and below it there. Rewriting that part also
    // picks up edits made to the messages it covers; the older prefix, which
    // the tail does not reach, stays where it is.
    const tailIds = new Set(tail.map((message) => message.id));
    messagesList.value = [
      ...messagesList.value.filter((message) => !tailIds.has(message.id)),
      ...tail,
    ];
    if (fresh.length > 0) {
      pendingReadReceipt = true;
      if (wasAtTail) scrollToBottom();
      await markTailRead(wasAtTail);
    }
  } catch {
    // Silent: the next push, reconnect or return to the tab reads again.
  } finally {
    readingTail = false;
  }
}

/**
 * The reader coming back down to the end of the correspondence: the feed
 * scrolled. A window replacement his position stood in the way of is made
 * here, and the tail is read again rather than the answer kept from then:
 * what the earlier read found is as old as the moment he started scrolling
 * back.
 */
function readDeferredWindow() {
  if (!deferredWindowRead || !isAtTail()) return;
  deferredWindowRead = false;
  readTail();
}

function handleMessagingNotification(notification: SignalRNotification) {
  if (notification.eventType !== NotificationType.NewMessage) return;
  const chatId = selectedChat.value?.id;
  // Every correspondence of the reader pushes this same event, and the chat in
  // the payload is the only thing that tells one from another.
  const pushedChatId = notification.payload?.chatId as string | undefined;
  if (!chatId || pushedChatId !== chatId) return;
  readTail();
}

async function handleVisibilityChange() {
  // Events that happened while the tab was in the background are not replayed
  // to it, so coming back to the tab starts with a read.
  if (document.visibilityState !== "visible") return;
  await readTail();
  // Whatever was appended while the tab was hidden is read now: the reader is
  // looking at it, and this read may well have found nothing left to append.
  // Where he is looking still decides it, measured here because nothing has
  // been appended since: a reader who left the tab parked in the middle of the
  // history comes back to the same place, not to a cleared unread mark.
  await markTailRead(isAtTail());
}

// Nothing is delivered while the socket is down and nothing is repeated once
// it is back up, so a fresh connection starts by reading what it missed.
watch(isSocketConnected, (connected) => {
  if (connected) readTail();
});

// The read a send stood in the way of, spent once the created message is in
// the list. Waiting for that is what keeps the same message off the screen
// twice; dropping the trigger instead would lose the other side's message
// that arrived while the reader was writing.
watch(sending, (isSending) => {
  if (isSending || !missedTailRead) return;
  missedTailRead = false;
  readTail();
});

onMounted(() => {
  unsubscribeSignalR = onGlobalNotification(handleMessagingNotification);
  document.addEventListener("visibilitychange", handleVisibilityChange);
  document.addEventListener("keydown", openSearchOnFind);
});

onUnmounted(() => {
  messagingStore.clearSelection();
  unsubscribeSignalR?.();
  unsubscribeSignalR = null;
  document.removeEventListener("visibilitychange", handleVisibilityChange);
  document.removeEventListener("keydown", openSearchOnFind);
});
</script>

<template>
  <div class="chat-view">
    <!-- The server's answer, and only that: a correspondence on its way or
         one the network failed to bring is not a missing one. -->
    <div v-if="chatMissing" class="chat-view__missing">
      <secondary-text>Переписка не найдена</secondary-text>
      <router-link
        :to="{ name: 'messenger', query: listPageQuery(route.query) }"
        >Все переписки</router-link
      >
    </div>
    <ErrorState v-else-if="chatError" :message="chatError" :retry="loadChat" />

    <template v-else>
      <ChatFeed
        ref="feed"
        :messages="messagesList"
        :loading="feedLoading"
        :error="messagesError"
        :retry="retryMessages"
        empty-text="Сообщений пока нет"
        :notice="feedNotice"
        label="Сообщения переписки"
        context="direct"
        :has-more-before="hasMoreBefore"
        :has-more-after="hasMoreAfter"
        :error-before="errorBefore"
        :error-after="errorAfter"
        :load-older="() => messagingStore.fetchMoreBefore()"
        :load-newer="() => messagingStore.fetchMoreAfter()"
        :actions="feedActions"
        :sending="sending"
        :can-write="canWrite"
        :composer-disabled="!selectedChat"
        :draft-key="composerDraftKey('chat', 'message', selectedChat?.id)"
        :link-to="linkTo"
        @jump-to-latest="jumpToLatest"
        @scroll="onFeedScroll"
      >
        <!-- The top of the frame, where the global chat has its events: who
             the correspondence is with, "поиск" and "…"; the search row
             under it while it is open. -->
        <template #strip>
          <ConversationLine
            :chat="lineChat"
            :interlocutor="interlocutor"
            :search-open="searchOpen"
            :can-search="hasHistory"
            @toggle-search="toggleSearch"
            @clear-history="askingClear = true"
            @leave="askingLeave = true"
          />
          <div
            v-if="searchOpen"
            id="conversation-search"
            class="conversation-search"
          >
            <ChatSearchRow
              ref="searchRow"
              :scope="`dm:${chatId()}`"
              :selected-date="selectedDate"
              :min-date="firstDay"
              :max-date="today"
              @jump="landOnMessage"
              @date-picked="pickDate"
              @date-open="messagingStore.fetchFirstMessage(chatId())"
            />
          </div>
        </template>
      </ChatFeed>

      <ConfirmDialog
        v-model:show="askingClear"
        title="Очистить историю?"
        :message="clearMessage"
        confirm-label="Очистить"
        failure="Не удалось очистить историю"
        :action="clearHistory"
      />
      <ConfirmDialog
        v-model:show="askingLeave"
        title="Покинуть группу?"
        message="Переписка уйдет из вашего списка, а у участников останется со всеми сообщениями"
        confirm-label="Покинуть"
        failure="Не удалось покинуть группу"
        :action="leaveGroup"
        @done="afterLeave"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.chat-view
  display: flex
  flex-direction: column
  min-width: 0

.chat-view__missing
  display: flex
  flex-direction: column
  gap: $small

// The search row under the line, in the line's dress: the surface of the
// strip and its dashed hand-over to the feed. The row keeps its own margin
// under it, so the padding is on three sides.
.conversation-search
  flex-shrink: 0
  min-width: 0
  padding: $small $medium 0
  background-color: $bg-element
  border-bottom: 1px dashed $border
</style>
