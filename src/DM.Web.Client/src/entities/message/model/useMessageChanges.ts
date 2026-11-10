import { ref, type Ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { Chat, ChatId, Message, MessageId } from "./types";
import messagingApi from "../api/messagingApi";
import { unwrapResource } from "@/shared/api";

interface MessageChanges {
  messages: Ref<Message[]>;
  chats: Ref<ListEnvelope<Chat> | null>;
  selectedChat: Ref<Chat | null>;
  currentUser: Ref<{ username: string } | null>;
  sending: Ref<boolean>;
}

/** The error goes back, so the caller can restore the text. */
async function sendMessage(c: MessageChanges, chatId: ChatId, text: string) {
  if (!text.trim()) return { error: null };
  c.sending.value = true;
  try {
    const { data, error } = await messagingApi.sendMessage(chatId, text);
    if (error) return { error };
    const sent = unwrapResource<Message>(data);
    if (!sent) return { error: null };
    c.messages.value.push(sent);
    const chat = c.chats.value?.resources.find((ch) => ch.id === chatId);
    if (chat) chat.lastMessage = sent as Chat["lastMessage"];
    if (c.selectedChat.value?.id === chatId) {
      c.selectedChat.value.lastMessage = sent as Chat["lastMessage"];
    }
  } finally {
    c.sending.value = false;
  }
  return { error: null };
}

async function updateMessage(c: MessageChanges, id: string, text: string) {
  const { data, error } = await messagingApi.updateMessage(id as MessageId, {
    text,
  });
  const updated = unwrapResource<Message>(data);
  if (!error && updated) {
    const idx = c.messages.value.findIndex((m) => m.id === id);
    if (idx !== -1) {
      c.messages.value[idx] = updated;
    }
  }
  return { data: updated, error };
}

// Marked removed rather than filtered out.
async function deleteMessage(c: MessageChanges, id: string) {
  const { error } = await messagingApi.deleteMessage(id as MessageId);
  if (!error) {
    const idx = c.messages.value.findIndex((m) => m.id === id);
    if (idx !== -1) {
      c.messages.value[idx] = {
        ...c.messages.value[idx],
        isRemoved: true as Message["isRemoved"],
      };
    }
  }
  return { error };
}

async function likeMessage(c: MessageChanges, id: string) {
  const { data, error } = await messagingApi.likeMessage(id as MessageId);
  const liked = unwrapResource<Message>(data);
  if (!error && liked) {
    const idx = c.messages.value.findIndex((m) => m.id === id);
    if (idx !== -1) {
      c.messages.value[idx] = liked;
    }
  }
  return { data: liked, error };
}

async function unlikeMessage(c: MessageChanges, id: string) {
  const { error } = await messagingApi.unlikeMessage(id as MessageId);
  if (!error && c.currentUser.value) {
    const idx = c.messages.value.findIndex((m) => m.id === id);
    if (idx !== -1) {
      // The answer has no body, so the likes are changed here.
      const currentUsername = c.currentUser.value.username;
      c.messages.value[idx] = {
        ...c.messages.value[idx],
        likes: c.messages.value[idx].likes.filter(
          (u) => u.username !== currentUsername,
        ) as Message["likes"],
      };
    }
  }
  return { error };
}

// Each change is written into the window once the server took it.
export function useMessageChanges(options: Omit<MessageChanges, "sending">) {
  const c: MessageChanges = { ...options, sending: ref(false) };

  return {
    sending: c.sending,
    sendMessage: (chatId: ChatId, text: string) => sendMessage(c, chatId, text),
    updateMessage: (id: string, text: string) => updateMessage(c, id, text),
    deleteMessage: (id: string) => deleteMessage(c, id),
    likeMessage: (id: string) => likeMessage(c, id),
    unlikeMessage: (id: string) => unlikeMessage(c, id),
  };
}
