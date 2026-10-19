/**
 * The BBCode field: the textarea of the BBCode mode, which holds the markup
 * the server gets whichever mode is on screen. A piece of markup goes in at
 * the caret or around the selection, and the field grows with its text, the
 * bordered box around it with it.
 *
 * Beside it, the editor all of its parts act on (EditorTarget): the field, the
 * commands of the toolbar, the panels of the insertions and the draft.
 */
import { nextTick, type Ref, type ShallowRef } from "vue";
import type { Editor } from "@tiptap/vue-3";
import type { InsertKind } from "./insertions";

/**
 * The two modes: "wysiwyg", the visual preview of the BBCode that will be
 * sent, and "bbcode", the BBCode source itself.
 */
export type EditorMode = "wysiwyg" | "bbcode";

/**
 * One editor as its parts act on it. BBCodeEditor.vue builds it once, and the
 * field, the commands, the insertions and the draft each take it whole rather
 * than each its own list of refs.
 */
export interface EditorTarget {
  /** The mode on screen. */
  mode: Ref<EditorMode>;
  /** The document of the visual mode. */
  editor: ShallowRef<Editor | undefined>;
  /** The text of the BBCode field. */
  source: Ref<string>;
  /** The textarea of the BBCode mode. */
  field: Ref<HTMLTextAreaElement | null>;
  /** The bordered box of the editor, which grows with the field. */
  frame: Ref<HTMLElement | null>;
  /** What the parent set: the least height of the field, the draft's key, the text. */
  props: Readonly<{ minHeight: number; draftKey: string; modelValue: string }>;
  /** A text the author made, handed to the parent and kept as the draft. */
  changed(text: string): void;
  /** Text into whichever mode is showing, handed to the parent. */
  apply(text: string): void;
  /** The panel of an insertion, opened under its button, or closed again. */
  toggleInsertion(kind: InsertKind, event: Event): void;
  closeInsertion(): void;
  /** The help window. */
  openHelp(): void;
  /** The draft into the editor, when there is one. */
  restoreDraft(): void;
}

/** Put a piece of BBCode at the caret of the BBCode mode. */
export function insertAtCaret(target: EditorTarget, piece: string) {
  const textarea = target.field.value;
  if (!textarea) return;
  const pos = textarea.selectionStart;
  target.source.value =
    target.source.value.substring(0, pos) +
    piece +
    target.source.value.substring(pos);
  target.changed(target.source.value);
  nextTick(() => {
    textarea.focus();
    textarea.setSelectionRange(pos + piece.length, pos + piece.length);
  });
}

/** Put a tag around the selection of the BBCode mode, keeping it selected. */
export function wrapSelection(
  target: EditorTarget,
  before: string,
  after: string,
) {
  const textarea = target.field.value;
  if (!textarea) return;

  const start = textarea.selectionStart;
  const end = textarea.selectionEnd;
  const selected = target.source.value.substring(start, end);
  const newText = before + selected + after;

  target.source.value =
    target.source.value.substring(0, start) +
    newText +
    target.source.value.substring(end);
  target.changed(target.source.value);

  nextTick(() => {
    textarea.focus();
    textarea.setSelectionRange(
      start + before.length,
      start + before.length + selected.length,
    );
  });
}

/** The field as tall as its text, and the box around it with it. */
export function autoResizeTextarea(target: EditorTarget) {
  const textarea = target.field.value;
  const frame = target.frame.value;
  if (!textarea || !frame) return;

  textarea.style.height = "auto";
  frame.style.height = "auto";

  const scrollHeight = textarea.scrollHeight;
  const textareaHeight = Math.max(scrollHeight, target.props.minHeight);

  textarea.style.height = `${textareaHeight}px`;

  frame.style.height = `${textareaHeight}px`;
}

/** The heights the field grew to, taken off again. */
export function resetEditorHeight(target: EditorTarget) {
  if (target.frame.value) {
    target.frame.value.style.height = "";
  }
  if (target.field.value) {
    target.field.value.style.height = "";
  }
}
