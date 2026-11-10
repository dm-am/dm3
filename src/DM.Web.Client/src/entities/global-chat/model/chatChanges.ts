import { ref, type Ref } from "vue";
import type { GlobalChatMessage } from "./types";
import globalChatApi from "../api/globalChatApi";
import { unwrapResource } from "@/shared/api";
import type { User } from "@/shared/api/models/common/user";
import { jumpToLatest, type ChatWindow } from "./chatWindow";

interface ChatChanges {
  w: ChatWindow;
  currentUser: Ref<User | null>;
  sending: Ref<boolean>;
}

/** The error goes back, so the caller can restore the text. */
async function sendMessage(c: ChatChanges, text: string) {
  if (!text.trim()) return { error: null };
  c.sending.value = true;
  try {
    const { data, error } = await globalChatApi.sendMessage(text);
    if (error) return { error };
    // Keyed by id, so addMessage knows the copy the hub pushes back.
    const sent = unwrapResource<GlobalChatMessage>(data);
    if (sent) {
      if (c.w.hasMoreAfter.value) {
        await jumpToLatest(c.w);
      }
      c.w.messages.value.push(sent);
      c.w.messagesById.set(sent.id, sent);
    }
  } finally {
    c.sending.value = false;
  }
  return { error: null };
}

function addMessage(c: ChatChanges, message: GlobalChatMessage) {
  // Only while the latest messages are on screen.
  if (c.w.messagesById.has(message.id) || c.w.hasMoreAfter.value) return;
  c.w.messages.value.push(message);
  c.w.messagesById.set(message.id, message);
}

/** The error goes back, so the editor can stay open. */
async function updateMessage(c: ChatChanges, id: string, text: string) {
  const { data, error } = await globalChatApi.updateMessage(id, text);
  const updated = unwrapResource<GlobalChatMessage>(data);
  if (!error && updated) {
    const index = c.w.messages.value.findIndex((m) => m.id === id);
    if (index !== -1) {
      c.w.messages.value[index] = updated;
      c.w.messagesById.set(id, updated);
    }
  }
  return { error };
}

// Struck through only on success: a refusal must not look like a deletion.
async function deleteMessage(c: ChatChanges, id: string) {
  const { error } = await globalChatApi.deleteMessage(id);
  if (!error) {
    const index = c.w.messages.value.findIndex((m) => m.id === id);
    if (index !== -1) {
      const updated = {
        ...c.w.messages.value[index],
        isRemoved: true,
        deletedBy: c.currentUser.value ?? null,
        deletedUtc: new Date().toISOString(),
      };
      c.w.messages.value[index] = updated;
      c.w.messagesById.set(id, updated);
    }
  }
  return { error };
}

async function likeMessage(c: ChatChanges, id: string) {
  const { data, error } = await globalChatApi.likeMessage(id);
  const liked = unwrapResource<GlobalChatMessage>(data);
  if (liked) {
    const index = c.w.messages.value.findIndex((m) => m.id === id);
    if (index !== -1) {
      c.w.messages.value[index] = liked;
      c.w.messagesById.set(id, liked);
    }
  }
  return { error };
}

async function unlikeMessage(c: ChatChanges, id: string) {
  const { error } = await globalChatApi.unlikeMessage(id);
  // The answer has no body, so the likes are changed here.
  if (!error && c.currentUser.value) {
    const index = c.w.messages.value.findIndex((m) => m.id === id);
    if (index !== -1) {
      const msg = c.w.messages.value[index];
      const updated = {
        ...msg,
        likes: msg.likes.filter(
          (u) => u.username !== c.currentUser.value?.username,
        ),
      };
      c.w.messages.value[index] = updated;
      c.w.messagesById.set(id, updated);
    }
  }
  return { error };
}

// Each change is written into the window once the server took it.
export function useChatChanges(w: ChatWindow, currentUser: Ref<User | null>) {
  const c: ChatChanges = { w, currentUser, sending: ref(false) };

  return {
    sending: c.sending,
    sendMessage: (text: string) => sendMessage(c, text),
    addMessage: (message: GlobalChatMessage) => addMessage(c, message),
    updateMessage: (id: string, text: string) => updateMessage(c, id, text),
    deleteMessage: (id: string) => deleteMessage(c, id),
    likeMessage: (id: string) => likeMessage(c, id),
    unlikeMessage: (id: string) => unlikeMessage(c, id),
  };
}
