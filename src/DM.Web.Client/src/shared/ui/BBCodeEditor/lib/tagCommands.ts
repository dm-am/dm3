/**
 * What each tag button of the toolbar does, in either mode: in the BBCode mode
 * it writes the tag into the field, in the visual mode it runs the command of
 * the document. The buttons that ask for something first open their panel
 * (insertions.ts).
 */
import type { Editor } from "@tiptap/vue-3";
import { insertAtCaret, wrapSelection, type EditorTarget } from "./sourceField";

/** The tag around the selection in the BBCode mode, the document's command in the visual one. */
export const wrapTag =
  (before: string, after: string, visual: (editor: Editor) => void) =>
  (target: EditorTarget) => {
    if (target.mode.value === "bbcode") wrapSelection(target, before, after);
    else if (target.editor.value) visual(target.editor.value);
  };

/**
 * A spoiler asks for its caption, which is optional. Inside a spoiler of the
 * visual mode the button takes the spoiler off instead.
 */
export function insertSpoiler(target: EditorTarget, event: Event) {
  const editor = target.editor.value;
  if (target.mode.value === "wysiwyg" && editor?.isActive("spoiler")) {
    target.closeInsertion();
    editor.chain().focus().toggleSpoiler().run();
    return;
  }
  target.toggleInsertion("spoiler", event);
}

export function insertTab(target: EditorTarget) {
  if (target.mode.value === "bbcode") {
    insertAtCaret(target, "[tab]");
  } else {
    target.editor.value?.chain().focus().insertTab().run();
  }
}
