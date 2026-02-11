import { defineStore, storeToRefs } from "pinia";
import { ref, computed } from "vue";
import type { ListEnvelope, Paging } from "@/api/models/common";
import type {
  Conversation,
  ConversationId,
  Message,
  MessageId,
} from "@/api/models/messaging";
import type { UserLogin } from "@/api/models/community";
import messagingApi from "@/api/requests/messagingApi";
import { useUserStore } from "@/stores/user";

const PAGE_SIZE = 50;

export const useMessagingStore = defineStore("messaging", () => {
  const { user: currentUser } = storeToRefs(useUserStore());

  // Error state for messaging operations
  const error = ref<string | null>(null);

  // Conversations list
  const conversations = ref<ListEnvelope<Conversation> | null>(null);
  const loadingConversations = ref(false);

  async function fetchConversations(number: number = 1) {
    loadingConversations.value = true;
    try {
      const size =
        currentUser.value?.settings?.pagingLimits?.entitiesPerPage ?? 20;
      const { data, error: err } = await messagingApi.getConversations({
        number,
        size,
      });
      if (err) {
        error.value = "Не удалось загрузить переписки";
        conversations.value = null;
        return;
      }
      error.value = null;
      conversations.value = data ?? null;
    } finally {
      loadingConversations.value = false;
    }
  }

  // Selected conversation
  const selectedConversation = ref<Conversation | null>(null);
  const loadingConversation = ref(false);

  async function selectConversation(id: ConversationId) {
    loadingConversation.value = true;
    try {
      const { data, error: err } = await messagingApi.getConversation(id);
      if (err) {
        error.value = "Не удалось загрузить переписку";
        selectedConversation.value = null;
        return;
      }
      error.value = null;
      selectedConversation.value = data?.resource ?? null;
    } finally {
      loadingConversation.value = false;
    }
  }

  async function selectDirectConversation(login: UserLogin) {
    loadingConversation.value = true;
    try {
      const { data, error: err } = await messagingApi.getOrCreateDirectConversation(login);
      if (err) {
        error.value = "Не удалось загрузить прямую переписку";
        selectedConversation.value = null;
        return null;
      }
      error.value = null;
      selectedConversation.value = data?.resource ?? null;
      return data?.resource ?? null;
    } finally {
      loadingConversation.value = false;
    }
  }

  // Messages in selected conversation (flat array for easier manipulation)
  const messagesList = ref<Message[]>([]);
  const loadingMessages = ref(false);
  const loadingBefore = ref(false);
  const hasMoreBefore = ref(false);
  const hasMoreAfter = ref(false);
  const currentPaging = ref<Paging | null>(null);
  const highlightedMessageId = ref<string | null>(null);

  // Backwards compatibility: ListEnvelope wrapper
  const messages = computed(() => {
    if (messagesList.value.length === 0 && !currentPaging.value) return null;
    return {
      resources: messagesList.value,
      paging: currentPaging.value,
    } as ListEnvelope<Message>;
  });

  // Fetch initial messages (latest page)
  async function fetchMessages(conversationId: ConversationId) {
    loadingMessages.value = true;
    try {
      // First, get page 1 to know total pages
      const { data: firstData } = await messagingApi.getMessages(
        conversationId,
        { number: 1, size: PAGE_SIZE },
      );
      const totalPages = firstData?.paging?.pages ?? 1;

      if (totalPages === 1) {
        // Only one page, use it directly
        messagesList.value = firstData?.resources ?? [];
        currentPaging.value = firstData?.paging ?? null;
        hasMoreBefore.value = false;
        hasMoreAfter.value = false;
      } else {
        // Fetch the last page (newest messages)
        const { data } = await messagingApi.getMessages(conversationId, {
          number: totalPages,
          size: PAGE_SIZE,
        });
        messagesList.value = data?.resources ?? [];
        currentPaging.value = data?.paging ?? null;
        hasMoreBefore.value = totalPages > 1;
        hasMoreAfter.value = false;
      }
    } finally {
      loadingMessages.value = false;
    }
  }

  // Load older messages (scroll up)
  async function fetchMoreBefore() {
    if (
      loadingBefore.value ||
      !hasMoreBefore.value ||
      !selectedConversation.value ||
      !currentPaging.value
    )
      return;

    const currentPage = currentPaging.value.current;
    if (currentPage <= 1) {
      hasMoreBefore.value = false;
      return;
    }

    loadingBefore.value = true;
    try {
      const prevPage = currentPage - 1;
      const { data } = await messagingApi.getMessages(
        selectedConversation.value.id,
        {
          number: prevPage,
          size: PAGE_SIZE,
        },
      );

      if (data && data.resources.length > 0) {
        // Prepend older messages
        messagesList.value = [...data.resources, ...messagesList.value];
        currentPaging.value = {
          ...currentPaging.value!,
          current: prevPage,
          number: prevPage,
        };
        hasMoreBefore.value = prevPage > 1;
      } else {
        hasMoreBefore.value = false;
      }
    } finally {
      loadingBefore.value = false;
    }
  }

  // Jump to latest messages
  async function jumpToLatest() {
    if (!selectedConversation.value) return;
    await fetchMessages(selectedConversation.value.id);
    highlightedMessageId.value = null;
  }

  function clearHighlight() {
    highlightedMessageId.value = null;
  }

  // Interlocutor (the other participant in a conversation)
  const interlocutor = computed(() => {
    if (!selectedConversation.value) return null;
    const participants = selectedConversation.value.participants;
    if (!participants || participants.length === 0) return null;

    // If only one participant, that's the interlocutor (API might not include current user)
    if (participants.length === 1) {
      return participants[0];
    }

    // If current user is known, find the other participant (case-insensitive)
    if (currentUser.value?.login) {
      const currentLogin = currentUser.value.login.toLowerCase();
      const other = participants.find(
        (p) => p.login.toLowerCase() !== currentLogin,
      );
      if (other) return other;
    }

    // Fallback: return the first participant that's not obviously the current user
    return participants[0];
  });

  // Total unread count across all conversations
  const totalUnreadCount = computed(() => {
    if (!conversations.value) return 0;
    return conversations.value.resources.reduce(
      (sum, conv) => sum + (conv.unreadMessagesCount ?? 0),
      0,
    );
  });

  // Fetch just enough data to get unread counts (called on app start)
  async function fetchUnreadCount() {
    if (!currentUser.value) return;
    // Load first page to get unread counts
    const { data } = await messagingApi.getConversations({
      number: 1,
      size: 20,
    });
    if (data) {
      conversations.value = data;
    }
  }

  // Mark conversation as read
  async function markAsRead(conversationId: ConversationId) {
    await messagingApi.markConversationAsRead(conversationId);
    // Update local state
    if (selectedConversation.value?.id === conversationId) {
      (selectedConversation.value as Conversation).unreadMessagesCount =
        0 as Conversation["unreadMessagesCount"];
    }
    if (conversations.value) {
      const conv = conversations.value.resources.find(
        (c) => c.id === conversationId,
      );
      if (conv) {
        (conv as Conversation).unreadMessagesCount =
          0 as Conversation["unreadMessagesCount"];
      }
    }
  }

  // Send message
  const sending = ref(false);

  async function sendMessage(conversationId: ConversationId, text: string) {
    if (!text.trim()) return null;
    sending.value = true;
    try {
      const { data, error } = await messagingApi.sendMessage(
        conversationId,
        text,
      );
      if (!error && data) {
        messagesList.value.push(data.resource as Message);
        // Update last message in conversation list
        if (conversations.value) {
          const conv = conversations.value.resources.find(
            (c) => c.id === conversationId,
          );
          if (conv) {
            (conv as Conversation).lastMessage =
              data.resource as typeof conv.lastMessage;
          }
        }
        if (selectedConversation.value?.id === conversationId) {
          (selectedConversation.value as Conversation).lastMessage =
            data.resource as Conversation["lastMessage"];
        }
      }
      return data?.resource ?? null;
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
        messagesList.value[idx] = data.resource;
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
        messagesList.value[idx] = data.resource;
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
        const currentLogin = currentUser.value.login;
        messagesList.value[idx] = {
          ...messagesList.value[idx],
          likes: messagesList.value[idx].likes.filter(
            (u) => u.login !== currentLogin,
          ) as Message["likes"],
        };
      }
    }
    return { error };
  }

  // Check if current user liked a message
  function isLikedByCurrentUser(message: Message): boolean {
    if (!currentUser.value || !message.likes) return false;
    return message.likes.some((u) => u.login === currentUser.value!.login);
  }

  // Clear selected conversation
  function clearSelection() {
    selectedConversation.value = null;
    messagesList.value = [];
    currentPaging.value = null;
    hasMoreBefore.value = false;
    hasMoreAfter.value = false;
    highlightedMessageId.value = null;
  }

  return {
    // Error state
    error,

    // Conversations
    conversations,
    loadingConversations,
    fetchConversations,
    fetchUnreadCount,
    totalUnreadCount,

    // Selected conversation
    selectedConversation,
    loadingConversation,
    selectConversation,
    selectDirectConversation,
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
