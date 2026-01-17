import { defineStore, storeToRefs } from "pinia";
import { ref, computed } from "vue";
import type { ChatMessage } from "@/api/models/chat";
import chatApi from "@/api/requests/chatApi";
import { useUserStore } from "@/stores/user";

export const useChatStore = defineStore("chat", () => {
  const { user: currentUser } = storeToRefs(useUserStore());
  const messages = ref<ChatMessage[]>([]);
  const loading = ref(false);
  const loadingBefore = ref(false);
  const loadingAfter = ref(false);
  const sending = ref(false);
  const hasMoreBefore = ref(true);
  const hasMoreAfter = ref(false);
  const highlightedMessageId = ref<string | null>(null);

  // Initial load - fetches the latest messages
  async function fetchMessages() {
    loading.value = true;
    try {
      const { data } = await chatApi.getMessages({ number: 1, size: 50 });
      messages.value = data?.resources || [];
      hasMoreBefore.value = (data?.paging?.pages ?? 1) > 1;
      hasMoreAfter.value = false; // We're at the latest
    } finally {
      loading.value = false;
    }
  }

  // Load older messages (scroll up)
  async function fetchMoreBefore() {
    if (loadingBefore.value || !hasMoreBefore.value || messages.value.length === 0) return;
    loadingBefore.value = true;
    try {
      const firstMessage = messages.value[0];
      const { data } = await chatApi.getMessagesBefore(firstMessage.id, 50);
      if (data && data.resources.length > 0) {
        messages.value = [...data.resources, ...messages.value];
        hasMoreBefore.value = data.paging?.hasMoreBefore ?? false;
      } else {
        hasMoreBefore.value = false;
      }
    } finally {
      loadingBefore.value = false;
    }
  }

  // Load newer messages (scroll down)
  async function fetchMoreAfter() {
    if (loadingAfter.value || !hasMoreAfter.value || messages.value.length === 0) return;
    loadingAfter.value = true;
    try {
      const lastMessage = messages.value[messages.value.length - 1];
      const { data } = await chatApi.getMessagesAfter(lastMessage.id, 50);
      if (data && data.resources.length > 0) {
        messages.value = [...messages.value, ...data.resources];
        hasMoreAfter.value = data.paging?.hasMoreAfter ?? false;
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
      const { data } = await chatApi.getMessagesAround(messageId, 50);
      if (data && data.resources.length > 0) {
        messages.value = data.resources;
        hasMoreBefore.value = data.paging?.hasMoreBefore ?? false;
        hasMoreAfter.value = data.paging?.hasMoreAfter ?? false;
        highlightedMessageId.value = messageId;
      }
    } finally {
      loading.value = false;
    }
  }

  // Navigate to messages for a specific date (or nearest after if empty)
  async function navigateToDate(date: string) {
    loading.value = true;
    try {
      // Use the endpoint that finds first message on or after the date
      const { data } = await chatApi.getFirstMessageOnOrAfterDate(date);
      if (data && data.resource) {
        await navigateToMessage(data.resource.id);
      } else {
        // No messages found, load latest
        await fetchMessages();
      }
    } catch {
      // 404 or error - load latest
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

  async function fetchMessageById(id: string): Promise<ChatMessage | null> {
    try {
      const { data } = await chatApi.getMessage(id);
      return data?.resource || null;
    } catch {
      return null;
    }
  }

  function findMessageInLoaded(id: string): ChatMessage | null {
    return messages.value.find(m => m.id === id) || null;
  }

  function clearHighlight() {
    highlightedMessageId.value = null;
  }

  async function sendMessage(text: string) {
    if (!text.trim()) return;
    sending.value = true;
    try {
      const { data } = await chatApi.sendMessage(text);
      if (data) {
        // If we're not at the latest, jump to latest first
        if (hasMoreAfter.value) {
          await jumpToLatest();
        }
        messages.value.push(data.resource);
      }
    } finally {
      sending.value = false;
    }
  }

  function addMessage(message: ChatMessage) {
    const exists = messages.value.some((m) => m.id === message.id);
    if (!exists && !hasMoreAfter.value) {
      // Only add if we're viewing the latest messages
      messages.value.push(message);
    }
  }

  async function updateMessage(id: string, text: string) {
    const { data } = await chatApi.updateMessage(id, text);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data.resource;
      }
    }
  }

  async function deleteMessage(id: string) {
    await chatApi.deleteMessage(id);
    const index = messages.value.findIndex((m) => m.id === id);
    if (index !== -1) {
      messages.value[index].isRemoved = true;
    }
  }

  async function likeMessage(id: string) {
    const { data } = await chatApi.likeMessage(id);
    if (data) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        messages.value[index] = data.resource;
      }
    }
  }

  async function unlikeMessage(id: string) {
    await chatApi.unlikeMessage(id);
    // Backend returns 204 No Content, so update likes locally
    if (currentUser.value) {
      const index = messages.value.findIndex((m) => m.id === id);
      if (index !== -1) {
        const msg = messages.value[index];
        messages.value[index] = {
          ...msg,
          likes: msg.likes.filter((u) => u.login !== currentUser.value?.login),
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
