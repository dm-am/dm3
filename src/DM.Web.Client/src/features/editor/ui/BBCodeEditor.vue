<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useEditor, EditorContent } from "@tiptap/vue-3";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import Placeholder from "@tiptap/extension-placeholder";
import {
  bbcodeToHtml,
  htmlToBbcode,
  validateBBCode,
  cleanPastedHtml,
  type BBCodeContext,
  CONTEXT_TAGS,
} from "@/shared/lib/utils/bbcode";
import { Tooltip } from "@/shared/ui/Tooltip";
import InputDialog, { type InputField } from "./InputDialog.vue";

// BBCode custom extensions (aligned with DM2 server supported tags)
import {
  Spoiler,
  Nsfw,
  BbTab,
  BbQuote,
  Private,
  Noparse,
  BbLink,
  BbImage,
  ModBlock,
  WarningBlock,
} from "../lib/tiptap-extensions";

const props = withDefaults(
  defineProps<{
    modelValue: string;
    context?: BBCodeContext;
    placeholder?: string;
    draftKey?: string;
    disabled?: boolean;
    minHeight?: number;
    maxHeight?: number;
    resizable?: boolean;
    isModerator?: boolean;
    maxLength?: number;
    showCharCount?: boolean;
  }>(),
  {
    context: "common",
    placeholder: "Введите текст...",
    draftKey: "",
    disabled: false,
    minHeight: 80,
    maxHeight: 400,
    resizable: true,
    isModerator: false,
    maxLength: 0, // 0 = no limit
    showCharCount: true,
  },
);

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
  (e: "submit"): void;
}>();

// Editor mode: 'wysiwyg' (default — visual preview of the BBCode that will
// be sent) or 'bbcode' (raw BBCode source — the authoritative representation
// sent to the server on save, regardless of which mode is active).
const mode = ref<"wysiwyg" | "bbcode">("wysiwyg");
const bbcodeText = ref("");
const editorContainer = ref<HTMLElement | null>(null);
const bbcodeTextarea = ref<HTMLTextAreaElement | null>(null);
const modeTabsRef = ref<HTMLElement | null>(null);

// Indicator position for smooth animation
const indicatorStyle = ref({ left: "0px", width: "50%" });

function updateIndicator() {
  if (!modeTabsRef.value) return;
  const tabs = modeTabsRef.value.querySelectorAll(".mode-tab");
  const activeIndex = mode.value === "bbcode" ? 0 : 1;
  const activeTab = tabs[activeIndex] as HTMLElement;
  if (!activeTab) return;
  indicatorStyle.value = {
    left: `${activeTab.offsetLeft}px`,
    width: `${activeTab.offsetWidth}px`,
  };
}

watch(mode, () => {
  nextTick(updateIndicator);
});

// Draft recovery
const hasDraft = ref(false);
const showHelp = ref(false);

// Draft status indicator
const draftStatus = ref<"idle" | "saving" | "saved">("idle");
const draftSavedAt = ref<number | null>(null);

// Input dialogs (replacing browser prompt())
const showLinkDialog = ref(false);
const showImageDialog = ref(false);
const showPrivateDialog = ref(false);

// Dialog field configurations
const linkDialogFields: InputField[] = [
  {
    name: "url",
    label: "URL ссылки",
    type: "url",
    placeholder: "https://example.com",
    required: true,
  },
  {
    name: "text",
    label: "Текст ссылки (опционально)",
    type: "text",
    placeholder: "Отображаемый текст",
    required: false,
  },
];

const imageDialogFields: InputField[] = [
  {
    name: "url",
    label: "URL изображения",
    type: "url",
    placeholder: "https://example.com/image.jpg",
    required: true,
  },
  {
    name: "width",
    label: "Ширина (px)",
    type: "text",
    placeholder: "600",
    required: false,
    validator: (value: string) => {
      if (value && !/^\d+$/.test(value)) return "Только цифры";
      return null;
    },
  },
  {
    name: "height",
    label: "Высота (px)",
    type: "text",
    placeholder: "400",
    required: false,
    validator: (value: string) => {
      if (value && !/^\d+$/.test(value)) return "Только цифры";
      return null;
    },
  },
  {
    name: "alt",
    label: "Описание (alt)",
    type: "text",
    placeholder: "Описание изображения",
    required: false,
  },
];

const privateDialogFields: InputField[] = [
  {
    name: "character",
    label: "Имя персонажа",
    type: "text",
    placeholder: "Имя персонажа",
    required: true,
    validator: (value: string) => {
      if (!value.trim()) return "Введите имя персонажа";
      return null;
    },
  },
];

// BBCode validation
const validationErrors = ref<string[]>([]);
let validationTimeout: ReturnType<typeof setTimeout> | null = null;

// Character and word count
const charCount = computed(() => {
  if (mode.value === "wysiwyg" && editor.value) {
    return (
      editor.value.storage.characterCount?.characters?.() ??
      editor.value.getText().length
    );
  }
  return bbcodeText.value.length;
});

const wordCount = computed(() => {
  const text =
    mode.value === "wysiwyg" && editor.value
      ? editor.value.getText()
      : bbcodeText.value;
  if (!text.trim()) return 0;
  return text.trim().split(/\s+/).filter(Boolean).length;
});

const isOverLimit = computed(
  () => props.maxLength > 0 && charCount.value > props.maxLength,
);

// Draft status text
const draftStatusText = computed(() => {
  if (draftStatus.value === "saving") return "Сохранение...";
  if (draftStatus.value === "saved" && draftSavedAt.value) {
    const seconds = Math.floor((Date.now() - draftSavedAt.value) / 1000);
    if (seconds < 5) return "Сохранено";
    if (seconds < 60) return `Сохранено ${seconds} сек. назад`;
    const minutes = Math.floor(seconds / 60);
    if (minutes === 1) return "Сохранено 1 мин. назад";
    if (minutes < 5) return `Сохранено ${minutes} мин. назад`;
    return `Сохранено ${minutes} мин. назад`;
  }
  return "";
});

function validateBbcode() {
  if (mode.value !== "bbcode") {
    validationErrors.value = [];
    return;
  }
  validationErrors.value = validateBBCode(bbcodeText.value);
}

// Debounced validation to avoid running on every keystroke
function debouncedValidation() {
  if (validationTimeout) {
    clearTimeout(validationTimeout);
  }
  validationTimeout = setTimeout(validateBbcode, 300);
}

// Watch for bbcode changes in bbcode mode
watch([bbcodeText, mode], () => {
  if (mode.value === "bbcode") {
    debouncedValidation();
  } else {
    validationErrors.value = [];
  }
});

// Available tags for current context
const availableTags = computed(() => CONTEXT_TAGS[props.context]);

// Custom clipboard text serializer - single newlines between paragraphs
// By default, browsers add double newlines between <p> tags when copying
function serializeNodeToText(node: any, isFirst: boolean = true): string {
  let text = "";

  if (node.isText) {
    return node.text || "";
  }

  if (node.isTextblock) {
    // Paragraph, heading, code block, etc.
    if (!isFirst) text += "\n";
    node.content?.forEach((child: any) => {
      text += serializeNodeToText(child, true);
    });
    return text;
  }

  if (node.type?.name === "hardBreak") {
    return "\n";
  }

  // For other nodes, recursively process children
  let childIndex = 0;
  node.content?.forEach((child: any) => {
    text += serializeNodeToText(child, childIndex === 0);
    childIndex++;
  });

  return text;
}

// Tiptap editor
const editor = useEditor({
  extensions: [
    StarterKit.configure({
      heading: false, // BBCode doesn't support headings
      horizontalRule: false, // [cut] marker was removed — truncation is now height-based
      blockquote: false, // We use custom BbQuote instead
      codeBlock: false, // We use inline code only, no code blocks
    }),
    Underline,
    Placeholder.configure({
      placeholder: props.placeholder,
    }),
    // BBCode custom extensions (aligned with DM2 server)
    Spoiler,
    Nsfw,
    BbTab,
    BbQuote,
    Private,
    Noparse,
    BbLink,
    BbImage,
    ModBlock,
    WarningBlock,
  ],
  content: "",
  editable: !props.disabled,
  editorProps: {
    // Clean pasted content from Word, Google Docs, etc.
    transformPastedHTML: (html: string) => {
      return cleanPastedHtml(html);
    },
    // Custom clipboard serializer - single newlines between paragraphs
    clipboardTextSerializer: (slice) => {
      let text = "";
      let isFirst = true;
      slice.content.forEach((node: any) => {
        text += serializeNodeToText(node, isFirst);
        isFirst = false;
      });
      return text;
    },
  },
  onUpdate: ({ editor }) => {
    if (mode.value === "wysiwyg") {
      const html = editor.getHTML();
      const bbcode = htmlToBbcode(html);
      emit("update:modelValue", bbcode);
      saveDraft(bbcode);
    }
  },
});

// Initialize content from modelValue
watch(
  () => props.modelValue,
  (newValue) => {
    if (mode.value === "wysiwyg" && editor.value) {
      const currentBbcode = htmlToBbcode(editor.value.getHTML());
      if (currentBbcode !== newValue) {
        const html = bbcodeToHtml(newValue);
        editor.value.commands.setContent(html, { emitUpdate: false });
      }
    } else if (mode.value === "bbcode") {
      if (bbcodeText.value !== newValue) {
        bbcodeText.value = newValue;
        nextTick(() => autoResizeTextarea());
      }
    }
  },
  { immediate: true },
);

// Watch disabled state
watch(
  () => props.disabled,
  (disabled) => {
    editor.value?.setEditable(!disabled);
  },
);

// Switch between modes
function switchMode(newMode: "wysiwyg" | "bbcode") {
  if (newMode === mode.value) return;

  if (newMode === "bbcode") {
    // WYSIWYG -> BBCode
    if (editor.value) {
      bbcodeText.value = htmlToBbcode(editor.value.getHTML());
    }
  } else {
    // BBCode -> WYSIWYG
    if (editor.value) {
      const html = bbcodeToHtml(bbcodeText.value);
      editor.value.commands.setContent(html, { emitUpdate: false });
    }
  }

  mode.value = newMode;

  nextTick(() => {
    if (newMode === "bbcode" && bbcodeTextarea.value) {
      autoResizeTextarea();
    } else if (newMode === "wysiwyg") {
      // Reset height that may have been set by BBCode textarea auto-resize
      resetEditorHeight();
    }
  });
}

// BBCode textarea handlers
function onBbcodeInput(event: Event) {
  const target = event.target as HTMLTextAreaElement;
  bbcodeText.value = target.value;
  emit("update:modelValue", target.value);
  saveDraft(target.value);
  autoResizeTextarea();
}

function autoResizeTextarea() {
  if (!bbcodeTextarea.value || !editorContainer.value) return;

  // Reset heights to measure natural content height
  bbcodeTextarea.value.style.height = "auto";
  editorContainer.value.style.height = "auto";

  // Get textarea content height (includes padding)
  const scrollHeight = bbcodeTextarea.value.scrollHeight;
  const textareaHeight = Math.max(scrollHeight, props.minHeight);

  // Set textarea height
  bbcodeTextarea.value.style.height = `${textareaHeight}px`;

  // Calculate total editor height (textarea + toolbar)
  const toolbar = editorContainer.value.querySelector(".editor-toolbar");
  const toolbarHeight = toolbar ? toolbar.getBoundingClientRect().height : 0;
  const editorHeight = textareaHeight + toolbarHeight;

  // Set editor container height
  editorContainer.value.style.height = `${editorHeight}px`;
}

// ═══════════════════════════════════════════════════════════════════════════════
// TOOLBAR ACTIONS
// ═══════════════════════════════════════════════════════════════════════════════
//
// Design decision: These toggle functions intentionally follow a similar pattern
// but are NOT abstracted into a configuration-driven helper. Reasons:
//
// 1. EXCEPTIONS: 5 functions (link, image, private, cut, tab) have significantly
//    different behavior (dialogs, self-closing tags, parameters) that would
//    create a leaky abstraction if forced into a common pattern.
//
// 2. READABILITY: Each function is immediately understandable - you can see
//    exactly what BBCode tag maps to what WYSIWYG command without indirection.
//
// 3. DEBUGGING: When a specific tag breaks, you know exactly where to look.
//    No need to trace through abstraction layers.
//
// 4. LOW CHANGE FREQUENCY: Tags rarely change after initial implementation.
//    The maintenance burden of duplication is minimal.
//
// See BBCODE_PIPELINE.md "Why Toolbar Functions Are Not Fully Abstracted" for details.
// ═══════════════════════════════════════════════════════════════════════════════

function toggleBold() {
  if (mode.value === "bbcode") {
    wrapSelection("[b]", "[/b]");
  } else {
    editor.value?.chain().focus().toggleBold().run();
  }
}

function toggleItalic() {
  if (mode.value === "bbcode") {
    wrapSelection("[i]", "[/i]");
  } else {
    editor.value?.chain().focus().toggleItalic().run();
  }
}

function toggleUnderline() {
  if (mode.value === "bbcode") {
    wrapSelection("[u]", "[/u]");
  } else {
    editor.value?.chain().focus().toggleUnderline().run();
  }
}

function toggleStrike() {
  if (mode.value === "bbcode") {
    wrapSelection("[strike]", "[/strike]");
  } else {
    editor.value?.chain().focus().toggleStrike().run();
  }
}

function toggleCode() {
  if (mode.value === "bbcode") {
    wrapSelection("[code]", "[/code]");
  } else {
    editor.value?.chain().focus().toggleCode().run();
  }
}

function toggleBulletList() {
  if (mode.value === "bbcode") {
    wrapSelection("[ul]\n[li]", "[/li]\n[/ul]");
  } else {
    editor.value?.chain().focus().toggleBulletList().run();
  }
}

function toggleOrderedList() {
  if (mode.value === "bbcode") {
    wrapSelection("[ol]\n[li]", "[/li]\n[/ol]");
  } else {
    editor.value?.chain().focus().toggleOrderedList().run();
  }
}

function toggleBlockquote() {
  if (mode.value === "bbcode") {
    wrapSelection("[quote]", "[/quote]");
  } else {
    editor.value?.chain().focus().toggleQuote().run();
  }
}

function insertLink() {
  showLinkDialog.value = true;
}

function handleLinkSubmit(values: Record<string, string>) {
  const url = values.url?.trim();
  const text = values.text?.trim();

  if (!url) return;

  if (mode.value === "bbcode") {
    const textarea = bbcodeTextarea.value;
    if (!textarea) return;

    const pos = textarea.selectionStart;
    // DM3 format: [link=text]URL[/link] or [link]URL[/link]
    let linkTag: string;
    if (text) {
      linkTag = `[link=${text}]${url}[/link]`;
    } else {
      linkTag = `[link]${url}[/link]`;
    }
    bbcodeText.value =
      bbcodeText.value.substring(0, pos) +
      linkTag +
      bbcodeText.value.substring(pos);
    emit("update:modelValue", bbcodeText.value);
    saveDraft(bbcodeText.value);
    nextTick(() => {
      textarea.focus();
      textarea.setSelectionRange(pos + linkTag.length, pos + linkTag.length);
    });
  } else {
    // Use setBbLink with optional text - it handles both cases now
    editor.value
      ?.chain()
      .focus()
      .setBbLink({ href: url, text: text || undefined })
      .run();
  }
}

function insertImage() {
  showImageDialog.value = true;
}

function handleImageSubmit(values: Record<string, string>) {
  const url = values.url?.trim();
  if (!url) return;

  const width = values.width?.trim();
  const height = values.height?.trim();
  const alt = values.alt?.trim();

  // Build image tag with optional size and alt
  let imgTag: string;
  const sizeParam = width && height ? `${width}x${height}` : width || "";
  const altParam = alt ? ` alt="${alt}"` : "";

  if (sizeParam && altParam) {
    imgTag = `[img=${sizeParam}${altParam}]${url}[/img]`;
  } else if (sizeParam) {
    imgTag = `[img=${sizeParam}]${url}[/img]`;
  } else if (altParam) {
    imgTag = `[img${altParam}]${url}[/img]`;
  } else {
    imgTag = `[img]${url}[/img]`;
  }

  if (mode.value === "bbcode") {
    const textarea = bbcodeTextarea.value;
    if (!textarea) return;
    const pos = textarea.selectionStart;
    bbcodeText.value =
      bbcodeText.value.substring(0, pos) +
      imgTag +
      bbcodeText.value.substring(pos);
    emit("update:modelValue", bbcodeText.value);
    saveDraft(bbcodeText.value);
    nextTick(() => {
      textarea.focus();
      textarea.setSelectionRange(pos + imgTag.length, pos + imgTag.length);
    });
  } else {
    // For Tiptap mode, just insert BBCode - it will be converted
    editor.value?.chain().focus().setBbImage({ src: url }).run();
  }
}

function insertSpoiler() {
  if (mode.value === "bbcode") {
    wrapSelection("[spoiler]", "[/spoiler]");
  } else {
    editor.value?.chain().focus().toggleSpoiler().run();
  }
}

function insertPrivate() {
  showPrivateDialog.value = true;
}

function handlePrivateSubmit(values: Record<string, string>) {
  const character = values.character?.trim();
  if (!character) return;

  if (mode.value === "bbcode") {
    wrapSelection(`[private=${character}]`, "[/private]");
  } else {
    editor.value?.chain().focus().togglePrivate({ character }).run();
  }
}

function insertTab() {
  if (mode.value === "bbcode") {
    const textarea = bbcodeTextarea.value;
    if (!textarea) return;
    const pos = textarea.selectionStart;
    bbcodeText.value =
      bbcodeText.value.substring(0, pos) +
      "[tab]" +
      bbcodeText.value.substring(pos);
    emit("update:modelValue", bbcodeText.value);
    saveDraft(bbcodeText.value);
    nextTick(() => {
      textarea.focus();
      textarea.setSelectionRange(pos + 5, pos + 5);
    });
  } else {
    editor.value?.chain().focus().insertTab().run();
  }
}

function insertNsfw() {
  if (mode.value === "bbcode") {
    wrapSelection("[nsfw]", "[/nsfw]");
  } else {
    editor.value?.chain().focus().toggleNsfw().run();
  }
}

function insertNoparse() {
  if (mode.value === "bbcode") {
    wrapSelection("[noparse]", "[/noparse]");
  } else {
    editor.value?.chain().focus().toggleNoparse().run();
  }
}

function insertMod() {
  if (mode.value === "bbcode") {
    wrapSelection("[mod]", "[/mod]");
  } else {
    editor.value?.chain().focus().toggleModBlock().run();
  }
}

function insertWarning() {
  if (mode.value === "bbcode") {
    wrapSelection("[warning]", "[/warning]");
  } else {
    editor.value?.chain().focus().toggleWarningBlock().run();
  }
}

function loadDraftManual() {
  const draft = loadDraft();
  if (draft) {
    restoreDraft();
  }
}

// Draft management with expiration
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const _DRAFT_SAVE_INTERVAL = 5000; // 5 seconds - TODO: implement auto-save interval
const DRAFT_EXPIRATION_MS = 7 * 24 * 60 * 60 * 1000; // 7 days
let draftInterval: ReturnType<typeof setInterval> | null = null;
let lastSavedDraft = "";

interface DraftData {
  content: string;
  timestamp: number;
}

function getDraftKey(): string {
  return props.draftKey ? `bbcode_draft_${props.draftKey}` : "";
}

let draftStatusTimeout: ReturnType<typeof setTimeout> | null = null;

function saveDraft(content: string) {
  const key = getDraftKey();
  if (!key || content === lastSavedDraft) return;

  // Show saving indicator
  draftStatus.value = "saving";

  if (content.trim()) {
    const draftData: DraftData = {
      content,
      timestamp: Date.now(),
    };
    try {
      localStorage.setItem(key, JSON.stringify(draftData));
      lastSavedDraft = content;
      draftSavedAt.value = Date.now();
      draftStatus.value = "saved";
    } catch (e) {
      // localStorage might be full or disabled
      console.warn("Failed to save draft:", e);
      draftStatus.value = "idle";
    }
  } else {
    localStorage.removeItem(key);
    lastSavedDraft = "";
    draftStatus.value = "idle";
    draftSavedAt.value = null;
  }

  // Reset status after 30 seconds of inactivity
  if (draftStatusTimeout) clearTimeout(draftStatusTimeout);
  draftStatusTimeout = setTimeout(() => {
    if (draftStatus.value === "saved") {
      // Keep showing "saved" but refresh the time display
    }
  }, 30000);
}

function loadDraft(): string | null {
  const key = getDraftKey();
  if (!key) return null;

  const raw = localStorage.getItem(key);
  if (!raw) return null;

  try {
    const draftData: DraftData = JSON.parse(raw);
    // Check if draft is expired
    if (Date.now() - draftData.timestamp > DRAFT_EXPIRATION_MS) {
      localStorage.removeItem(key);
      return null;
    }
    return draftData.content;
  } catch {
    // Plain string format - return as-is
    return raw;
  }
}

// Clean up all expired drafts on mount
function cleanupExpiredDrafts() {
  const keysToRemove: string[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key?.startsWith("bbcode_draft_")) {
      const raw = localStorage.getItem(key);
      if (raw) {
        try {
          const draftData: DraftData = JSON.parse(raw);
          if (Date.now() - draftData.timestamp > DRAFT_EXPIRATION_MS) {
            keysToRemove.push(key);
          }
        } catch {
          // Plain string format - keep it
        }
      }
    }
  }
  keysToRemove.forEach((key) => localStorage.removeItem(key));
}

function clearDraft() {
  const key = getDraftKey();
  if (key) {
    localStorage.removeItem(key);
    lastSavedDraft = "";
  }
}

function restoreDraft() {
  const draft = loadDraft();
  if (draft) {
    emit("update:modelValue", draft);
    if (mode.value === "wysiwyg" && editor.value) {
      const html = bbcodeToHtml(draft);
      editor.value.commands.setContent(html, { emitUpdate: false });
    } else {
      bbcodeText.value = draft;
      nextTick(() => autoResizeTextarea());
    }
  }
  hasDraft.value = false;
}

// Check for existing draft on mount
onMounted(() => {
  // Clean up expired drafts from all keys
  cleanupExpiredDrafts();

  const draft = loadDraft();
  if (draft && draft.trim() && draft !== props.modelValue) {
    hasDraft.value = true;
  }

  // Set initial content
  if (props.modelValue && editor.value) {
    const html = bbcodeToHtml(props.modelValue);
    editor.value.commands.setContent(html, { emitUpdate: false });
  }

  // Auto-resize textarea if in BBCode mode with content
  if (mode.value === "bbcode" && props.modelValue) {
    bbcodeText.value = props.modelValue;
    nextTick(() => autoResizeTextarea());
  }

  // Initialize mode indicator position
  nextTick(updateIndicator);
});

onUnmounted(() => {
  if (draftInterval) {
    clearInterval(draftInterval);
  }
  editor.value?.destroy();
});

// Keyboard shortcuts for BBCode mode
function handleBbcodeKeydown(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey) {
    switch (event.key.toLowerCase()) {
      case "b":
        event.preventDefault();
        wrapSelection("[b]", "[/b]");
        break;
      case "i":
        event.preventDefault();
        wrapSelection("[i]", "[/i]");
        break;
      case "u":
        event.preventDefault();
        wrapSelection("[u]", "[/u]");
        break;
      case "enter":
        event.preventDefault();
        emit("submit");
        break;
    }
  }
  if (event.key === "Enter" && !event.shiftKey && !event.ctrlKey) {
    // Allow shift+enter for newlines
  }
}

function wrapSelection(before: string, after: string) {
  const textarea = bbcodeTextarea.value;
  if (!textarea) return;

  const start = textarea.selectionStart;
  const end = textarea.selectionEnd;
  const selected = bbcodeText.value.substring(start, end);
  const newText = before + selected + after;

  bbcodeText.value =
    bbcodeText.value.substring(0, start) +
    newText +
    bbcodeText.value.substring(end);
  emit("update:modelValue", bbcodeText.value);
  saveDraft(bbcodeText.value);

  nextTick(() => {
    textarea.focus();
    textarea.setSelectionRange(
      start + before.length,
      start + before.length + selected.length,
    );
  });
}

// Reset editor height to default
function resetEditorHeight() {
  if (editorContainer.value) {
    editorContainer.value.style.height = "";
  }
  if (bbcodeTextarea.value) {
    bbcodeTextarea.value.style.height = "";
  }
}

// Expose methods for parent components
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
    resetEditorHeight();
  },
  clearDraft,
});
</script>

<template>
  <div class="bbcode-editor-wrapper">
    <!-- Popups (outside overflow container) -->

    <!-- Toolbar (outside the bordered container) -->
    <div
      class="editor-toolbar"
      role="toolbar"
      aria-label="Панель форматирования"
    >
      <!-- Text formatting -->
      <Tooltip v-if="availableTags.includes('b')" text="Жирный (Ctrl+B)">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('bold') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('bold')"
          :disabled="disabled"
          @click="toggleBold"
          aria-label="Жирный"
        >
          <b>b</b>
        </button>
      </Tooltip>
      <Tooltip v-if="availableTags.includes('i')" text="Курсив (Ctrl+I)">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('italic') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('italic')"
          :disabled="disabled"
          @click="toggleItalic"
          aria-label="Курсив"
        >
          <i>i</i>
        </button>
      </Tooltip>
      <Tooltip v-if="availableTags.includes('u')" text="Подчеркнутый (Ctrl+U)">
        <button
          type="button"
          class="tag-btn"
          :class="{
            active: mode === 'wysiwyg' && editor?.isActive('underline'),
          }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('underline')"
          :disabled="disabled"
          @click="toggleUnderline"
          aria-label="Подчеркнутый"
        >
          <u>u</u>
        </button>
      </Tooltip>
      <Tooltip v-if="availableTags.includes('strike')" text="Зачеркнутый">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('strike') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('strike')"
          :disabled="disabled"
          @click="toggleStrike"
          aria-label="Зачеркнутый"
        >
          <s>strike</s>
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Hidden/conditional visibility -->
      <Tooltip v-if="availableTags.includes('spoiler')" text="Спойлер">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('spoiler') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('spoiler')"
          :disabled="disabled"
          @click="insertSpoiler"
          aria-label="Спойлер"
        >
          spoiler
        </button>
      </Tooltip>
      <Tooltip v-if="availableTags.includes('nsfw')" text="18+ контент">
        <button
          type="button"
          class="tag-btn tag-btn-nsfw"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('nsfw') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('nsfw')"
          :disabled="disabled"
          @click="insertNsfw"
          aria-label="NSFW контент"
        >
          nsfw
        </button>
      </Tooltip>
      <Tooltip
        v-if="availableTags.includes('private')"
        text="Приватный текст (видно только указанному персонажу)"
      >
        <button
          type="button"
          class="tag-btn tag-btn-private"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('private') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('private')"
          :disabled="disabled"
          @click="insertPrivate"
          aria-label="Приватный текст"
        >
          private
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Lists -->
      <Tooltip v-if="availableTags.includes('ul')" text="Маркированный список">
        <button
          type="button"
          class="tag-btn"
          :class="{
            active: mode === 'wysiwyg' && editor?.isActive('bulletList'),
          }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('bulletList')"
          :disabled="disabled"
          @click="toggleBulletList"
          aria-label="Маркированный список"
        >
          ul
        </button>
      </Tooltip>
      <Tooltip v-if="availableTags.includes('ol')" text="Нумерованный список">
        <button
          type="button"
          class="tag-btn"
          :class="{
            active: mode === 'wysiwyg' && editor?.isActive('orderedList'),
          }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('orderedList')"
          :disabled="disabled"
          @click="toggleOrderedList"
          aria-label="Нумерованный список"
        >
          ol
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Media -->
      <Tooltip v-if="availableTags.includes('img')" text="Вставить изображение">
        <button
          type="button"
          class="tag-btn"
          :disabled="disabled"
          @click="insertImage"
          aria-label="Вставить изображение"
        >
          img
        </button>
      </Tooltip>
      <Tooltip v-if="availableTags.includes('link')" text="Вставить ссылку">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('bbLink') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('bbLink')"
          :disabled="disabled"
          @click="insertLink"
          aria-label="Вставить ссылку"
        >
          link
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Blocks -->
      <Tooltip v-if="availableTags.includes('quote')" text="Цитата">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('bbQuote') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('bbQuote')"
          :disabled="disabled"
          @click="toggleBlockquote"
          aria-label="Цитата"
        >
          quote
        </button>
      </Tooltip>
      <Tooltip
        v-if="availableTags.includes('warning') && isModerator"
        text="Предупреждение"
      >
        <button
          type="button"
          class="tag-btn tag-btn-warning"
          :class="{
            active: mode === 'wysiwyg' && editor?.isActive('warningBlock'),
          }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('warningBlock')"
          :disabled="disabled"
          @click="insertWarning"
          aria-label="Предупреждение"
        >
          warning
        </button>
      </Tooltip>
      <Tooltip
        v-if="availableTags.includes('mod') && isModerator"
        text="Модераторский блок"
      >
        <button
          type="button"
          class="tag-btn tag-btn-mod"
          :class="{
            active: mode === 'wysiwyg' && editor?.isActive('modBlock'),
          }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('modBlock')"
          :disabled="disabled"
          @click="insertMod"
          aria-label="Модераторский блок"
        >
          mod
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Layout -->
      <Tooltip v-if="availableTags.includes('tab')" text="Отступ">
        <button
          type="button"
          class="tag-btn"
          :disabled="disabled"
          @click="insertTab"
          aria-label="Вставить отступ"
        >
          tab
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Code/Raw -->
      <Tooltip v-if="availableTags.includes('code')" text="Моноширинный код">
        <button
          type="button"
          class="tag-btn tag-btn-code"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('code') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('code')"
          :disabled="disabled"
          @click="toggleCode"
          aria-label="Код"
        >
          code
        </button>
      </Tooltip>
      <Tooltip
        v-if="availableTags.includes('noparse')"
        text="Без обработки BBCode"
      >
        <button
          type="button"
          class="tag-btn"
          :class="{ active: mode === 'wysiwyg' && editor?.isActive('noparse') }"
          :aria-pressed="mode === 'wysiwyg' && editor?.isActive('noparse')"
          :disabled="disabled"
          @click="insertNoparse"
          aria-label="Без форматирования"
        >
          noparse
        </button>
      </Tooltip>
      <span
        class="toolbar-separator"
        role="separator"
        aria-orientation="vertical"
      ></span>
      <!-- Utils -->
      <Tooltip text="Загрузить черновик">
        <button
          type="button"
          class="tag-btn"
          :disabled="disabled"
          @click="loadDraftManual"
          aria-label="Загрузить черновик"
        >
          load
        </button>
      </Tooltip>
      <Tooltip text="Справка">
        <button
          type="button"
          class="tag-btn"
          :class="{ active: showHelp }"
          :aria-pressed="showHelp"
          :aria-expanded="showHelp"
          @click="showHelp = !showHelp"
          aria-label="Показать справку"
        >
          help
        </button>
      </Tooltip>
      <!-- Spacer to push mode toggle to the right -->
      <span class="toolbar-spacer"></span>
      <!-- Mode toggle -->
      <div ref="modeTabsRef" class="mode-tabs">
        <Tooltip text="Режим BBCode">
          <button
            type="button"
            class="mode-tab"
            :class="{ active: mode === 'bbcode' }"
            @click="switchMode('bbcode')"
            aria-label="Режим BBCode"
          >
            <small>[bbcode]</small>
          </button>
        </Tooltip>
        <Tooltip text="Визуальный редактор">
          <button
            type="button"
            class="mode-tab"
            :class="{ active: mode === 'wysiwyg' }"
            @click="switchMode('wysiwyg')"
            aria-label="Визуальный редактор"
          >
            <small><i>wysiwyg</i></small>
          </button>
        </Tooltip>
        <span class="mode-indicator" :style="indicatorStyle"></span>
      </div>
    </div>

    <!-- Help dialog (non-modal) -->
    <Teleport to="body">
      <Transition name="help-dialog">
        <div
          v-if="showHelp"
          class="help-dialog"
          role="dialog"
          aria-label="Справка по BBCode"
        >
          <div class="help-dialog-header">
            <span class="help-dialog-title">Справка по BBCode</span>
            <button
              type="button"
              class="help-dialog-close"
              @click="showHelp = false"
              aria-label="Закрыть"
            >
              {{ symbols.close }}
            </button>
          </div>
          <div class="help-dialog-body">
            <div class="help-grid">
              <!-- Форматирование -->
              <section class="help-section">
                <h4 class="help-section-title">Форматирование</h4>
                <div class="help-item">
                  <code>[b]</code>текст<code>[/b]</code> — <b>жирный</b>
                  <kbd>Ctrl+B</kbd>
                </div>
                <div class="help-item">
                  <code>[i]</code>текст<code>[/i]</code> — <i>курсив</i>
                  <kbd>Ctrl+I</kbd>
                </div>
                <div class="help-item">
                  <code>[u]</code>текст<code>[/u]</code> — <u>подчеркнутый</u>
                  <kbd>Ctrl+U</kbd>
                </div>
                <div class="help-item">
                  <code>[strike]</code>текст<code>[/strike]</code> —
                  <s>зачеркнутый</s>
                </div>
              </section>

              <!-- Скрытый контент -->
              <section class="help-section">
                <h4 class="help-section-title">Скрытый контент</h4>
                <div class="help-item">
                  <code>[spoiler]</code>текст<code>[/spoiler]</code> — скрыть
                  под спойлер
                </div>
                <div class="help-item help-item-nsfw">
                  <code>[nsfw]</code>текст<code>[/nsfw]</code> — контент 18+
                </div>
                <div class="help-item help-item-private">
                  <code>[private=Имя]</code>текст<code>[/private]</code> —
                  только для персонажа
                </div>
              </section>

              <!-- Списки -->
              <section class="help-section">
                <h4 class="help-section-title">Списки</h4>
                <div class="help-item">
                  <code>[ul][li]</code>пункт<code>[/li][/ul]</code> —
                  маркированный
                </div>
                <div class="help-item">
                  <code>[ol][li]</code>пункт<code>[/li][/ol]</code> —
                  нумерованный
                </div>
              </section>

              <!-- Медиа -->
              <section class="help-section">
                <h4 class="help-section-title">Медиа</h4>
                <div class="help-item">
                  <code>[img]</code>url<code>[/img]</code> — изображение
                </div>
                <div class="help-item">
                  <code>[link]</code>url<code>[/link]</code> — ссылка
                </div>
                <div class="help-item">
                  <code>[link=текст]</code>url<code>[/link]</code> — ссылка с
                  текстом
                </div>
              </section>

              <!-- Блоки -->
              <section class="help-section">
                <h4 class="help-section-title">Блоки</h4>
                <div class="help-item">
                  <code>[quote]</code>текст<code>[/quote]</code> — цитата
                </div>
                <div class="help-item help-item-code">
                  <code>[code]</code>текст<code>[/code]</code> — моноширинный
                  код
                </div>
                <div v-if="isModerator" class="help-item help-item-warning">
                  <code>[warning]</code>текст<code>[/warning]</code> —
                  предупреждение
                </div>
                <div v-if="isModerator" class="help-item help-item-mod">
                  <code>[mod]</code>текст<code>[/mod]</code> — модераторский
                  блок
                </div>
              </section>

              <!-- Разное -->
              <section class="help-section">
                <h4 class="help-section-title">Разное</h4>
                <div class="help-item">
                  <code>[tab]</code> — отступ (красная строка)
                </div>
                <div class="help-item">
                  <code>[noparse]</code>текст<code>[/noparse]</code> — без
                  обработки
                </div>
              </section>
            </div>

            <div class="help-footer">
              <kbd>Ctrl+Enter</kbd> — отправить сообщение
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>

    <div
      class="bbcode-editor"
      :class="{ disabled, resizable }"
      ref="editorContainer"
      role="region"
      aria-label="Редактор BBCode"
    >
      <!-- Editor content -->
      <div
        class="editor-content"
        :style="{
          minHeight: `${minHeight}px`,
          maxHeight: resizable ? 'none' : `${maxHeight}px`,
        }"
      >
        <!-- WYSIWYG mode -->
        <EditorContent
          v-show="mode === 'wysiwyg'"
          class="wysiwyg-content"
          :editor="editor"
          aria-label="Визуальный редактор"
        />

        <!-- BBCode mode -->
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

      <!-- Input dialogs (use Teleport internally, can stay here) -->
      <InputDialog
        v-model:show="showLinkDialog"
        title="Вставить ссылку"
        :fields="linkDialogFields"
        submit-label="Вставить"
        @submit="handleLinkSubmit"
      />

      <InputDialog
        v-model:show="showImageDialog"
        title="Вставить изображение"
        :fields="imageDialogFields"
        submit-label="Вставить"
        @submit="handleImageSubmit"
      />

      <InputDialog
        v-model:show="showPrivateDialog"
        title="Приватный текст"
        :fields="privateDialogFields"
        submit-label="Вставить"
        @submit="handlePrivateSubmit"
      />
    </div>

    <!-- Status bar - outside resizable area -->
    <div
      v-if="showCharCount"
      class="status-bar"
      role="status"
      aria-live="polite"
    >
      <span class="status-item"
        >{{ wordCount }}
        {{
          wordCount === 1
            ? "слово"
            : wordCount >= 2 && wordCount <= 4
              ? "слова"
              : "слов"
        }}</span
      >
      <span class="status-separator">|</span>
      <span class="status-item" :class="{ 'over-limit': isOverLimit }">
        {{ charCount }}{{ maxLength > 0 ? ` / ${maxLength}` : "" }} символов
      </span>
      <span v-if="hasDraft && draftKey" class="status-separator">|</span>
      <span v-if="hasDraft && draftKey" class="status-item draft-available"
        >Есть черновик</span
      >
      <span
        v-if="draftStatusText && draftKey && !hasDraft"
        class="status-separator"
        >|</span
      >
      <span
        v-if="draftStatusText && draftKey && !hasDraft"
        class="status-item draft-status"
        :class="{ saving: draftStatus === 'saving' }"
      >
        {{ draftStatusText }}
      </span>
    </div>

    <!-- BBCode validation errors - outside resizable area -->
    <div
      v-if="mode === 'bbcode' && validationErrors.length > 0"
      id="bbcode-validation-errors"
      class="validation-errors"
      role="alert"
      aria-live="polite"
      aria-atomic="true"
    >
      <div
        class="validation-error"
        v-for="error in validationErrors"
        :key="error"
      >
        <span class="error-icon" aria-hidden="true">{{ symbols.warning }}</span>
        <span class="error-text">{{ error }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/ZIndex"

.bbcode-editor-wrapper
  position: relative

.bbcode-editor
  position: relative
  display: flex
  flex-direction: column
  background-color: $input-bg-overlay
  border: 1px dashed $border

  &:focus-within
    outline: none
    border-style: solid
    border-color: $border-focus

  &.disabled
    opacity: 0.6
    pointer-events: none

  &.resizable
    resize: vertical
    overflow: hidden
    min-height: 120px

    .editor-content
      overflow: auto

.editor-toolbar
  display: flex
  align-items: center
  flex-wrap: wrap
  gap: 4px
  padding: $small 0
  flex-shrink: 0

.toolbar-spacer
  flex: 1

.mode-tabs
  display: flex
  border-bottom: 2px solid $border
  position: relative

.mode-tab
  padding: $tiny $small
  border: none
  background: none
  color: $text-muted
  font-family: inherit
  font-size: $secondary-font-size
  cursor: pointer
  transition: filter 0.2s ease

  &:hover:not(.active)
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)
    cursor: default

.mode-indicator
  position: absolute
  bottom: -2px
  height: 2px
  background-color: $text-muted
  transition: left 0.25s ease, width 0.25s ease
  pointer-events: none

.toolbar-separator
  width: 1px
  height: 18px
  background-color: $border
  margin: 0 2px

.tag-btn
  padding: 2px 6px
  border: 1px solid
  border-color: $border
  border-radius: 2px
  background: transparent
  cursor: pointer
  font-size: 12px
  font-family: inherit
  color: $text

  &:hover:not(:disabled)
    background-color: $bg-element-accent

  &:disabled
    opacity: 0.4
    cursor: default

  &.active
    background-color: $bg-element-accent
    font-weight: 600

  &.tag-btn-code
    font-family: $code-font
    padding-top: 3px
    padding-bottom: 1px

.tag-btn-private
  color: $accent-green

.tag-btn-nsfw
  color: $accent-red

.tag-btn-mod
  color: $accent-green

.tag-btn-warning
  color: $accent-red

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
    line-height: 1.5
    +bbcode-content-editor

    // Tiptap-specific: paragraph spacing
    p
      margin: 0
    p + p
      margin-top: $small

    // Empty paragraphs (Tiptap cursor placeholders) - minimal height
    p:empty,
    p > br:only-child
      margin: 0
      line-height: 0.5

    // Tiptap-specific: horizontal rule (rare in BBCode, kept for any
    // legacy <hr> rendering inside the editor)
    hr
      border: none
      border-top: 2px dashed
      border-color: $border
      margin: $medium 0

    // Tiptap placeholder
    .ProseMirror-placeholder
      color: $text-muted
      opacity: 0.6

    // Selection highlight
    ::selection
      background-color: $selection-bg
      color: $selection-text
    ::-moz-selection
      background-color: $selection-bg
      color: $selection-text

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
    color: $text-muted
    opacity: 0.6

  &:disabled
    cursor: default

  &::selection
    background-color: $selection-bg
    color: $selection-text
  &::-moz-selection
    background-color: $selection-bg
    color: $selection-text

// Status bar - outside the bordered container
.status-bar
  display: flex
  align-items: center
  gap: $small
  padding: $tiny 0
  font-size: 11px
  color: $text-muted

.status-separator
  opacity: 0.5

.status-item
  &.over-limit
    color: $accent-red
    font-weight: 600

.draft-available
  color: $accent-red-muted

.draft-status
  display: flex
  align-items: center
  gap: 4px
  color: $accent-green-muted

  &.saving
    color: $text-muted

.validation-errors
  padding: $small $medium
  background-color: $bg-highlight-blue
  border-top: 1px solid
  border-color: $border

.validation-error
  display: flex
  align-items: center
  gap: $small
  padding: 2px 0
  font-size: $secondary-font-size
  color: $accent-red

  .error-icon
    font-size: 12px

  .error-text
    flex: 1

// Help dialog (non-modal, teleported to body - use :global)
:global(.help-dialog)
  position: fixed
  top: 50%
  left: 50%
  transform: translate(-50%, -50%)
  z-index: $z-modal
  width: 520px
  max-width: calc(100vw - 32px)
  max-height: calc(100vh - 64px)
  display: flex
  flex-direction: column
  background-color: $bg-element
  border: 1px solid $border
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3)

:global(.help-dialog-header)
  display: flex
  align-items: center
  justify-content: space-between
  padding: $small $medium
  background-color: $bg-element-accent
  border-bottom: 1px solid $border
  flex-shrink: 0

:global(.help-dialog-title)
  font-weight: 600
  font-size: $font-size

:global(.help-dialog-close)
  background: none
  border: none
  font-size: 20px
  line-height: 1
  color: $text-muted
  cursor: pointer
  padding: 4px 8px

  &:hover
    color: $text

:global(.help-dialog-body)
  overflow-y: auto
  flex: 1
  padding: $medium

:global(.help-grid)
  display: grid
  grid-template-columns: 1fr 1fr
  gap: $medium

:global(.help-section)
  margin-bottom: 0

:global(.help-section-title)
  margin: 0 0 $small 0
  font-size: 11px
  font-weight: 600
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $text-muted
  border-bottom: 1px solid $border
  padding-bottom: $tiny

:global(.help-item)
  font-size: $secondary-font-size
  line-height: 1.6
  color: $text

  code
    font-family: $code-font
    font-size: 12px
    color: $link

  kbd
    float: right
    padding: 1px 5px
    font-size: 11px
    font-family: inherit
    color: $text-muted
    background-color: $bg-element-accent
    border: 1px solid $border
    border-radius: 2px

:global(.help-item-nsfw)
  code
    color: $accent-red

:global(.help-item-private)
  code
    color: $accent-green

:global(.help-item-warning)
  code
    color: $accent-red

:global(.help-item-mod)
  code
    color: $accent-green

:global(.help-item-code)
  code
    font-family: $code-font

:global(.help-footer)
  margin-top: $medium
  padding-top: $small
  border-top: 1px solid $border
  text-align: center
  font-size: $secondary-font-size
  color: $text-muted

  kbd
    padding: 2px 6px
    font-family: inherit
    background-color: $bg-element-accent
    border: 1px solid $border
    border-radius: 2px

// Help dialog transitions
:global(.help-dialog-enter-active),
:global(.help-dialog-leave-active)
  transition: opacity 0.2s ease, transform 0.2s ease

:global(.help-dialog-enter-from),
:global(.help-dialog-leave-to)
  opacity: 0
  transform: translate(-50%, -50%) scale(0.95)

// Help dialog mobile
@media (max-width: 600px)
  :global(.help-grid)
    grid-template-columns: 1fr

// Mobile responsive
@media (max-width: 768px)
  .editor-toolbar
    padding: $small
    gap: 2px

  .tag-btn
    // Touch target minimum 44x44px for accessibility
    min-width: 36px
    min-height: 36px
    padding: 6px 8px
    font-size: 13px

  .toolbar-separator
    display: none

  .toolbar-spacer
    display: none

  .mode-tabs
    margin-left: auto

  .status-bar
    flex-wrap: wrap
    justify-content: center
    padding: 6px $small

  .help-table
    font-size: 12px

    .help-col-key
      display: none

    th:last-child
      display: none

@media (max-width: 480px)
  .editor-toolbar
    justify-content: center

  .tag-btn
    min-width: 40px
    min-height: 40px
    padding: 8px 10px
</style>
