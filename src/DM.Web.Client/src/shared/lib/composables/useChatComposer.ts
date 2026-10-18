import { ref, type Ref } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "./useActionResult";

/**
 * Writing and deleting a chat message.
 *
 * The two chat views — the global chat and the messenger — had the same four
 * functions each, line for line: the send with its optimistic clear and its
 * restore-on-failure, and the three halves of the inline delete confirmation.
 * Only the store differed. What a duplicate like that costs is not the lines,
 * it is that every later fix has to be found twice: the toolbar above these
 * messages was copied the same way and one copy went years without accessible
 * names on its buttons, because nobody looking at the other one could tell.
 *
 * What stays with the page is what genuinely differs: which store answers, and
 * where the feed scrolls afterwards.
 *
 * A refusal is said where the press was, not in a plate (UI_STANDARDS,
 * "Всплывающие сообщения"): a refused send under the "Отправить" of the
 * composer, with the text back in the field; a refused press on a message - a
 * delete here, a saved edit or a like on the page - in a line under that
 * message, inside its row of the list so the list measures it with the row.
 */

/** What a chat store answers a send or a delete with. */
export interface ChatActionResult {
  error: GeneralError | BadRequestError | null;
}

export interface ChatComposerOptions {
  /** True while a send is in flight — the store's own flag. */
  sending: Ref<boolean>;

  /**
   * Hands the text to the store. Returns the error rather than throwing, so
   * the text can go back into the field.
   */
  send: (text: string) => Promise<ChatActionResult>;

  /** Hands the id to the store. */
  remove: (id: string) => Promise<ChatActionResult>;

  /**
   * The message editor. Its own clear() drops the saved draft as well, which is
   * the copy of the text that outlives the tab, so it is only called once the
   * send has landed.
   */
  editor: Ref<{ clear: () => void } | null>;

  /**
   * Which message shows "точно удалить?" in place of its delete button. Owned
   * by useMessageToolbar, which also drops it when the toolbar goes away.
   */
  confirmingDeleteId: Ref<string | null>;

  /** Follows the feed after a message of one's own lands. */
  scrollToBottom: () => void;

  /**
   * An extra precondition beyond "there is text and nothing is in flight". The
   * messenger has one — a chat has to be selected — and the global chat does
   * not.
   */
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

  /**
   * Says a refused press on a message under it, or nothing where the plate is
   * the carrier (an expired session, a dead connection).
   */
  function refuseMessage(
    id: string,
    error: GeneralError | BadRequestError,
    fallback: string,
  ): void {
    const text = refusalHere(error, fallback);
    messageRefusal.value = text ? { id, text } : null;
  }

  /**
   * Takes the line under a message down as a new press on it starts. A line
   * left over from a refusal the retry has since overcome would stand under a
   * heart that is filled, or an edit that is saved, and say the opposite.
   */
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
    // Give the text back on failure. Emptying the field before the request is
    // what makes sending feel instant; losing what was written when it fails is
    // not part of that bargain.
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
