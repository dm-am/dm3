import { defineStore, storeToRefs } from "pinia";
import { ref, computed } from "vue";
import type { ListEnvelope, CursorPaging } from "@/shared/api/models/common";
import type {
  Chat,
  ChatId,
  Message,
  MessageId,
} from "./types";
import type { Username } from "@/shared/api/models/common";
import messagingApi from "../api/messagingApi";
import { useAuthStore } from "@/shared/stores";

const PAGE_SIZE = 50;

export const useMessagingStore = defineStore("messaging", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());

  // Error state for messaging operations
  const error = ref<string | null>(null);

  // Chats list
  const chats = ref<ListEnvelope<Chat> | null>(null);
  const loadingChats = ref(false);

  async function fetchChats(number: number = 1) {
    loadingChats.value = true;
    try {
      const size =
        currentUser.value?.settings?.paging?.entitiesPerPage ?? 20;
      const { data, error: err } = await messagingApi.getChats({
        number,
        size,
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

  // Selected chat
  const selectedChat = ref<Chat | null>(null);
  const loadingChat = ref(false);

  async function selectChat(id: ChatId) {
    loadingChat.value = true;
    try {
      const { data, error: err } = await messagingApi.getChat(id);
      if (err) {
        error.value = "Не удалось загрузить переписку";
        selectedChat.value = null;
        return;
      }
      error.value = null;
      selectedChat.value = data ?? null;
    } finally {
      loadingChat.value = false;
    }
  }

  async function selectDirectChat(username: Username) {
    loadingChat.value = true;
    try {
      const { data, error: err } = await messagingApi.getOrCreateDirectChat(username);
      if (err) {
        error.value = "Не удалось загрузить прямую переписку";
        selectedChat.value = null;
        return null;
      }
      error.value = null;
      selectedChat.value = data ?? null;
      return data ?? null;
    } finally {
      loadingChat.value = false;
    }
  }

  // Messages in selected chat (flat array for easier manipulation)
  const messagesList = ref<Message[]>([]);
  const loadingMessages = ref(false);
  const loadingBefore = ref(false);
  const hasMoreBefore = ref(false);
  const hasMoreAfter = ref(false);
  const currentCursor = ref<CursorPaging | null>(null);
  const highlightedMessageId = ref<string | null>(null);

  // Backwards compatibility: ListEnvelope wrapper
  const messages = computed(() => {
    if (messagesList.value.length === 0 && !currentCursor.value) return null;
    return {
      resources: messagesList.value,
      paging: null, // Cursor-based pagination doesn't use traditional paging
    } as ListEnvelope<Message>;
  });

  // Fetch initial messages (latest messages)
  async function fetchMessages(chatId: ChatId) {
    loadingMessages.value = true;
    try {
      const { data } = await messagingApi.getMessages(chatId, {
        limit: PAGE_SIZE,
      });

      messagesList.value = data?.resources ?? [];
      currentCursor.value = data?.paging ?? null;
      hasMoreBefore.value = data?.paging?.hasPrev ?? false;
      hasMoreAfter.value = data?.paging?.hasNext ?? false;
    } finally {
      loadingMessages.value = false;
    }
  }

  // Load older messages (scroll up)
  async function fetchMoreBefore() {
    if (
      loadingBefore.value ||
      !hasMoreBefore.value ||
      !selectedChat.value ||
      !currentCursor.value?.prevCursor
    )
      return;

    loadingBefore.value = true;
    try {
      const { data } = await messagingApi.getMessagesBefore(
        selectedChat.value.id,
        currentCursor.value.prevCursor,
        PAGE_SIZE,
      );

      if (data && data.resources.length > 0) {
        // Prepend older messages
        messagesList.value = [...data.resources, ...messagesList.value];
        currentCursor.value = {
          ...currentCursor.value,
          prevCursor: data.paging.prevCursor,
          hasPrev: data.paging.hasPrev,
        };
        hasMoreBefore.value = data.paging.hasPrev;
      } else {
        hasMoreBefore.value = false;
      }
    } finally {
      loadingBefore.value = false;
    }
  }

  // Jump to latest messages
  async function jumpToLatest() {
    if (!selectedChat.value) return;
    await fetchMessages(selectedChat.value.id);
    highlightedMessageId.value = null;
  }

  function clearHighlight() {
    highlightedMessageId.value = null;
  }

  // Interlocutor (the other participant in a chat)
  const interlocutor = computed(() => {
    if (!selectedChat.value) return null;
    const participants = selectedChat.value.participants;
    if (!participants || participants.length === 0) return null;

    // If only one participant, that's the interlocutor (API might not include current user)
    if (participants.length === 1) {
      return participants[0];
    }

    // If current user is known, find the other participant (case-insensitive)
    if (currentUser.value?.username) {
      const currentUsername = currentUser.value.username.toLowerCase();
      const other = participants.find(
        (p) => p.username.toLowerCase() !== currentUsername,
      );
      if (other) return other;
    }

    // Fallback: return the first participant that's not obviously the current user
    return participants[0];
  });

  // Total unread count across all chats
  const totalUnreadCount = computed(() => {
    if (!chats.value) return 0;
    return chats.value.resources.reduce(
      (sum, chat) => sum + (chat.unreadMessagesCount ?? 0),
      0,
    );
  });

  // Fetch just enough data to get unread counts (called on app start)
  async function fetchUnreadCount() {
    if (!currentUser.value) return;
    // Load first page to get unread counts
    const { data } = await messagingApi.getChats({
      number: 1,
      size: 20,
    });
    if (data) {
      chats.value = data;
    }
  }

  // Mark chat as read
  async function markAsRead(chatId: ChatId) {
    await messagingApi.markAsRead(chatId);
    // Update local state
    if (selectedChat.value?.id === chatId) {
      (selectedChat.value as Chat).unreadMessagesCount =
        0 as Chat["unreadMessagesCount"];
    }
    if (chats.value) {
      const chat = chats.value.resources.find(
        (c) => c.id === chatId,
      );
      if (chat) {
        (chat as Chat).unreadMessagesCount =
          0 as Chat["unreadMessagesCount"];
      }
    }
  }

  // Send message
  const sending = ref(false);

  async function sendMessage(chatId: ChatId, text: string) {
    if (!text.trim()) return null;
    sending.value = true;
    try {
      const { data, error } = await messagingApi.sendMessage(
        chatId,
        text,
      );
      if (!error && data) {
        messagesList.value.push(data as Message);
        // Update last message in chat list
        if (chats.value) {
          const chat = chats.value.resources.find(
            (c) => c.id === chatId,
          );
          if (chat) {
            (chat as Chat).lastMessage =
              data as typeof chat.lastMessage;
          }
        }
        if (selectedChat.value?.id === chatId) {
          (selectedChat.value as Chat).lastMessage =
            data as Chat["lastMessage"];
        }
      }
      return data ?? null;
    } finally {
      sending.value = false;
    }
  }

  // Update message
  async function updateMessage(id: string, text: string) {
    const { data, error } = await messagingApi.updateMessage(id as MessageId, {
      text,
    });
    if (!error && data) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        messagesList.value[idx] = data;
      }
    }
    return { data, error };
  }

  // Delete message - mark as removed instead of filtering
  async function deleteMessage(id: string) {
    const { error } = await messagingApi.deleteMessage(id as MessageId);
    if (!error) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        messagesList.value[idx] = {
          ...messagesList.value[idx],
          isRemoved: true as unknown as Message["isRemoved"],
        };
      }
    }
    return { error };
  }

  // Like/unlike message
  async function likeMessage(id: string) {
    const { data, error } = await messagingApi.likeMessage(id as MessageId);
    if (!error && data) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        messagesList.value[idx] = data;
      }
    }
    return { data, error };
  }

  async function unlikeMessage(id: string) {
    const { error } = await messagingApi.unlikeMessage(id as MessageId);
    if (!error && currentUser.value) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        // Remove current user from likes locally since backend returns 204 No Content
        const currentUsername = currentUser.value.username;
        messagesList.value[idx] = {
          ...messagesList.value[idx],
          likes: messagesList.value[idx].likes.filter(
            (u) => u.username !== currentUsername,
          ) as Message["likes"],
        };
      }
    }
    return { error };
  }

  // Check if current user liked a message
  function isLikedByCurrentUser(message: Message): boolean {
    if (!currentUser.value || !message.likes) return false;
    return message.likes.some((u) => u.username === currentUser.value!.username);
  }

  // Clear selected chat
  function clearSelection() {
    selectedChat.value = null;
    messagesList.value = [];
    currentCursor.value = null;
    hasMoreBefore.value = false;
    hasMoreAfter.value = false;
    highlightedMessageId.value = null;
  }

  return {
    // Error state
    error,

    // Chats
    chats,
    loadingChats,
    fetchChats,
    fetchUnreadCount,
    totalUnreadCount,

    // Selected chat
    selectedChat,
    loadingChat,
    selectChat,
    selectDirectChat,
    interlocutor,
    markAsRead,
    clearSelection,

    // Messages
    messages,
    messagesList,
    loadingMessages,
    loadingBefore,
    hasMoreBefore,
    hasMoreAfter,
    highlightedMessageId,
    fetchMessages,
    fetchMoreBefore,
    jumpToLatest,
    clearHighlight,
    sending,
    sendMessage,
    updateMessage,
    deleteMessage,

    // Likes
    likeMessage,
    unlikeMessage,
    isLikedByCurrentUser,
  };
});
