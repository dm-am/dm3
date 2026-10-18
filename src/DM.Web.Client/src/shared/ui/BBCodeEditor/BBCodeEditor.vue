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
import { symbols } from "@/shared/lib/utils/icons";
import { pluralize } from "@/shared/lib/utils/pluralize";
import { useEditor, EditorContent } from "@tiptap/vue-3";
import {
  bbcodeToHtml,
  htmlToBbcode,
  validateBBCode,
  cleanPastedHtml,
  isSafeAddress,
  isWritableValue,
  isWritablePrivateValue,
  type BBCodeContext,
  type BBCodeIssue,
  CONTEXT_TAGS,
} from "@/shared/lib/utils/bbcode";
import { Tooltip } from "@/shared/ui/Tooltip";
import InsertPanel, { type InputField } from "./InsertPanel.vue";
import MarkupHelpDialog from "./MarkupHelpDialog.vue";
import { createEditorExtensions, EDITOR_PARSE_OPTIONS } from "./lib";

const props = withDefaults(
  defineProps<{
    modelValue: string;
    context?: BBCodeContext;
    /**
     * The surface the save path reads the text on, when it is not the one the
     * editor draws for. Only the markup check reads it. The buttons, the help
     * and the preview stay with context. The metagame half of a game post is
     * written with the post's editor and saved on the comment surface, which
     * refuses a hidden block, and the check has to say so before the send.
     */
    checkContext?: BBCodeContext;
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
    checkContext: undefined,
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
const indicatorStyle = ref<Record<string, string>>({
  left: "0px",
  width: "50%",
});

/**
 * Lays the line under the active tab. It slides there when the mode changes,
 * and is put there at once when a tab changes size under it: the tabs are
 * first measured before the site's font has arrived, and "wysiwyg" in the
 * fallback font was 3px wider, so the line stood past the editor's right edge
 * until the mode was switched. The box is read in fractions of a pixel, since
 * offsetLeft and offsetWidth round and left the line a third of one past the tab.
 */
function updateIndicator(slide = true) {
  if (!modeTabsRef.value) return;
  const tabs = modeTabsRef.value.querySelectorAll<HTMLElement>(".mode-tab");
  const activeTab = tabs[mode.value === "bbcode" ? 0 : 1];
  if (!activeTab) return;
  const strip = modeTabsRef.value.getBoundingClientRect();
  const tab = activeTab.getBoundingClientRect();
  indicatorStyle.value = {
    left: `${tab.left - strip.left}px`,
    width: `${tab.width}px`,
    ...(slide ? {} : { transition: "none" }),
  };
}

watch(mode, () => {
  nextTick(() => updateIndicator());
});

// Watches the two tabs for a change of size, the arrival of the font among them.
let modeTabsObserver: ResizeObserver | null = null;

function observeModeTabs() {
  if (!modeTabsRef.value || typeof ResizeObserver === "undefined") return;
  modeTabsObserver = new ResizeObserver(() => updateIndicator(false));
  for (const tab of modeTabsRef.value.querySelectorAll(".mode-tab")) {
    modeTabsObserver.observe(tab);
  }
}

// The row of tag buttons beside the mode switch. It wraps at every width:
// every tag is in sight and nothing is cut away into "…", on a phone too,
// which shows the row of the full version. At 1366
// it takes two lines under the comment of a topic and one in the window of
// 920. It breaks between groups alone, so "ul ol" never parts and no line
// opens with a rule.

// Draft recovery
const hasDraft = ref(false);

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

// Draft status indicator
const draftStatus = ref<"idle" | "saving" | "saved">("idle");
const draftSavedAt = ref<number | null>(null);

/** The insertions that ask for something first, each in a panel under its button. */
type InsertKind = "link" | "image" | "private" | "spoiler";

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

/**
 * Why a parameter value cannot be written, or null when it can. Markup has no
 * escaping, so the value would end sooner than it was meant to.
 */
function valueRefusal(value: string): string | null {
  if (isWritableValue(value)) return null;
  if (value.includes("[")) return "Нельзя использовать символ [";
  if (/" *\]/.test(value)) return "Нельзя ставить кавычку перед ]";
  return "Нельзя ставить кавычку перед словом со знаком =";
}

/**
 * An address the server would not show although its scheme is right: one
 * that is not an address at all (a space or a control character in it, no
 * host), or one that points at the reader's own machine or network. The
 * scheme itself is the panel's own check.
 */
function addressRefusal(value: string): string | null {
  const address = value.trim();
  if (!/^https?:\/\//i.test(address) || isSafeAddress(address)) return null;
  return isWellFormedAddress(address)
    ? "Адрес ведет в локальную сеть, такой сайт не покажет"
    : "Введите корректный URL";
}

/** An address with a host and nothing in it that no address carries. */
function isWellFormedAddress(address: string): boolean {
  for (let index = 0; index < address.length; index++) {
    const code = address.charCodeAt(index);
    if (code <= 0x20 || (code >= 0x7f && code <= 0x9f)) return false;
  }
  if (/\s/.test(address)) return false;
  try {
    return new URL(address).hostname !== "";
  } catch {
    return false;
  }
}

/** A size in pixels, as the grammar takes it. */
function sizeRefusal(value: string): string | null {
  if (!value.trim() || /^[1-9]\d{0,3}$/.test(value.trim())) return null;
  return "Только целое число от 1 до 9999";
}

// The fields of the insertion panels. The text of a link comes first, as it
// reads in the post, and the caret starts in it (owner's call, 28 September
// 2026); the address is the one field a link cannot do without.
const linkFields: InputField[] = [
  {
    name: "text",
    label: "Текст ссылки",
    type: "text",
    placeholder: "Отображаемый текст",
    required: false,
    validator: valueRefusal,
  },
  {
    name: "url",
    label: "URL ссылки",
    type: "url",
    placeholder: "https://example.com",
    required: true,
    validator: addressRefusal,
  },
];

const imageFields: InputField[] = [
  {
    name: "url",
    label: "URL изображения",
    type: "url",
    placeholder: "https://example.com/image.jpg",
    required: true,
    validator: addressRefusal,
  },
  {
    name: "width",
    label: "Ширина (px)",
    type: "text",
    placeholder: "600",
    required: false,
    validator: sizeRefusal,
  },
  {
    name: "height",
    label: "Высота (px)",
    type: "text",
    placeholder: "400",
    required: false,
    validator: sizeRefusal,
  },
];

// Empty names make the bare [private], which the game leads alone read.
const privateFields: InputField[] = [
  {
    name: "character",
    label: "Имена персонажей через запятую",
    type: "text",
    placeholder: "Вася, Петя",
    required: false,
    validator: (value: string) => {
      if (isWritablePrivateValue(value)) return null;
      if (value.includes("[")) return "Нельзя использовать символ [";
      return "Нельзя ставить кавычку перед ]";
    },
  },
];

const spoilerFields: InputField[] = [
  {
    name: "text",
    label: "Подпись кнопки",
    type: "text",
    placeholder: "Показать содержимое",
    required: false,
    validator: valueRefusal,
  },
];

// BBCode validation
const validationErrors = ref<BBCodeIssue[]>([]);
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

// The two items of the status line are built here, not glued in the template:
// an item spelled out of adjacent mustaches has no single text node to select,
// and the noun after a count comes from pluralize — shared/lib/utils/pluralize
// owns that rule, and the ternary that used to stand here said "22 слов".
const wordCountLabel = computed(
  () =>
    `${wordCount.value} ${pluralize(wordCount.value, "слово", "слова", "слов")}`,
);

const charCountLabel = computed(() => {
  // The noun agrees with the number standing right before it, which is the
  // limit when there is one: "12 / 500 символов", but "1 символ". Only one of
  // the editor's twenty-odd embed sites passes a limit, so the counter of every
  // other one opened on "1 символов".
  const counted = props.maxLength > 0 ? props.maxLength : charCount.value;
  const limit = props.maxLength > 0 ? ` / ${props.maxLength}` : "";
  return `${charCount.value}${limit} ${pluralize(counted, "символ", "символа", "символов")}`;
});

// Draft status text. Relative time ("Сохранено 12 сек. назад") is deliberately
// absent: a computed has no reactive dependency on the clock, so it would be
// recomputed only when the status itself changes, i.e. always at zero seconds.
// Bringing it back takes a ticking ref, not a wider computed.
const draftStatusText = computed(() => {
  if (draftStatus.value === "saving") return "Сохранение...";
  if (draftStatus.value === "saved" && draftSavedAt.value) return "Сохранено";
  return "";
});

function validateBbcode() {
  if (mode.value !== "bbcode") {
    validationErrors.value = [];
    return;
  }
  validationErrors.value = validateBBCode(
    bbcodeText.value,
    props.checkContext ?? props.context,
  );
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

/** The tags the toolbar offers here: [mod] to a moderator alone. */
const offeredTags = computed(() =>
  availableTags.value.filter((tag) => tag !== "mod" || props.isModerator),
);

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

/**
 * The letter of a key as the Latin layout has it. event.key is the letter the
 * active layout types, so on a Russian one Ctrl+B arrived as "и" and matched
 * nothing; event.code names the physical key.
 */
function letterOf(event: KeyboardEvent): string {
  const code = /^Key([A-Z])$/.exec(event.code ?? "");
  return code ? code[1].toLowerCase() : event.key.toLowerCase();
}

/** Ctrl (Cmd on a Mac) and Shift with a letter, and nothing else. */
function isChord(
  event: KeyboardEvent,
  letter: string,
  shift: boolean,
): boolean {
  return (
    (event.ctrlKey || event.metaKey) &&
    !event.altKey &&
    event.shiftKey === shift &&
    letterOf(event) === letter
  );
}

/** Text into the editor, in the visual mode's reading of it. */
function showInVisualMode(bbcode: string) {
  editor.value?.commands.setContent(
    bbcodeToHtml(bbcode, { context: props.context }),
    { emitUpdate: false, parseOptions: EDITOR_PARSE_OPTIONS },
  );
}

// Tiptap editor
const editor = useEditor({
  extensions: createEditorExtensions(props.placeholder),
  content: "",
  editable: !props.disabled,
  editorProps: {
    // The editor's own keys come before every extension's. Ctrl+Enter sends,
    // as it does in the BBCode mode; the kit bound it to a line break.
    // Ctrl+Shift+S is the spoiler of the toolbar: bound by the extension it
    // matched the typed letter, and on a Russian layout that fell through to
    // the kit's strike, bound to the same key.
    handleKeyDown: (_view, event) => {
      if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
        event.preventDefault();
        emit("submit");
        return true;
      }
      if (
        isChord(event, "s", true) &&
        availableTags.value.includes("spoiler")
      ) {
        event.preventDefault();
        editor.value?.chain().focus().toggleSpoiler().run();
        return true;
      }
      return false;
    },
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
        showInVisualMode(newValue);
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
    // A shut editor takes nothing in: its buttons are off, and so is a panel
    // that was open when the form went to send.
    if (disabled) closeInsertion();
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
      showInVisualMode(bbcodeText.value);
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
// 1. EXCEPTIONS: 4 functions (link, image, private, tab) have significantly
//    different behavior (panels, self-closing tags, parameters) that would
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

function insertLink(event: Event) {
  toggleInsertion("link", event);
}

/** Put a piece of BBCode at the caret of the BBCode mode. */
function insertAtCaret(piece: string) {
  const textarea = bbcodeTextarea.value;
  if (!textarea) return;
  const pos = textarea.selectionStart;
  bbcodeText.value =
    bbcodeText.value.substring(0, pos) +
    piece +
    bbcodeText.value.substring(pos);
  emit("update:modelValue", bbcodeText.value);
  saveDraft(bbcodeText.value);
  nextTick(() => {
    textarea.focus();
    textarea.setSelectionRange(pos + piece.length, pos + piece.length);
  });
}

/**
 * [link]address[/link] shows the word "ссылка", as on DM2;
 * [link text="..."]address[/link] shows its own text.
 */
function handleLinkSubmit(values: Record<string, string>) {
  const url = values.url?.trim();
  const text = values.text?.trim();

  if (!url) return;

  if (mode.value === "bbcode") {
    insertAtCaret(
      text ? `[link text="${text}"]${url}[/link]` : `[link]${url}[/link]`,
    );
  } else {
    editor.value
      ?.chain()
      .focus()
      .setBbLink({ href: url, text: text || undefined })
      .run();
  }
}

function insertImage(event: Event) {
  toggleInsertion("image", event);
}

/**
 * [img width="..." height="..."]address[/img], with only the parameters that
 * were filled in, in that order. An image has no alt: a reader never sees it,
 * and text nobody sees on the page is where an insult goes unnoticed.
 */
function handleImageSubmit(values: Record<string, string>) {
  const url = values.url?.trim();
  if (!url) return;

  const width = values.width?.trim() || null;
  const height = values.height?.trim() || null;

  if (mode.value === "bbcode") {
    let opening = "[img";
    if (width) opening += ` width="${width}"`;
    if (height) opening += ` height="${height}"`;
    insertAtCaret(`${opening}]${url}[/img]`);
  } else {
    editor.value
      ?.chain()
      .focus()
      .setBbImage({
        src: url,
        width: width ? parseInt(width, 10) : null,
        height: height ? parseInt(height, 10) : null,
      })
      .run();
  }
}

/**
 * A spoiler asks for its caption, which is optional. Inside a spoiler of the
 * visual mode the button takes the spoiler off instead.
 */
function insertSpoiler(event: Event) {
  if (mode.value === "wysiwyg" && editor.value?.isActive("spoiler")) {
    closeInsertion();
    editor.value.chain().focus().toggleSpoiler().run();
    return;
  }
  toggleInsertion("spoiler", event);
}

function handleSpoilerSubmit(values: Record<string, string>) {
  const text = values.text?.trim();
  if (mode.value === "bbcode") {
    wrapSelection(
      text ? `[spoiler text="${text}"]` : "[spoiler]",
      "[/spoiler]",
    );
  } else {
    editor.value
      ?.chain()
      .focus()
      .setSpoiler({ text: text || null })
      .run();
  }
}

function insertPrivate(event: Event) {
  toggleInsertion("private", event);
}

/**
 * [private="Вася, Петя"] for the characters named, and the bare [private],
 * which only the game leads read, when no name is given.
 */
function handlePrivateSubmit(values: Record<string, string>) {
  const character = values.character?.trim() || null;

  if (mode.value === "bbcode") {
    wrapSelection(
      character === null ? "[private]" : `[private="${character}"]`,
      "[/private]",
    );
  } else {
    editor.value?.chain().focus().togglePrivate({ character }).run();
  }
}

/** Each panel: its name, which is its button's, its fields and what it writes. */
const INSERTIONS: Record<
  InsertKind,
  {
    label: string;
    fields: InputField[];
    write: (values: Record<string, string>) => void;
  }
> = {
  link: {
    label: "Вставить ссылку",
    fields: linkFields,
    write: handleLinkSubmit,
  },
  image: {
    label: "Вставить изображение",
    fields: imageFields,
    write: handleImageSubmit,
  },
  private: {
    label: "Приватный текст",
    fields: privateFields,
    write: handlePrivateSubmit,
  },
  spoiler: {
    label: "Спойлер",
    fields: spoilerFields,
    write: handleSpoilerSubmit,
  },
};

/** The panel's values into the text; the panel goes, the caret is in the text. */
function submitInsertion(values: Record<string, string>) {
  const kind = insertion.value?.kind;
  if (!kind) return;
  closeInsertion();
  INSERTIONS[kind].write(values);
}

function insertTab() {
  if (mode.value === "bbcode") {
    insertAtCaret("[tab]");
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

function loadDraftManual() {
  const draft = loadDraft();
  if (draft) {
    restoreDraft();
  }
}

/** A button of the toolbar: a tag of the markup, or one of the editor's own. */
interface ToolbarButton {
  /** Its caption: the tag it writes, as DM2's toolbar spells it. */
  name: string;
  /** The element the caption is set in, so it reads as what the tag does. */
  look?: "b" | "i" | "u" | "s";
  /** Its colour, the colour of what it writes. */
  tone?: "nsfw" | "private" | "mod";
  /** Its tooltip. */
  hint: string;
  /** Its name for a screen reader. */
  label: string;
  /**
   * The group it stands in. A rule closes a group in the row, and the row
   * breaks between groups alone.
   */
  group: string;
  /** The mark or node of the visual mode it stands pressed in. */
  mark?: string;
  /** The panel it opens. */
  panel?: InsertKind;
  /** The editor's own button, not a tag: on every surface. */
  own?: boolean;
  /** Opens the help window: it reads, so it works in a shut editor too. */
  help?: boolean;
  act: (event: Event) => void;
}

const TOOLBAR: readonly ToolbarButton[] = [
  {
    name: "b",
    look: "b",
    hint: "Жирный (Ctrl+B)",
    label: "Жирный",
    group: "text",
    mark: "bold",
    act: toggleBold,
  },
  {
    name: "i",
    look: "i",
    hint: "Курсив (Ctrl+I)",
    label: "Курсив",
    group: "text",
    mark: "italic",
    act: toggleItalic,
  },
  {
    name: "u",
    look: "u",
    hint: "Подчеркнутый (Ctrl+U)",
    label: "Подчеркнутый",
    group: "text",
    mark: "underline",
    act: toggleUnderline,
  },
  {
    name: "strike",
    look: "s",
    hint: "Зачеркнутый",
    label: "Зачеркнутый",
    group: "text",
    mark: "strike",
    act: toggleStrike,
  },
  {
    name: "spoiler",
    hint: "Спойлер",
    label: "Спойлер",
    group: "hidden",
    mark: "spoiler",
    panel: "spoiler",
    act: insertSpoiler,
  },
  {
    name: "nsfw",
    tone: "nsfw",
    hint: "18+ контент",
    label: "NSFW контент",
    group: "hidden",
    mark: "nsfw",
    act: insertNsfw,
  },
  {
    name: "private",
    tone: "private",
    hint: "Приватный текст (видно только указанному персонажу)",
    label: "Приватный текст",
    group: "hidden",
    mark: "private",
    panel: "private",
    act: insertPrivate,
  },
  {
    name: "ul",
    hint: "Маркированный список",
    label: "Маркированный список",
    group: "lists",
    mark: "bulletList",
    act: toggleBulletList,
  },
  {
    name: "ol",
    hint: "Нумерованный список",
    label: "Нумерованный список",
    group: "lists",
    mark: "orderedList",
    act: toggleOrderedList,
  },
  {
    name: "img",
    hint: "Вставить изображение",
    label: "Вставить изображение",
    group: "media",
    panel: "image",
    act: insertImage,
  },
  {
    name: "link",
    hint: "Вставить ссылку",
    label: "Вставить ссылку",
    group: "media",
    mark: "bbLink",
    panel: "link",
    act: insertLink,
  },
  {
    name: "quote",
    hint: "Цитата",
    label: "Цитата",
    group: "blocks",
    mark: "bbQuote",
    act: toggleBlockquote,
  },
  {
    name: "mod",
    tone: "mod",
    hint: "Модераторский блок",
    label: "Модераторский блок",
    group: "blocks",
    mark: "modBlock",
    act: insertMod,
  },
  {
    name: "tab",
    hint: "Отступ",
    label: "Вставить отступ",
    group: "layout",
    act: insertTab,
  },
  {
    name: "code",
    hint: "Моноширинный код",
    label: "Код",
    group: "code",
    mark: "code",
    act: toggleCode,
  },
  {
    name: "noparse",
    hint: "Без обработки BBCode",
    label: "Без форматирования",
    group: "code",
    mark: "noparse",
    act: insertNoparse,
  },
  {
    name: "load",
    hint: "Загрузить черновик",
    label: "Загрузить черновик",
    group: "utils",
    own: true,
    act: loadDraftManual,
  },
  {
    name: "help",
    hint: "Справка",
    label: "Показать справку",
    group: "utils",
    own: true,
    help: true,
    act: openHelp,
  },
];

/**
 * The buttons of this surface, in the order of the row: the tags it reads
 * ([mod] to a moderator alone) and the editor's own two. The last button of a
 * group carries the rule after it, and the last group carries none.
 */
const toolbar = computed(() => {
  const shown = TOOLBAR.filter(
    (button) => button.own || offeredTags.value.includes(button.name),
  );
  return shown.map((button, at) => ({
    ...button,
    closesGroup: at < shown.length - 1 && shown[at + 1].group !== button.group,
  }));
});

/** A piece of the row: buttons that stand on one line together. */
interface ToolbarPiece {
  key: string;
  /** The group of its buttons. */
  group: string;
  buttons: (typeof toolbar.value)[number][];
  /** Whether a rule ends it: it ends a group, and one more follows. */
  closesGroup: boolean;
}

/**
 * The pieces the row is laid in, each one a group never parted, so the row
 * wraps between groups alone. The rule after a group stands at the end of the
 * piece that closes it.
 */
const pieces = computed<ToolbarPiece[]>(() => {
  const groups: ToolbarPiece[] = [];
  for (const button of toolbar.value) {
    let last: ToolbarPiece | undefined = groups[groups.length - 1];
    if (last?.group !== button.group) {
      last = {
        key: button.group,
        group: button.group,
        buttons: [],
        closesGroup: false,
      };
      groups.push(last);
    }
    last.buttons.push(button);
    last.closesGroup = button.closesGroup;
  }
  return groups;
});

/** Whether a tag stands pressed: the caret is in what it wrote. */
function isPressed(button: ToolbarButton): boolean | undefined {
  if (!button.mark) return undefined;
  return mode.value === "wysiwyg" && editor.value?.isActive(button.mark);
}

// Draft management with expiration
const DRAFT_EXPIRATION_MS = 7 * 24 * 60 * 60 * 1000; // 7 days
let lastSavedDraft = "";

interface DraftData {
  content: string;
  timestamp: number;
}

function getDraftKey(): string {
  return props.draftKey ? `bbcode_draft_${props.draftKey}` : "";
}

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

/** Put text into whichever mode is showing, and hand it to the parent. */
function applyContent(content: string) {
  emit("update:modelValue", content);
  if (mode.value === "wysiwyg" && editor.value) {
    showInVisualMode(content);
  } else {
    bbcodeText.value = content;
    nextTick(() => autoResizeTextarea());
  }
}

function restoreDraft() {
  const draft = loadDraft();
  if (draft) applyContent(draft);
  hasDraft.value = false;
}

/**
 * The composer belongs to one subject at a time, and the key says which one.
 * Pages keep the composer across a change of subject — the router reuses the
 * component and reloads its data underneath — so nothing else empties the box
 * when the reader opens another game, another dialogue, another room. Left
 * alone, what was typed for the previous subject stays on screen and is saved
 * under the new subject's key.
 *
 * An empty previous key means nothing was being drafted (the subject had not
 * loaded yet), so that transition only picks up the new subject's draft.
 */
watch(
  () => props.draftKey,
  (key, previous) => {
    lastSavedDraft = "";
    draftStatus.value = "idle";
    draftSavedAt.value = null;
    if (previous) applyContent("");
    const draft = key ? loadDraft() : null;
    hasDraft.value = Boolean(draft?.trim());
  },
);

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
    showInVisualMode(props.modelValue);
  }

  // Auto-resize textarea if in BBCode mode with content
  if (mode.value === "bbcode" && props.modelValue) {
    bbcodeText.value = props.modelValue;
    nextTick(() => autoResizeTextarea());
  }

  // Initialize mode indicator position
  nextTick(() => updateIndicator());
  observeModeTabs();
});

onUnmounted(() => {
  modeTabsObserver?.disconnect();
  editor.value?.destroy();
});

// Keyboard shortcuts for BBCode mode
function handleBbcodeKeydown(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey)) return;
  if (event.key === "Enter") {
    event.preventDefault();
    emit("submit");
    return;
  }
  const chords: Array<[string, boolean, string, string]> = [
    ["b", false, "[b]", "[/b]"],
    ["i", false, "[i]", "[/i]"],
    ["u", false, "[u]", "[/u]"],
    ["s", true, "[spoiler]", "[/spoiler]"],
  ];
  for (const [letter, shift, before, after] of chords) {
    if (isChord(event, letter, shift)) {
      event.preventDefault();
      wrapSelection(before, after);
      return;
    }
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
      <!-- The tag buttons, wrapping between groups, every tag in sight. The
           switch stands outside the row. -->
      <div class="toolbar-tags">
        <span v-for="piece in pieces" :key="piece.key" class="tag-group"
          ><template v-for="button in piece.buttons" :key="button.name"
            ><Tooltip :text="button.hint"
              ><button
                type="button"
                class="tag-btn"
                :class="[
                  button.tone && `tag-btn-${button.tone}`,
                  { active: isPressed(button) },
                ]"
                :aria-pressed="isPressed(button)"
                :disabled="button.help ? undefined : disabled"
                :aria-haspopup="
                  button.panel || button.help ? 'dialog' : undefined
                "
                :aria-expanded="
                  button.panel ? insertion?.kind === button.panel : undefined
                "
                :aria-label="button.label"
                @click="button.act"
              >
                <component :is="button.look" v-if="button.look">{{
                  button.name
                }}</component
                ><template v-else>{{ button.name }}</template>
              </button></Tooltip
            ></template
          ><span
            v-if="piece.closesGroup"
            class="toolbar-separator"
            role="separator"
            aria-orientation="vertical"
          ></span
        ></span>
      </div>
      <!-- Mode toggle, at the right end of the line. In a column too narrow
           for it and the widest group of tags side by side, at the right end
           of a line of its own under the tags. -->

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
        <!-- Zero-width preserved space: the pair copies as "[bbcode] wysiwyg"
             and gains no gap on screen (the global .copy-space, Reset.sass). -->
        <span class="copy-space">{{ " " }}</span>
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

      <MarkupHelpDialog
        v-if="helpBuilt"
        v-model="showHelp"
        :tags="offeredTags"
        :drafts="Boolean(draftKey)"
        :sends="sendsOnChord"
      />
    </div>

    <!-- Status bar - outside resizable area -->
    <div
      v-if="showCharCount"
      class="status-bar"
      role="status"
      aria-live="polite"
    >
      <span class="status-item">{{ wordCountLabel }}</span
      ><span class="status-separator">{{ " | " }}</span
      ><span class="status-item" :class="{ 'over-limit': isOverLimit }">{{
        charCountLabel
      }}</span
      ><span v-if="hasDraft && draftKey" class="status-separator">{{
        " | "
      }}</span
      ><span v-if="hasDraft && draftKey" class="status-item draft-available"
        >Есть черновик</span
      ><span
        v-if="draftStatusText && draftKey && !hasDraft"
        class="status-separator"
        >{{ " | " }}</span
      ><span
        v-if="draftStatusText && draftKey && !hasDraft"
        class="status-item draft-status"
        :class="{ saving: draftStatus === 'saving' }"
        >{{ draftStatusText }}</span
      >
    </div>

    <!-- The markup check - outside resizable area. One finding to a line,
         read as "Строка 3: [b] не закрыт, добавьте [/b]". -->
    <div
      v-if="mode === 'bbcode' && validationErrors.length > 0"
      id="bbcode-validation-errors"
      class="validation-errors"
      role="alert"
      aria-live="polite"
      aria-atomic="true"
    >
      <div
        v-for="(issue, index) in validationErrors"
        :key="`${index}:${issue.message}`"
        class="validation-error"
      >
        <span class="error-icon" aria-hidden="true">{{ symbols.warning }}</span>
        <span class="error-text">{{ issue.message }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

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

// A finger does not drag the grip in the corner of the field, and the browser
// of a phone drew it as a dot just outside the rounded corner. A touch screen
// has none; under a mouse it stays as it was. The field grows with its text
// either way.
@media (pointer: coarse)
  .bbcode-editor.resizable
    resize: none

// The row of tag buttons takes what the mode switch leaves, and the switch
// keeps its own width at the right end, in the middle of the row's lines. The
// toolbar wraps as well. A column of it can be narrower than the widest group
// of tags and the switch side by side (243 in the form of a post at 1001: 154
// for "spoiler nsfw private |", 114 for the switch); the switch goes down to a
// line of its own there, at the right end, and never stands out of the
// editor. Where both fit, as at 1366 and in the window of 920, they stay on
// one line.
.editor-toolbar
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  padding: $small 0
  flex-shrink: 0

  .mode-tabs
    margin-left: auto

// The row of tag buttons is given what the switch leaves and asks for the
// width of its widest group, which never parts. It wraps between its groups,
// and the switch stands in the middle of its lines.
.toolbar-tags
  flex: 1 1 0
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $minor

// Inline flow, not flex, for the same reason as the status bar: flex items
// copy one per line, and this pair copied as "[bbcode]\nwysiwyg". font-size: 0
// removes the container's own line-box strut, so the tabs keep exactly the
// height the flex row gave them (each button restates its own font size).
.mode-tabs
  display: block
  flex-shrink: 0
  white-space: nowrap
  font-size: 0
  border-bottom: 2px solid $border
  position: relative

.mode-tab
  display: inline-block
  vertical-align: top
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

// A group of tags and the rule after it, a piece of the row: the row breaks
// between groups alone, and the rule hangs at the end of the line its group
// ends, as a bar of a caption line does.
.tag-group
  display: inline-flex
  align-items: center
  gap: $minor

.toolbar-separator
  width: 1px
  height: 18px
  background-color: $border
  margin: 0 2px

.tag-btn
  // The vertical half is $tiny. The horizontal one stays a literal: 6px sits
  // between $minor and $small, the toolbar is one row of these buttons, and a
  // step on the scale moves that row by 4px per button.
  padding: $tiny 6px
  border: 1px solid
  border-color: $border
  // DM2's 2, as the toolbar stood before 28 September: the site's 8 is the
  // mark of a control of the page, and it drew the row as a row of pills
  // (owner, 5 October 2026). RADIUS_REGISTER in styleSystem.spec.ts.
  border-radius: 2px
  background: transparent
  cursor: pointer
  font-size: 12px
  font-family: inherit
  color: $text

  &:hover:not(:disabled)
    background-color: $bg-element-accent

  &:disabled
    opacity: $disabled-opacity
    cursor: default

  // The bold of the face: PT Sans ships 400 and 700 alone, and a 600 was a
  // 700 drawn under another name.
  &.active
    background-color: $bg-element-accent
    font-weight: 700

  // "code" is set in the face of the row, as every tag of the DM2 toolbar is.
  // In Courier New at 12px its hairline strokes read as grey beside the other
  // captions, as if the button were shut.

.tag-btn-private
  color: $accent-green

.tag-btn-nsfw
  color: $accent-red

.tag-btn-mod
  color: $accent-green

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
    // Holds the float of the hint of an empty field (below), so all of the
    // hint is the field to tap: its second line on a phone too. The text
    // holds no floats of its own, and a block of it stands where it stood.
    display: flow-root
    // Line-height and paragraph spacing come from +bbcode-content
    // (SSOT): p margin 0, blank line = one full empty line box — the
    // WYSIWYG view previews exactly what the display render shows.
    +bbcode-content

    // A hidden block written around blocks stands on the blocks, and its mark
    // is a span that holds them. Drawn in the line, its colour showed only in
    // the gaps between them, and the blocks looked like open text.
    .bb-private:has(> :is(p, div, blockquote, ul, ol))
      display: block

    // Tiptap placeholder. The extension only marks the empty node and hands the
    // text over in data-placeholder — drawing it is on us, so the hint the
    // BBCode textarea gets for free has to be written out here.
    //
    // A float as wide as the line whose right margin takes that width back:
    // it stands no line aside, so the caret of the empty line is at its start,
    // on the hint's first letter, and it keeps the height of its own lines.
    // Of no height, as Tiptap draws it, a hint of two lines on a phone ran out
    // of .wysiwyg-content, a box one line high that cuts what overflows it,
    // and its second line showed as a strip of the tops of its letters. The
    // field holds its float now (flow-root above) and grows under the hint.
    p.is-editor-empty:first-child::before
      content: attr(data-placeholder)
      float: left
      width: 100%
      margin-right: -100%
      pointer-events: none
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

// Status bar - outside the bordered container. Inline flow, not flex: a flex
// item is blockified, and the strip copied as "0 слов\n|\n0 / 10000 символов"
// on every form of the site. The separator carries a real " | " text node, so
// the copy reads as one line; the former gap: $small is that pair of spaces
// plus the separator's own padding.
.status-bar
  display: block
  padding: $tiny 0
  font-size: 11px
  color: $text-muted

.status-separator
  padding: 0 $minor
  opacity: 0.5

.status-item
  &.over-limit
    color: $accent-red
    font-weight: 700

// The -muted accent variants do not clear AA as a text colour (2.82 and 3.00
// on the card in the light theme). The draft status is ordinary small text.
.draft-available
  color: $accent-red

.draft-status
  color: $accent-green

  &.saving
    color: $text-muted

// The markup check, under the status line: a band of the editor's own, one
// finding to a line, each after the warning sign and in the colour of a
// refusal. For a while it was plain lines in the colour of the text, flush
// with the editor's edge; they read worse than this band, and it came back.
// The band is the page a shade darker, the overlay the site's controls sit on,
// and not a colour: the owner found the blue fill it had too loud for a note
// under the field.
.validation-errors
  padding: $small $medium
  background-color: $control-bg-overlay
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
</style>
