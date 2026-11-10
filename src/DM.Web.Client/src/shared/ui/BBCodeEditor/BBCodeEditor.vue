<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  onMounted,
  onUnmounted,
  nextTick,
  getCurrentInstance,
} from "vue";
import { useEditor, EditorContent } from "@tiptap/vue-3";
import { bbcodeToHtml } from "@/shared/lib/bbcode/bbcodeToHtml";
import { htmlToBbcode } from "@/shared/lib/bbcode/htmlToBbcode";
import {
  type BBCodeContext,
  CONTEXT_TAGS,
} from "@/shared/lib/bbcode/bbcodeTagSet";
import InsertPanel from "./InsertPanel.vue";
import MarkupHelpDialog from "./MarkupHelpDialog.vue";
import BBCodeEditorToolbar, {
  type ToolbarButton,
} from "./BBCodeEditorToolbar.vue";
import EditorModeSwitch from "./EditorModeSwitch.vue";
import EditorStatusLine from "./EditorStatusLine.vue";
import MarkupCheck from "./MarkupCheck.vue";
import {
  createEditorExtensions,
  EDITOR_CORE_OPTIONS,
  EDITOR_PARSE_OPTIONS,
  visualEditorProps,
} from "./lib";
import { useEditorDraft } from "./lib/editorDraft";
import { isChord, SOURCE_CHORDS } from "./lib/editorKeys";
import { INSERTIONS, type InsertKind } from "./lib/insertions";
import {
  autoResizeTextarea,
  resetEditorHeight,
  wrapSelection,
  type EditorMode,
  type EditorTarget,
} from "./lib/sourceField";

const props = withDefaults(
  defineProps<{
    modelValue: string;
    context?: BBCodeContext;
    /** The surface the text is saved on, when it is not the drawn one: only the markup check reads it. */
    checkContext?: BBCodeContext;
    placeholder?: string;
    draftKey?: string;
    disabled?: boolean;
    minHeight?: number;
    maxHeight?: number;
    resizable?: boolean;
    isModerator?: boolean;
    maxLength?: number;
  }>(),
  {
    context: "common",
    checkContext: undefined,
    placeholder: "Введите текст",
    draftKey: "",
    disabled: false,
    minHeight: 80,
    maxHeight: 400,
    resizable: true,
    isModerator: false,
    maxLength: 0, // 0 = no limit
  },
);

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
  (e: "submit"): void;
}>();

// The source is what is sent in either mode; the visual mode only previews it.
const mode = ref<EditorMode>("wysiwyg");
const bbcodeText = ref("");
const editorContainer = ref<HTMLElement | null>(null);
const bbcodeTextarea = ref<HTMLTextAreaElement | null>(null);

// The help window. It is built the first time it is asked for and kept after:
// most editors on a page never open it, and a page carries one editor per post
// being edited.
const showHelp = ref(false);
const helpBuilt = ref(false);

function openHelp() {
  helpBuilt.value = true;
  showHelp.value = true;
}

/**
 * Whether Ctrl+Enter sends here: the editor emits "submit" on it everywhere,
 * and it sends only where the parent listens. Half of the forms do not, and a
 * help that promised the chord in all of them would be wrong in half.
 */
const sendsOnChord = Boolean(getCurrentInstance()?.vnode.props?.onSubmit);

/** The panel on screen and the button it hangs under, or null. */
const insertion = ref<{ kind: InsertKind; anchor: HTMLElement } | null>(null);

/**
 * The button of an insertion opens its panel, which hangs from the button,
 * and, pressed again, closes it. The panel of another button closes on the
 * press that opens this one.
 */
function toggleInsertion(kind: InsertKind, event: Event) {
  if (insertion.value?.kind === kind) {
    insertion.value = null;
    return;
  }
  insertion.value = { kind, anchor: event.currentTarget as HTMLElement };
}

function closeInsertion() {
  insertion.value = null;
}

const availableTags = computed(() => CONTEXT_TAGS[props.context]);

/** The tags the toolbar offers here: [mod] to a moderator alone. */
const offeredTags = computed(() =>
  availableTags.value.filter((tag) => tag !== "mod" || props.isModerator),
);

/** Text into the editor, in the visual mode's reading of it. */
function showInVisualMode(bbcode: string) {
  editor.value?.commands.setContent(
    bbcodeToHtml(bbcode, { context: props.context }),
    { emitUpdate: false, parseOptions: EDITOR_PARSE_OPTIONS },
  );
}

const editor = useEditor({
  extensions: createEditorExtensions(props.placeholder),
  coreExtensionOptions: EDITOR_CORE_OPTIONS,
  content: "",
  editable: !props.disabled,
  editorProps: visualEditorProps({
    submit: () => emit("submit"),
    spoilerOffered: () => availableTags.value.includes("spoiler"),
    toggleSpoiler: () => editor.value?.chain().focus().toggleSpoiler().run(),
  }),
  onUpdate: ({ editor }) => {
    if (mode.value === "wysiwyg")
      target.changed(htmlToBbcode(editor.getHTML()));
  },
});

/** Put text into whichever mode is showing, and hand it to the parent. */
function applyContent(content: string) {
  emit("update:modelValue", content);
  if (mode.value === "wysiwyg" && editor.value) {
    showInVisualMode(content);
  } else {
    bbcodeText.value = content;
    nextTick(() => autoResizeTextarea(target));
  }
}

/** This editor as its parts act on it: the commands, the panels, the field. */
const target: EditorTarget = {
  mode,
  editor,
  source: bbcodeText,
  field: bbcodeTextarea,
  frame: editorContainer,
  props,
  changed: (text) => {
    emit("update:modelValue", text);
    saveDraft(text);
  },
  apply: applyContent,
  toggleInsertion,
  closeInsertion,
  openHelp,
  restoreDraft: () => restoreDraft(),
};

const {
  state: draftState,
  saveDraft,
  clearDraft,
  restoreDraft,
} = useEditorDraft(target);

/** The text the status line counts: what the visual mode shows, or the source. */
const countedText = computed(() =>
  mode.value === "wysiwyg" && editor.value
    ? editor.value.getText()
    : bbcodeText.value,
);

watch(
  () => props.modelValue,
  (newValue) => {
    if (mode.value === "wysiwyg" && editor.value) {
      const currentBbcode = htmlToBbcode(editor.value.getHTML());
      if (currentBbcode !== newValue) {
        showInVisualMode(newValue);
      }
    } else if (mode.value === "bbcode") {
      if (bbcodeText.value !== newValue) {
        bbcodeText.value = newValue;
        nextTick(() => autoResizeTextarea(target));
      }
    }
  },
  { immediate: true },
);

watch(
  () => props.disabled,
  (disabled) => {
    editor.value?.setEditable(!disabled);
    // A shut editor takes nothing in: its buttons are off, and so is a panel
    // that was open when the form went to send.
    if (disabled) closeInsertion();
  },
);

function switchMode(newMode: EditorMode) {
  if (newMode === mode.value) return;

  if (editor.value) {
    if (newMode === "bbcode")
      bbcodeText.value = htmlToBbcode(editor.value.getHTML());
    else showInVisualMode(bbcodeText.value);
  }

  mode.value = newMode;

  nextTick(() => {
    if (newMode === "bbcode" && bbcodeTextarea.value) {
      autoResizeTextarea(target);
    } else if (newMode === "wysiwyg") {
      // Reset height that may have been set by BBCode textarea auto-resize
      resetEditorHeight(target);
    }
  });
}

function onBbcodeInput(event: Event) {
  const value = (event.target as HTMLTextAreaElement).value;
  bbcodeText.value = value;
  target.changed(value);
  autoResizeTextarea(target);
}

function handleBbcodeKeydown(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey)) return;
  if (event.key === "Enter") {
    event.preventDefault();
    emit("submit");
    return;
  }
  for (const [letter, shift, before, after] of SOURCE_CHORDS) {
    if (isChord(event, letter, shift)) {
      event.preventDefault();
      wrapSelection(target, before, after);
      return;
    }
  }
}

/** Whether a tag stands pressed: the caret is in what it wrote. */
function isPressed(button: ToolbarButton): boolean | undefined {
  if (!button.mark) return undefined;
  return mode.value === "wysiwyg" && editor.value?.isActive(button.mark);
}

/** The panel's values into the text; the panel goes, the caret is in the text. */
function submitInsertion(values: Record<string, string>) {
  const kind = insertion.value?.kind;
  if (!kind) return;
  closeInsertion();
  INSERTIONS[kind].write(target, values);
}

onMounted(() => {
  if (props.modelValue && editor.value) {
    showInVisualMode(props.modelValue);
  }
});

onUnmounted(() => {
  editor.value?.destroy();
});

defineExpose({
  focus: () => {
    if (mode.value === "wysiwyg") {
      editor.value?.commands.focus("end");
    } else {
      bbcodeTextarea.value?.focus();
    }
  },
  clear: () => {
    emit("update:modelValue", "");
    if (mode.value === "wysiwyg") {
      editor.value?.commands.clearContent();
    } else {
      bbcodeText.value = "";
    }
    clearDraft();
    resetEditorHeight(target);
  },
  clearDraft,
});
</script>

<template>
  <div class="bbcode-editor-wrapper">
    <BBCodeEditorToolbar
      :tags="offeredTags"
      :disabled="disabled"
      :open-panel="insertion?.kind ?? null"
      :is-pressed="isPressed"
      @press="(button, event) => button.act(target, event)"
    >
      <EditorModeSwitch :model-value="mode" @update:model-value="switchMode" />
    </BBCodeEditorToolbar>

    <!-- The panel of an insertion, under its button. It stands in the
         wrapper and not in the bordered box, which clips what overflows it. -->
    <InsertPanel
      v-if="insertion"
      :key="insertion.kind"
      :label="INSERTIONS[insertion.kind].label"
      :fields="INSERTIONS[insertion.kind].fields"
      :anchor="insertion.anchor"
      @submit="submitInsertion"
      @close="closeInsertion"
    />

    <div
      class="bbcode-editor"
      :class="{ disabled, resizable }"
      ref="editorContainer"
      role="region"
      aria-label="Редактор BBCode"
    >
      <div
        class="editor-content"
        :style="{
          minHeight: `${minHeight}px`,
          maxHeight: resizable ? 'none' : `${maxHeight}px`,
        }"
      >
        <EditorContent
          v-show="mode === 'wysiwyg'"
          class="wysiwyg-content"
          :editor="editor"
          aria-label="Визуальный редактор"
        />

        <textarea
          v-show="mode === 'bbcode'"
          ref="bbcodeTextarea"
          class="bbcode-textarea"
          :value="bbcodeText"
          :placeholder="placeholder"
          :disabled="disabled"
          :aria-label="placeholder || 'Редактор BBCode'"
          aria-describedby="bbcode-validation-errors"
          @input="onBbcodeInput"
          @keydown="handleBbcodeKeydown"
        ></textarea>
      </div>

      <MarkupHelpDialog
        v-if="helpBuilt"
        v-model="showHelp"
        :tags="offeredTags"
        :drafts="Boolean(draftKey)"
        :sends="sendsOnChord"
      />
    </div>

    <!-- Outside the resizable area. -->
    <EditorStatusLine
      :text="countedText"
      :max-length="maxLength"
      :draft="draftState"
    />

    <MarkupCheck
      :text="bbcodeText"
      :active="mode === 'bbcode'"
      :context="checkContext ?? context"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *
@use "@/assets/styles/Inputs" as *

.bbcode-editor-wrapper
  position: relative

.bbcode-editor
  position: relative
  display: flex
  flex-direction: column
  background-color: $input-bg-overlay
  border: 1px solid $border
  border-radius: $border-radius

  &:focus-within
    outline: none
    border-color: $link
    box-shadow: 0 0 0 1px $link

  &.disabled
    opacity: $disabled-opacity
    pointer-events: none

  &.resizable
    resize: vertical
    overflow: hidden
    min-height: 120px

    .editor-content
      overflow: auto

.editor-content
  display: flex
  flex-direction: column
  overflow-y: auto

.wysiwyg-content
  box-sizing: border-box
  width: 100%
  height: 100%
  padding: $small $medium
  overflow-x: hidden

  :deep(.tiptap)
    outline: none
    width: 100%
    min-height: 100%
    // Holds the float of the empty-field hint, so the whole hint is the field to tap.
    display: flow-root
    +bbcode-content

    // A hidden block written around blocks stands on the blocks, and its mark
    // is a span that holds them. Drawn in the line, its colour showed only in
    // the gaps between them, and the blocks looked like open text.
    .bb-private:has(> :is(p, div, blockquote, ul, ol))
      display: block

    // Tiptap only marks the empty node. A full-width float stands no line aside: the caret sits on the hint.
    p.is-editor-empty:first-child::before
      content: attr(data-placeholder)
      float: left
      width: 100%
      margin-right: -100%
      pointer-events: none
      +placeholder-ink

.bbcode-textarea
  display: block
  box-sizing: border-box
  width: 100%
  min-height: 100%
  margin: 0
  padding: $small $medium
  border: none
  resize: none
  overflow-y: hidden
  font-family: inherit
  font-size: $font-size
  line-height: 1.5
  background-color: transparent
  color: $text
  outline: none
  box-shadow: none

  &:focus
    box-shadow: none

  &::placeholder
    +placeholder-ink

  &:disabled
    cursor: default
</style>
