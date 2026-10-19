<script setup lang="ts">
/**
 * The list of conversations beside the open correspondence, the search of a
 * person on top. A new message reads the page of the list again.
 */
import { computed, onMounted, onUnmounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useRoute, useRouter } from "vue-router";
import { messagingApi, useMessagingStore, type Chat } from "@/entities/message";
import { UserAutocomplete, useAuthStore } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import ChatPreview from "./ChatPreview.vue";
import { listPageQuery } from "./listPage";
import Paging from "@/shared/ui/Paging/Paging.vue";
import type { User } from "@/shared/api/models/community";
import { EmptyState } from "@/shared/ui/EmptyState";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ChatPreviewSkeleton } from "@/shared/ui/Skeleton";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import {
  NotificationType,
  type SignalRNotification,
} from "@/shared/api/models/notifications";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { chats, loadingChats, error: chatsError } = storeToRefs(messagingStore);
const { user: currentUser } = storeToRefs(useAuthStore());

function extractPage(value: string | null | undefined): number {
  const num = parseInt(value ?? "1", 10);
  return isNaN(num) || num < 1 ? 1 : num;
}

// The shared Paging writes the page as ?number=.
function loadChats() {
  return messagingStore.fetchChats(
    extractPage(route.query.number as string | undefined),
  );
}

useFetchData(
  loadChats,
  [],
  [
    {
      query: (q) => q.number,
      callback: () => loadChats(),
    },
  ],
);

/**
 * Every conversation of the reader, read when the field is first opened: the
 * people and the groups it offers before a name is typed.
 */
const conversations = ref<Chat[]>([]);
let conversationsRead = false;

async function readConversations() {
  if (conversationsRead) return;
  conversationsRead = true;
  const { data } = await messagingApi.getChats({ take: MAX_API_PAGE_SIZE });
  if (data) conversations.value = data.resources;
  else conversationsRead = false;
}

// New mail: the page of the list read again, once for a burst of pushes.
const REREAD_DELAY_MS = 1000;
let rereadTimer: ReturnType<typeof setTimeout> | null = null;
let unsubscribe: (() => void) | null = null;
const { onNotification } = useGlobalSignalR();

function onMail(notification: SignalRNotification) {
  if (notification.eventType !== NotificationType.NewMessage) return;
  // A new correspondent is a new conversation: the field reads them again when next opened.
  conversationsRead = false;
  if (rereadTimer) clearTimeout(rereadTimer);
  rereadTimer = setTimeout(() => {
    rereadTimer = null;
    loadChats();
  }, REREAD_DELAY_MS);
}

onMounted(() => {
  unsubscribe = onNotification(onMail);
});

onUnmounted(() => {
  unsubscribe?.();
  if (rereadTimer) clearTimeout(rereadTimer);
});

function getInterlocutor(chat: { participants: User[] }) {
  return chat.participants.find(
    (p) => p.username !== currentUser.value?.username,
  );
}

// Paging scrolls back to the list, not to the page top.
const chatsListRef = ref<HTMLElement | null>(null);

// By name: the list under the field already keeps the order of the last message.
const offeredConversations = computed(() =>
  conversations.value
    .flatMap((chat) => {
      if (chat.type === "Group") {
        return [{ key: chat.id, label: chat.title || "Групповая переписка" }];
      }
      const person = chat.type === "Direct" ? getInterlocutor(chat) : undefined;
      return person
        ? [{ key: chat.id, label: person.username, picture: person.picture }]
        : [];
    })
    .sort((a, b) => a.label.localeCompare(b.label, "ru")),
);

const me = computed(() =>
  currentUser.value ? [currentUser.value.username] : [],
);

function openConversation(id: string) {
  router.push({
    name: "chat",
    params: { id },
    query: listPageQuery(route.query),
  });
}

function writeTo(username: string) {
  router.push({
    name: "direct-message",
    params: { username },
    query: listPageQuery(route.query),
  });
}
</script>

<template>
  <div class="messenger-list">
    <div class="search-section">
      <!-- A letter reaches anyone registered, so the quiet ones are offered too. -->
      <UserAutocomplete
        model-value=""
        placeholder="Поиск собеседника"
        :exclude="me"
        include-inactive
        :initial="offeredConversations"
        clear-on-select
        @focusin="readConversations"
        @update:model-value="writeTo"
        @pick="openConversation"
      />
    </div>

    <div v-if="chats?.resources.length" ref="chatsListRef" class="chats-list">
      <chat-preview
        v-for="chat in chats.resources"
        :key="chat.id"
        :chat="chat"
        :interlocutor="getInterlocutor(chat)"
        :current-user="currentUser"
      />

      <Paging
        v-if="chats.paging"
        :paging="chats.paging"
        :to="{ name: route.name, params: route.params }"
        use-query
        query-key="number"
        :scroll-anchor="() => chatsListRef"
      />
    </div>

    <!-- A first open has nothing to keep on screen. -->
    <ChatPreviewSkeleton v-else-if="loadingChats" />

    <!-- A failed load is not an empty list (as in forum/CommentsList). -->
    <ErrorState
      v-else-if="chatsError"
      :message="chatsError"
      :retry="loadChats"
    />

    <EmptyState
      v-else
      title="Нет переписок"
      hint="Найдите собеседника через поиск выше"
    />
  </div>
</template>

<style scoped lang="sass">
.messenger-list
  display: flex
  flex-direction: column
  min-width: 0

.search-section
  margin-bottom: $medium

.chats-list
  display: flex
  flex-direction: column
  gap: $tiny
</style>
