import { defineStore, storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import {
  clearHighlight,
  fetchMessages,
  fetchMoreAfter,
  fetchMoreBefore,
  jumpToLatest,
  navigateToDate,
  navigateToMessage,
  newChatWindow,
} from "./chatWindow";
import { useChatCatchUp } from "./chatCatchUp";
import { useChatChanges } from "./chatChanges";

// The window of messages; the events are useGlobalChatEventsStore.
export const useGlobalChatStore = defineStore("globalChat", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const w = newChatWindow();
  const { pollForNewer } = useChatCatchUp(w);
  const changes = useChatChanges(w, currentUser);

  return {
    messages: w.messages,
    loading: w.loading,
    error: w.error,
    loadingBefore: w.loadingBefore,
    loadingAfter: w.loadingAfter,
    errorBefore: w.errorBefore,
    errorAfter: w.errorAfter,
    sending: changes.sending,
    hasMoreBefore: w.hasMoreBefore,
    hasMoreAfter: w.hasMoreAfter,
    highlightedMessageId: w.highlightedMessageId,
    landedOnRequestedDate: w.landedOnRequestedDate,
    prevCursor: w.prevCursor,
    nextCursor: w.nextCursor,
    fetchMessages: () => fetchMessages(w),
    fetchMoreBefore: () => fetchMoreBefore(w),
    fetchMoreAfter: () => fetchMoreAfter(w),
    pollForNewer,
    navigateToMessage: (messageId: string) => navigateToMessage(w, messageId),
    navigateToDate: (date: string) => navigateToDate(w, date),
    jumpToLatest: () => jumpToLatest(w),

    clearHighlight: () => clearHighlight(w),
    sendMessage: changes.sendMessage,
    addMessage: changes.addMessage,
    updateMessage: changes.updateMessage,
    deleteMessage: changes.deleteMessage,
    likeMessage: changes.likeMessage,
    unlikeMessage: changes.unlikeMessage,
  };
});
