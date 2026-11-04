<script setup lang="ts">
/**
 * A private correspondence, drawn as the global chat is (widgets/chat-feed,
 * useChatHistoryWindow), with ConversationLine at the top of the frame.
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
import { messagingApi } from "@/entities/message";
import { unwrapResource } from "@/shared/api";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatSiteTime } from "@/shared/lib/utils/datetime";
import ConversationLine from "./ConversationLine.vue";
import { listPageQuery } from "./listPage";
import { useChatLiveTail } from "./useChatLiveTail";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

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

// The interlocutor first: the person's name is what tells two correspondences' tabs apart.
useDocumentTitle(() =>
  joinTitleSegments(
    selectedChat.value?.type === "Group"
      ? selectedChat.value.title || undefined
      : interlocutor.value?.username,
    "Личные сообщения",
  ),
);

const feed = ref<InstanceType<typeof ChatFeed> | null>(null);

// The field is the server's answer about the viewer: a participant, and not
// one the other side keeps on a blacklist with private messages closed (the
// ordinary ban does not reach them). None while another correspondence loads.
const canWrite = computed(
  () =>
    Boolean(currentUser.value) &&
    !loadingChat.value &&
    selectedChat.value?.viewer?.canWrite === true,
);

/** The correspondence or its first window is on its way: the skeleton, never an answer. */
const feedLoading = computed(
  () =>
    loadingChat.value || (loadingMessages.value && !messagesList.value.length),
);

const feedActions: ChatFeedActions = {
  send: (text) =>
    selectedChat.value
      ? messagingStore.sendMessage(selectedChat.value.id, text)
      : Promise.resolve({ error: null }),
  remove: (id) => messagingStore.deleteMessage(id),
  like: (id) => messagingStore.likeMessage(id),
  unlike: (id) => messagingStore.unlikeMessage(id),
  // Through the envelope: off the top level the text was undefined.
  editText: async (id) => {
    const { data } = await messagingApi.getMessageForEdit(id as MessageId);
    return unwrapResource<Message>(data)?.text || "";
  },
  saveEdit: (id, text) => messagingStore.updateMessage(id, text),
};

/** A message opens its correspondence, the list kept on its page (listPage.ts). */
function linkTo(id: string): RouteLocationRaw {
  return {
    name: "chat",
    params: { id: String(route.params.id) },
    query: { ...listPageQuery(route.query), msg: id },
  };
}

function scrollToBottom() {
  nextTick(() => feed.value?.scrollToBottom());
}

const chatId = () => String(route.params.id) as ChatId;

// The global chat's history window (useChatHistoryWindow); a message link here is ?msg,
// and the list's page rides along with ?date.
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

const firstDay = computed(() =>
  firstMessageUtc.value
    ? formatSiteTime(firstMessageUtc.value, "YYYY-MM-DD")
    : "",
);

// "поиск" or Ctrl+F opens the search row under the line with the caret in it;
// once open, the row takes the key itself (ChatSearchRow).
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

const lineChat = computed(() =>
  loadingChat.value ? null : selectedChat.value,
);

// The menu's two questions
const isGroup = computed(() => selectedChat.value?.type === "Group");
const askingClear = ref(false);
const askingLeave = ref(false);

/** What the clearing does, said before it is done. */
const clearMessage = computed(() =>
  isGroup.value
    ? "Сообщения, написанные до этой минуты, перестанут показываться вам, а у других участников останутся"
    : "Сообщения, написанные до этой минуты, перестанут показываться вам, а у собеседника останутся",
);

function clearHistory() {
  return messagingStore.clearHistory(chatId(), OWNS_REFUSAL);
}

function leaveGroup() {
  return messagingStore.leaveChat(chatId(), OWNS_REFUSAL);
}

function afterLeave() {
  router.push({ name: "messenger" });
}

/** Opens the correspondence on the window the address asks for: ?msg, ?date or the latest. */
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
    if (!messagesError.value) await messagingStore.markAsRead(id);
    return;
  }
  await messagingStore.fetchMessages(id);
  if (messagesError.value) return;
  await messagingStore.markAsRead(id);
  scrollToBottom();
  nextTick(() => feed.value?.setupInfiniteScroll());
}

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

function onFeedScroll() {
  followToLive();
  readDeferredWindow();
}

// The feed brings the message the store landed on into view and marks it.
watch(highlightedMessageId, (id) => {
  if (!id) return;
  nextTick(() => {
    feed.value?.scrollToMessage(id);
    messagingStore.clearHighlight();
  });
});

// Another correspondence is opened whole; another message of the same one is
// landed on; ?msg leaving the address moves nothing.
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

// The live tail: new messages read in and the receipt for those seen (useChatLiveTail).
const { readDeferredWindow } = useChatLiveTail({
  isAtTail: () => feed.value?.atBottom() ?? true,
  scrollToBottom,
});

onMounted(() => {
  document.addEventListener("keydown", openSearchOnFind);
});

onUnmounted(() => {
  messagingStore.clearSelection();
  document.removeEventListener("keydown", openSearchOnFind);
});
</script>

<template>
  <div class="chat-view">
    <!-- The server's answer only: one on its way or failed by the network is not missing. -->
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
        <!-- The top of the frame: who it is with, "поиск" and "…", the search row under it. -->
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

// The search row in the line's dress; its own margin under it, the padding on three sides.
.conversation-search
  flex-shrink: 0
  min-width: 0
  padding: $small $medium 0
  background-color: $bg-element
  border-bottom: 1px dashed $border
</style>
