import { defineStore, storeToRefs } from "pinia";
import { ref, computed } from "vue";
import type { ListEnvelope, CursorPaging } from "@/shared/api/models/common";
import type { Chat, ChatId, Message, MessageId } from "./types";
import type { Username } from "@/shared/api/models/common";
import messagingApi from "../api/messagingApi";
import { useAuthStore } from "@/shared/stores";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

const PAGE_SIZE = 50;
const MAX_MESSAGES = 500;

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
      const take = currentUser.value?.settings?.paging?.entitiesPerPage ?? 20;
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

  // Selected chat
  const selectedChat = ref<Chat | null>(null);
  const loadingChat = ref(false);

  // One token per slice, as in the game details store: the conversation header
  // and the message window are fetched by separate calls and land separately,
  // so a single counter would let each cancel the other.
  //
  // Two quick moves between conversations put two of each on the wire, and the
  // first answer arriving last used to overwrite the state — the reader saw a
  // conversation they had navigated away from, while mark-as-read had already
  // been sent for it.
  const chatGuard = createRequestGuard();
  const messagesGuard = createRequestGuard();

  async function selectChat(id: ChatId) {
    const requestId = chatGuard.next();
    loadingChat.value = true;
    try {
      const { data, error: err } = await messagingApi.getChat(id);
      if (!chatGuard.isCurrent(requestId)) return;
      if (err) {
        error.value = "Не удалось загрузить переписку";
        selectedChat.value = null;
        return;
      }
      error.value = null;
      selectedChat.value = data ?? null;
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
      // The caller navigates by the id it gets back, so a stale answer still
      // returns its own chat — it just does not touch the store on the way.
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

  // Messages in selected chat (flat array for easier manipulation)
  const messagesList = ref<Message[]>([]);
  const loadingMessages = ref(false);
  const loadingBefore = ref(false);
  /** Failure of the last "load older" page, kept apart from `error` (the whole
   * view's failure) so a refused page-2 request leaves the rendered messages
   * alone; surfaced at the top sentinel with a retry, same as the global
   * chat's errorBefore. */
  const errorBefore = ref<string | null>(null);
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
    const requestId = messagesGuard.next();
    loadingMessages.value = true;
    // A fresh window carries no failed page with it: the error belongs to the
    // window it happened in, and keeping it would park the new one's sentinel.
    errorBefore.value = null;
    try {
      const { data } = await messagingApi.getMessages(chatId, {
        limit: PAGE_SIZE,
      });

      if (!messagesGuard.isCurrent(requestId)) return;
      messagesList.value = data?.resources ?? [];
      currentCursor.value = data?.paging ?? null;
      hasMoreBefore.value = data?.paging?.hasPrev ?? false;
      hasMoreAfter.value = data?.paging?.hasNext ?? false;
    } finally {
      if (messagesGuard.isCurrent(requestId)) loadingMessages.value = false;
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
    errorBefore.value = null;
    try {
      const { data, error: apiError } = await messagingApi.getMessagesBefore(
        selectedChat.value.id,
        currentCursor.value.prevCursor,
        PAGE_SIZE,
      );

      if (apiError) {
        // hasMoreBefore stays as it was. Clearing it on a failure would tell
        // the list the history had ended: the sentinel unmounts, and the rest
        // of the correspondence is unreachable until the chat is reopened —
        // with nothing on screen saying why. The sentinel keeps its place and
        // offers the retry instead.
        errorBefore.value = "Не удалось загрузить сообщения";
        return;
      }

      if (data && data.resources.length > 0) {
        // Prepend older messages
        messagesList.value = [...data.resources, ...messagesList.value];
        currentCursor.value = {
          ...currentCursor.value,
          prevCursor: data.paging.prevCursor,
          hasPrev: data.paging.hasPrev,
        };
        hasMoreBefore.value = data.paging.hasPrev;
        // Trim excess messages from the end to prevent unbounded growth
        if (messagesList.value.length > MAX_MESSAGES) {
          messagesList.value = messagesList.value.slice(0, MAX_MESSAGES);
          hasMoreAfter.value = true;
        }
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

  // Load the window around a specific message (jump-to-context from search).
  // Mirrors the global-chat store's navigateToMessage: fetches with
  // aroundMessageId, replaces the list, and flags the message for the view to
  // scroll/highlight.
  async function navigateToMessage(chatId: ChatId, messageId: MessageId) {
    const requestId = messagesGuard.next();
    loadingMessages.value = true;
    errorBefore.value = null;
    try {
      const { data } = await messagingApi.getMessages(chatId, {
        aroundMessageId: messageId,
        limit: PAGE_SIZE,
      });
      if (!messagesGuard.isCurrent(requestId)) return;
      if (data && data.resources.length > 0) {
        messagesList.value = data.resources;
        currentCursor.value = data.paging ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        highlightedMessageId.value = messageId as unknown as string;
      }
    } finally {
      if (messagesGuard.isCurrent(requestId)) loadingMessages.value = false;
    }
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

  // Debounce state for fetchUnreadCount (prevents request flood from SignalR)
  let fetchUnreadDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  const FETCH_UNREAD_DEBOUNCE_MS = 2000; // 2 seconds

  /**
   * Fetch just enough data to get unread counts.
   * @param immediate - If true, fetches immediately (for app start). If false, debounces (for SignalR).
   */
  async function fetchUnreadCount(immediate = false) {
    if (!currentUser.value) return;

    // Clear any pending debounced fetch
    if (fetchUnreadDebounceTimer) {
      clearTimeout(fetchUnreadDebounceTimer);
      fetchUnreadDebounceTimer = null;
    }

    const doFetch = async () => {
      const { data } = await messagingApi.getChats({
        number: 1,
        take: 20,
      });
      if (data) {
        chats.value = data;
      }
    };

    if (immediate) {
      await doFetch();
    } else {
      // Debounce: wait before fetching (batches rapid SignalR messages)
      fetchUnreadDebounceTimer = setTimeout(doFetch, FETCH_UNREAD_DEBOUNCE_MS);
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
      const chat = chats.value.resources.find((c) => c.id === chatId);
      if (chat) {
        (chat as Chat).unreadMessagesCount = 0 as Chat["unreadMessagesCount"];
      }
    }
  }

  // Send message
  const sending = ref(false);

  /** Returns the error when the send failed, so the caller can restore the text. */
  async function sendMessage(chatId: ChatId, text: string) {
    if (!text.trim()) return { error: null };
    sending.value = true;
    try {
      const { data, error } = await messagingApi.sendMessage(chatId, text);
      if (error) return { error };
      if (data) {
        messagesList.value.push(data as Message);
        // Update last message in chat list
        if (chats.value) {
          const chat = chats.value.resources.find((c) => c.id === chatId);
          if (chat) {
            (chat as Chat).lastMessage = data as typeof chat.lastMessage;
          }
        }
        if (selectedChat.value?.id === chatId) {
          (selectedChat.value as Chat).lastMessage =
            data as Chat["lastMessage"];
        }
      }
    } finally {
      sending.value = false;
    }
    return { error: null };
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
    return message.likes.some(
      (u) => u.username === currentUser.value!.username,
    );
  }

  // Clear selected chat
  function clearSelection() {
    selectedChat.value = null;
    messagesList.value = [];
    currentCursor.value = null;
    hasMoreBefore.value = false;
    hasMoreAfter.value = false;
    errorBefore.value = null;
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
    errorBefore,
    hasMoreBefore,
    hasMoreAfter,
    highlightedMessageId,
    fetchMessages,
    fetchMoreBefore,
    jumpToLatest,
    navigateToMessage,
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
