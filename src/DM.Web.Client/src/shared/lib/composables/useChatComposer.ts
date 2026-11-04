import { ref, type Ref } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "./useActionResult";

/**
 * Writing and deleting a chat message in both chat views; the page keeps its
 * store and where the feed scrolls. A refusal is said where the press was: a
 * refused send under the button with the text back, a refused press on a
 * message in a line inside its row.
 */

/** What a chat store answers a send or a delete with. */
export interface ChatActionResult {
  error: GeneralError | BadRequestError | null;
}

export interface ChatComposerOptions {
  /** True while a send is in flight — the store's own flag. */
  sending: Ref<boolean>;

  /** Returns the error rather than throwing, so the text can go back. */
  send: (text: string) => Promise<ChatActionResult>;

  /** Hands the id to the store. */
  remove: (id: string) => Promise<ChatActionResult>;

  /** Its clear() drops the saved draft too, so it is called once the send landed. */
  editor: Ref<{ clear: () => void } | null>;

  /** Owned by useMessageToolbar, which drops it with the toolbar. */
  confirmingDeleteId: Ref<string | null>;

  /** Follows the feed after a message of one's own lands. */
  scrollToBottom: () => void;

  /** A precondition beyond text and nothing in flight: the messenger's selected chat. */
  canSend?: () => boolean;
}

/** A refusal about one message, said under it. */
export interface MessageRefusal {
  id: string;
  text: string;
}

export function useChatComposer(options: ChatComposerOptions) {
  /** The composer field, v-model of the editor. */
  const newMessage = ref("");

  /** A refused send, under the button of the composer. */
  const sendResult = useActionResult();

  /** The last refused press on a message, under that message. */
  const messageRefusal = ref<MessageRefusal | null>(null);

  /** A refused press on a message, under it; nothing where the plate is the carrier. */
  function refuseMessage(
    id: string,
    error: GeneralError | BadRequestError,
    fallback: string,
  ): void {
    const text = refusalHere(error, fallback);
    messageRefusal.value = text ? { id, text } : null;
  }

  /** A new press takes the line down: a stale refusal would contradict "Нравится". */
  function clearMessageRefusal(id: string): void {
    if (messageRefusal.value?.id === id) messageRefusal.value = null;
  }

  async function handleSend(): Promise<void> {
    if (!newMessage.value.trim() || options.sending.value) return;
    if (options.canSend && !options.canSend()) return;

    const text = newMessage.value;
    newMessage.value = "";
    sendResult.clear();
    const { error } = await options.send(text);
    // Emptied before the request to feel instant, given back on failure.
    if (error) {
      newMessage.value = text;
      sendResult.refuse(error, "Не удалось отправить сообщение");
      return;
    }
    options.editor.value?.clear();
    options.scrollToBottom();
  }

  function requestDelete(id: string): void {
    options.confirmingDeleteId.value = id;
  }

  function cancelDelete(): void {
    options.confirmingDeleteId.value = null;
  }

  async function confirmDelete(): Promise<void> {
    const id = options.confirmingDeleteId.value;
    if (!id) return;
    messageRefusal.value = null;
    const { error } = await options.remove(id);
    options.confirmingDeleteId.value = null;
    if (error) refuseMessage(id, error, "Не удалось удалить сообщение");
  }

  return {
    newMessage,
    handleSend,
    requestDelete,
    cancelDelete,
    confirmDelete,
    sendResult,
    messageRefusal,
    refuseMessage,
    clearMessageRefusal,
  };
}
