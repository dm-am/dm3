import { ref, type Ref } from "vue";
import type { UnreadCount, ListEnvelope } from "@/shared/api/models/common";
import type { Chat, ChatId } from "./types";
import messagingApi from "../api/messagingApi";
import { unwrapResource } from "@/shared/api";
import { deferredRead } from "@/shared/lib/utils/deferredRead";

// Batches the pushes, so they do not flood the server with reads.
const FETCH_UNREAD_DEBOUNCE_MS = 2000;

// A stream of pushes must not hold the badge still: the count is reread at least this often.
const FETCH_UNREAD_MAX_WAIT_MS = 10000;

export function useChatUnread(options: {
  currentUser: Ref<{ username: string } | null>;
  chats: Ref<ListEnvelope<Chat> | null>;
  selectedChat: Ref<Chat | null>;
}) {
  const { currentUser, chats, selectedChat } = options;

  // As the server counts it: `chats` holds only one page.
  const totalUnreadCount = ref(0);

  const countRead = deferredRead(
    async () => {
      const { data } = await messagingApi.getUnreadCount();
      const counted = unwrapResource<UnreadCount>(data);
      if (counted) {
        totalUnreadCount.value = counted.count;
      }
    },
    FETCH_UNREAD_DEBOUNCE_MS,
    FETCH_UNREAD_MAX_WAIT_MS,
  );

  /** @param immediate At start; the pushes are debounced. */
  async function fetchUnreadCount(immediate = false) {
    if (!currentUser.value) return;
    if (immediate) await countRead.now();
    else countRead.later();
  }

  function unreadOf(chatId: ChatId): number {
    const known =
      chats.value?.resources.find((c) => c.id === chatId) ??
      (selectedChat.value?.id === chatId ? selectedChat.value : null);
    return known?.unreadMessagesCount ?? 0;
  }

  function discountUnread(chatId: ChatId) {
    totalUnreadCount.value = Math.max(
      0,
      totalUnreadCount.value - unreadOf(chatId),
    );
  }

  async function markAsRead(chatId: ChatId) {
    await messagingApi.markAsRead(chatId);
    // Nothing rereads the server's count after a receipt.
    discountUnread(chatId);
    if (selectedChat.value?.id === chatId) {
      selectedChat.value.unreadMessagesCount = 0 as Chat["unreadMessagesCount"];
    }
    if (chats.value) {
      const chat = chats.value.resources.find((c) => c.id === chatId);
      if (chat) chat.unreadMessagesCount = 0 as Chat["unreadMessagesCount"];
    }
  }

  return { totalUnreadCount, fetchUnreadCount, markAsRead, discountUnread };
}
