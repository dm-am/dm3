import { ref, type Ref } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { notifyFailure } from "@/shared/lib/errors";

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

export function useChatComposer(options: ChatComposerOptions) {
  /** The composer field, v-model of the editor. */
  const newMessage = ref("");

  async function handleSend(): Promise<void> {
    if (!newMessage.value.trim() || options.sending.value) return;
    if (options.canSend && !options.canSend()) return;

    const text = newMessage.value;
    newMessage.value = "";
    const { error } = await options.send(text);
    // Give the text back on failure. Emptying the field before the request is
    // what makes sending feel instant; losing what was written when it fails is
    // not part of that bargain.
    if (error) {
      newMessage.value = text;
      notifyFailure(error, "Не удалось отправить сообщение");
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
    const { error } = await options.remove(id);
    options.confirmingDeleteId.value = null;
    if (error) notifyFailure(error, "Не удалось удалить сообщение");
  }

  return {
    newMessage,
    handleSend,
    requestDelete,
    cancelDelete,
    confirmDelete,
  };
}
