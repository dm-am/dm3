import { defineStore, storeToRefs } from "pinia";
import { ref, computed } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { Chat, ChatId } from "./types";
import type { Username } from "@/shared/api/models/common";
import messagingApi from "../api/messagingApi";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { useMessageWindow } from "./useMessageWindow";
import { useChatUnread } from "./useChatUnread";
import { useMessageChanges } from "./useMessageChanges";
import type { RequestOptions } from "@/shared/api/interceptor";

export const useMessagingStore = defineStore("messaging", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const { pageSize } = usePaging();

  const error = ref<string | null>(null);

  const chats = ref<ListEnvelope<Chat> | null>(null);
  const loadingChats = ref(false);

  async function fetchChats(number: number = 1) {
    loadingChats.value = true;
    try {
      const take = pageSize.value;
      const { data, error: err } = await messagingApi.getChats({
        number,
        take,
      });
      if (err) {
        error.value = "Не удалось загрузить переписки";
        chats.value = null;
        return;
      }
      error.value = null;
      chats.value = data ?? null;
    } finally {
      loadingChats.value = false;
    }
  }

  const selectedChat = ref<Chat | null>(null);
  const loadingChat = ref(false);
  // Only a 404: a correspondence on its way or not brought by the network is not missing.
  const chatMissing = ref(false);
  /** Any other failure; the page offers a retry. */
  const chatError = ref<string | null>(null);

  // Apart from the window's guard: the header and the window land separately.
  const chatGuard = createRequestGuard();

  const { forgetFirstMessage, emptyWindow, resetWindow, ...messageWindow } =
    useMessageWindow({ selectedChat, pageSize });
  const { discountUnread, ...unread } = useChatUnread({
    currentUser,
    chats,
    selectedChat,
  });
  const changes = useMessageChanges({
    messages: messageWindow.messagesList,
    chats,
    selectedChat,
    currentUser,
  });

  async function selectChat(id: ChatId) {
    const requestId = chatGuard.next();
    // The page stays mounted, and the first day asked of the one before is not this one's.
    if (selectedChat.value?.id !== id) forgetFirstMessage();
    loadingChat.value = true;
    chatMissing.value = false;
    chatError.value = null;
    try {
      const { data, error: err } = await messagingApi.getChat(id);
      if (!chatGuard.isCurrent(requestId)) return;
      // Not `error`, which is the list's.
      if (err) {
        selectedChat.value = null;
        if (err.status === 404) chatMissing.value = true;
        else chatError.value = "Не удалось загрузить переписку";
        return;
      }
      selectedChat.value = data ?? null;
      chatMissing.value = !data;
    } finally {
      if (chatGuard.isCurrent(requestId)) loadingChat.value = false;
    }
  }

  async function selectDirectChat(username: Username) {
    const requestId = chatGuard.next();
    loadingChat.value = true;
    try {
      const { data, error: err } =
        await messagingApi.getOrCreateDirectChat(username);
      // The caller navigates by the id, so a stale answer still returns its chat.
      if (!chatGuard.isCurrent(requestId)) return data ?? null;
      if (err) {
        error.value = "Не удалось загрузить прямую переписку";
        selectedChat.value = null;
        return null;
      }
      error.value = null;
      selectedChat.value = data ?? null;
      return data ?? null;
    } finally {
      if (chatGuard.isCurrent(requestId)) loadingChat.value = false;
    }
  }

  const interlocutor = computed(() => {
    const participants = selectedChat.value?.participants;
    if (!participants?.length) return null;
    const me = currentUser.value?.username.toLowerCase();
    return (
      participants.find((p) => p.username.toLowerCase() !== me) ??
      participants[0]
    );
  });

  // The window is emptied: the server gives nothing older than the moment of clearing.
  async function clearHistory(chatId: ChatId, options?: RequestOptions) {
    const { error: apiError } = await messagingApi.clearHistory(
      chatId,
      options,
    );
    if (apiError) return { error: apiError };
    discountUnread(chatId);
    const cleared = (chat: Chat) => {
      chat.lastMessage = undefined;
      chat.unreadMessagesCount = 0 as Chat["unreadMessagesCount"];
    };
    const row = chats.value?.resources.find((c) => c.id === chatId);
    if (row) cleared(row);
    if (selectedChat.value?.id === chatId) {
      cleared(selectedChat.value);
      emptyWindow();
    }
    return { error: null };
  }

  async function leaveChat(chatId: ChatId, options?: RequestOptions) {
    const { error: apiError } = await messagingApi.leaveChat(chatId, options);
    if (apiError) return { error: apiError };
    discountUnread(chatId);
    if (chats.value) {
      const kept = chats.value.resources.filter((c) => c.id !== chatId);
      if (kept.length !== chats.value.resources.length) {
        chats.value = {
          ...chats.value,
          resources: kept,
          paging: chats.value.paging && {
            ...chats.value.paging,
            total: Math.max(0, chats.value.paging.total - 1),
          },
        };
      }
    }
    return { error: null };
  }

  function clearSelection() {
    selectedChat.value = null;
    chatMissing.value = false;
    chatError.value = null;
    resetWindow();
  }

  return {
    error,

    chats,
    loadingChats,
    fetchChats,

    selectedChat,
    loadingChat,
    chatMissing,
    chatError,
    selectChat,
    selectDirectChat,
    interlocutor,
    clearHistory,
    leaveChat,
    clearSelection,

    ...unread,
    ...messageWindow,
    ...changes,
  };
});
