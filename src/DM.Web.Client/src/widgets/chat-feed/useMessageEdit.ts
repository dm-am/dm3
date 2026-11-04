import { ref } from "vue";

/**
 * The edit box of the feed, one message at a time. It is seeded once with the
 * message's BBCode, and the typed text comes back with save-edit.
 *
 * The box opens on that text and not before: an empty box takes typing that
 * the arriving text overwrites. The last press decides, "Отмена" included, so
 * the text of a press that another one came after is dropped.
 */
export function useMessageEdit(options: {
  /** The BBCode of a message, as its author wrote it. */
  load: (id: string) => Promise<string>;
  /** The box closed, saved or not. */
  closed: () => void;
}) {
  const editingId = ref<string | null>(null);
  const editText = ref("");
  let presses = 0;

  async function startEdit(message: { id: string }) {
    const press = ++presses;
    const text = await options.load(message.id);
    if (press !== presses) return;
    editText.value = text;
    editingId.value = message.id;
  }

  function cancelEdit() {
    presses++;
    editingId.value = null;
    editText.value = "";
    options.closed();
  }

  return { editingId, editText, startEdit, cancelEdit };
}
