import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { GlobalChatMessage } from "./types";
import globalChatApi from "../api/globalChatApi";
import { useAuthStore } from "@/shared/stores";

export const useGlobalChatStore = defineStore("globalChat", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const messages = ref<GlobalChatMessage[]>([]);
  const loading = ref(false);
  const loadingBefore = ref(false);
  const loadingAfter = ref(false);
  const sending = ref(false);
  const hasMoreBefore = ref(true);
  const hasMoreAfter = ref(false);
  const highlightedMessageId = ref<string | null>(null);

  // Cursors for pagination
  const prevCursor = ref<string | null>(null);
  const nextCursor = ref<string | null>(null);

  // Initial load - fetches the latest messages
  async function fetchMessages() {
    loading.value = true;
    try {
      const { data } = await globalChatApi.getMessages({ limit: 50 });
      messages.value = data?.resources || [];
      prevCursor.value = data?.paging?.prevCursor ?? null;
      nextCursor.value = data?.paging?.nextCursor ?? null;
      hasMoreBefore.value = data?.paging?.hasPrev ?? false;
      hasMoreAfter.value = data?.paging?.hasNext ?? false;
    } finally {
      loading.value = false;
    }
  }

  // Load older messages (scroll up)
  async function fetchMoreBefore() {
    if (
      loadingBefore.value ||
      !hasMoreBefore.value ||
      !prevCursor.value
    )
      return;
    loadingBefore.value = true;
    try {
      const { data } = await globalChatApi.getMessagesBefore(prevCursor.value, 50);
      if (data && data.resources.length > 0) {
        messages.value = [...data.resources, ...messages.value];
        prevCursor.value = data.paging?.prevCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
      } else {
        hasMoreBefore.value = false;
      }
    } finally {
      loadingBefore.value = false;
    }
  }

  // Load newer messages (scroll down)
  async function fetchMoreAfter() {
    if (
      loadingAfter.value ||
      !hasMoreAfter.value ||
      !nextCursor.value
    )
      return;
    loadingAfter.value = true;
    try {
      const { data } = await globalChatApi.getMessagesAfter(nextCursor.value, 50);
      if (data && data.resources.length > 0) {
        messages.value = [...messages.value, ...data.resources];
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
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
    try {
      const { data } = await globalChatApi.getMessagesAround(messageId, 50);
      if (data && data.resources.length > 0) {
        messages.value = data.resources;
        prevCursor.value = data.paging?.prevCursor ?? null;
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        highlightedMessageId.value = messageId;
      }
    } finally {
      loading.value = false;
    }
  }

  // Navigate to messages for a specific date
  async function navigateToDate(date: string) {
    loading.value = true;
    try {
      // Convert date to ISO 8601 UTC timestamp (start of day)
      const timestampUtc = new Date(date + "T00:00:00Z").toISOString();
      const { data } = await globalChatApi.getMessagesNearDate(timestampUtc, 50);
      if (data && data.resources.length > 0) {
        messages.value = data.resources;
        prevCursor.value = data.paging?.prevCursor ?? null;
        nextCursor.value = data.paging?.nextCursor ?? null;
        hasMoreBefore.value = data.paging?.hasPrev ?? false;
        hasMoreAfter.value = data.paging?.hasNext ?? false;
        // Highlight the first message in the result
        highlightedMessageId.value = data.resources[0]?.id ?? null;
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

  async function fetchMessageById(id: string): Promise<GlobalChatMessage | null> {
    try {
      const { data } = await globalChatApi.getMessage(id);
      return data || null;
    } catch {
      return null;
    }
  }

  function findMessageInLoaded(id: string): GlobalChatMessage | null {
    return messages.value.find((m) => m.id === id) || null;
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
      }
    } finally {
      sending.value = false;
    }
  }

  function addMessage(message: GlobalChatMessage) {
    const exists = messages.value.some((m) => m.id === message.id);
    if (!exists && !hasMoreAfter.value) {
      // Only add if we're viewing the latest messages
      messages.value.push(message);
    }
  }

  async function updateMessage(id: string, text: string) {
    const { data } = await globalChatApi.updateMessage(id, text);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data;
      }
    }
  }

  async function deleteMessage(id: string) {
    await globalChatApi.deleteMessage(id);
    const index = messages.value.findIndex((m) => m.id === id);
    if (index !== -1) {
      messages.value[index] = {
        ...messages.value[index],
        isRemoved: true,
        deletedBy: currentUser.value ?? null,
        deletedAtUtc: new Date().toISOString(),
      };
    }
  }

  async function likeMessage(id: string) {
    const { data } = await globalChatApi.likeMessage(id);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data;
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
        messages.value[index] = {
          ...msg,
          likes: msg.likes.filter((u) => u.username !== currentUser.value?.username),
        };
      }
    }
  }

  return {
    messages,
    loading,
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
