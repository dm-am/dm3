import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { GlobalChatMessage } from "./types";
import globalChatApi from "../api/globalChatApi";
import { useAuthStore } from "@/shared/stores";

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

  // Cursors for pagination
  const prevCursor = ref<string | null>(null);
  const nextCursor = ref<string | null>(null);

  // Internal Map for O(1) message lookup by ID
  const messagesById = new Map<string, GlobalChatMessage>();

  /** Max messages in memory to prevent unbounded growth during long scrolling sessions */
  const MAX_MESSAGES = 500;

  // Sync Map when messages array changes
  function syncMessagesMap() {
    messagesById.clear();
    for (const msg of messages.value) {
      messagesById.set(msg.id, msg);
    }
  }

  /** Trim messages to MAX_MESSAGES, keeping the most recent. Adjusts cursors accordingly. */
  function trimOldMessages() {
    if (messages.value.length > MAX_MESSAGES) {
      messages.value = messages.value.slice(-MAX_MESSAGES);
      hasMoreBefore.value = true; // There are definitely older messages now
      prevCursor.value = messages.value[0]?.id ?? null;
      syncMessagesMap();
    }
  }

  // Initial load - fetches the latest messages
  async function fetchMessages() {
    loading.value = true;
    error.value = null;
    try {
      const { data, error: apiError } = await globalChatApi.getMessages({
        limit: 50,
      });
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
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
    if (loadingBefore.value || !hasMoreBefore.value || !prevCursor.value)
      return;
    loadingBefore.value = true;
    try {
      const { data } = await globalChatApi.getMessagesBefore(
        prevCursor.value,
        50,
      );
      if (data && data.resources.length > 0) {
        messages.value = [...data.resources, ...messages.value];
        prevCursor.value = data.paging?.prevCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
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
    try {
      const { data } = await globalChatApi.getMessagesAfter(
        nextCursor.value,
        50,
      );
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

  // Navigate to messages for a specific date
  async function navigateToDate(date: string) {
    loading.value = true;
    error.value = null;
    try {
      // Convert date to ISO 8601 UTC timestamp (start of day)
      const timestampUtc = new Date(date + "T00:00:00Z").toISOString();
      const { data, error: apiError } = await globalChatApi.getMessagesNearDate(
        timestampUtc,
        50,
      );
      if (apiError) {
        error.value = "Не удалось загрузить сообщения";
        return;
      }
      if (data && data.resources.length > 0) {
        messages.value = data.resources;
        prevCursor.value = data.paging?.prevCursor ?? null;
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        // Highlight the first message in the result
        highlightedMessageId.value = data.resources[0]?.id ?? null;
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
      return data || null;
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

  async function sendMessage(text: string) {
    if (!text.trim()) return;
    sending.value = true;
    try {
      const { data } = await globalChatApi.sendMessage(text);
      if (data) {
        // If we're not at the latest, jump to latest first
        if (hasMoreAfter.value) {
          await jumpToLatest();
        }
        messages.value.push(data);
        messagesById.set(data.id, data);
      }
    } finally {
      sending.value = false;
    }
  }

  function addMessage(message: GlobalChatMessage) {
    if (messagesById.has(message.id) || hasMoreAfter.value) return;
    // Only add if we're viewing the latest messages
    messages.value.push(message);
    messagesById.set(message.id, message);
  }

  async function updateMessage(id: string, text: string) {
    const { data } = await globalChatApi.updateMessage(id, text);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data;
        messagesById.set(id, data);
      }
    }
  }

  async function deleteMessage(id: string) {
    await globalChatApi.deleteMessage(id);
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

  async function likeMessage(id: string) {
    const { data } = await globalChatApi.likeMessage(id);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data;
        messagesById.set(id, data);
      }
    }
  }

  async function unlikeMessage(id: string) {
    await globalChatApi.unlikeMessage(id);
    // Backend returns 204 No Content, so update likes locally
    if (currentUser.value) {
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
  }

  return {
    messages,
    loading,
    error,
    loadingBefore,
    loadingAfter,
    sending,
    hasMoreBefore,
    hasMoreAfter,
    highlightedMessageId,
    prevCursor,
    nextCursor,
    fetchMessages,
    fetchMoreBefore,
    fetchMoreAfter,
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
  };
});
