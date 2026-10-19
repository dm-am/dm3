<script setup lang="ts">
// A button only says it was pressed; the editor runs its act on itself (EditorTarget).
import { computed } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { InsertKind } from "./lib/insertions";
import type { EditorTarget } from "./lib/sourceField";
import { insertSpoiler, insertTab, wrapTag } from "./lib/tagCommands";

/** A button of the toolbar: a tag of the markup, or one of the editor's own. */
export interface ToolbarButton {
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
  act: (target: EditorTarget, event: Event) => void;
}

const TOOLBAR: readonly ToolbarButton[] = [
  {
    name: "b",
    look: "b",
    hint: "Жирный (Ctrl+B)",
    label: "Жирный",
    group: "text",
    mark: "bold",
    act: wrapTag("[b]", "[/b]", (editor) =>
      editor.chain().focus().toggleBold().run(),
    ),
  },
  {
    name: "i",
    look: "i",
    hint: "Курсив (Ctrl+I)",
    label: "Курсив",
    group: "text",
    mark: "italic",
    act: wrapTag("[i]", "[/i]", (editor) =>
      editor.chain().focus().toggleItalic().run(),
    ),
  },
  {
    name: "u",
    look: "u",
    hint: "Подчеркнутый (Ctrl+U)",
    label: "Подчеркнутый",
    group: "text",
    mark: "underline",
    act: wrapTag("[u]", "[/u]", (editor) =>
      editor.chain().focus().toggleUnderline().run(),
    ),
  },
  {
    name: "strike",
    look: "s",
    hint: "Зачеркнутый",
    label: "Зачеркнутый",
    group: "text",
    mark: "strike",
    act: wrapTag("[strike]", "[/strike]", (editor) =>
      editor.chain().focus().toggleStrike().run(),
    ),
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
    act: wrapTag("[nsfw]", "[/nsfw]", (editor) =>
      editor.chain().focus().toggleNsfw().run(),
    ),
  },
  {
    name: "private",
    tone: "private",
    hint: "Скрытый блок (видно только указанному персонажу)",
    label: "Скрытый блок",
    group: "hidden",
    mark: "private",
    panel: "private",
    act: (target, event) => target.toggleInsertion("private", event),
  },
  {
    name: "ul",
    hint: "Маркированный список",
    label: "Маркированный список",
    group: "lists",
    mark: "bulletList",
    act: wrapTag("[ul]\n[li]", "[/li]\n[/ul]", (editor) =>
      editor.chain().focus().toggleBulletList().run(),
    ),
  },
  {
    name: "ol",
    hint: "Нумерованный список",
    label: "Нумерованный список",
    group: "lists",
    mark: "orderedList",
    act: wrapTag("[ol]\n[li]", "[/li]\n[/ol]", (editor) =>
      editor.chain().focus().toggleOrderedList().run(),
    ),
  },
  {
    name: "img",
    hint: "Вставить изображение",
    label: "Вставить изображение",
    group: "media",
    panel: "image",
    act: (target, event) => target.toggleInsertion("image", event),
  },
  {
    name: "link",
    hint: "Вставить ссылку",
    label: "Вставить ссылку",
    group: "media",
    mark: "bbLink",
    panel: "link",
    act: (target, event) => target.toggleInsertion("link", event),
  },
  {
    name: "quote",
    hint: "Цитата",
    label: "Цитата",
    group: "blocks",
    mark: "bbQuote",
    act: wrapTag("[quote]", "[/quote]", (editor) =>
      editor.chain().focus().toggleQuote().run(),
    ),
  },
  {
    name: "mod",
    tone: "mod",
    hint: "Модераторский блок",
    label: "Модераторский блок",
    group: "blocks",
    mark: "modBlock",
    act: wrapTag("[mod]", "[/mod]", (editor) =>
      editor.chain().focus().toggleModBlock().run(),
    ),
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
    act: wrapTag("[code]", "[/code]", (editor) =>
      editor.chain().focus().toggleCode().run(),
    ),
  },
  {
    name: "noparse",
    hint: "Без обработки BBCode",
    label: "Без форматирования",
    group: "code",
    mark: "noparse",
    act: wrapTag("[noparse]", "[/noparse]", (editor) =>
      editor.chain().focus().toggleNoparse().run(),
    ),
  },
  {
    name: "load",
    hint: "Загрузить черновик",
    label: "Загрузить черновик",
    group: "utils",
    own: true,
    act: (target) => target.restoreDraft(),
  },
  {
    name: "help",
    hint: "Справка",
    label: "Показать справку",
    group: "utils",
    own: true,
    help: true,
    act: (target) => target.openHelp(),
  },
];

const props = defineProps<{
  /** The tags the surface offers here. */
  tags: readonly string[];
  disabled: boolean;
  /** The panel on screen, whose button stands expanded, or null. */
  openPanel: InsertKind | null;
  /** Whether a tag stands pressed: the caret is in what it wrote. */
  isPressed: (button: ToolbarButton) => boolean | undefined;
}>();

const emit = defineEmits<{
  (e: "press", button: ToolbarButton, event: Event): void;
}>();

/**
 * The row in pieces, a group each, never parted: the row wraps between them, and a rule ends every piece but
 * the last. The buttons are the tags the surface reads ([mod] to a moderator alone) and the editor's own two.
 */
const pieces = computed(() => {
  const groups: { group: string; buttons: ToolbarButton[] }[] = [];
  for (const button of TOOLBAR) {
    if (!button.own && !props.tags.includes(button.name)) continue;
    const last = groups[groups.length - 1];
    if (last?.group === button.group) last.buttons.push(button);
    else groups.push({ group: button.group, buttons: [button] });
  }
  return groups;
});
</script>

<template>
  <div class="editor-toolbar" role="toolbar" aria-label="Панель форматирования">
    <div class="toolbar-tags">
      <span
        v-for="(piece, index) in pieces"
        :key="piece.group"
        class="tag-group"
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
                button.panel ? openPanel === button.panel : undefined
              "
              :aria-label="button.label"
              @click="emit('press', button, $event)"
            >
              <component :is="button.look" v-if="button.look">{{
                button.name
              }}</component
              ><template v-else>{{ button.name }}</template>
            </button></Tooltip
          ></template
        ><span
          v-if="index < pieces.length - 1"
          class="toolbar-separator"
          role="separator"
          aria-orientation="vertical"
        ></span
      ></span>
    </div>
    <slot />
  </div>
</template>

<style scoped lang="sass">
// Where the column is narrower than the widest group and the switch, the switch takes a line of its own.
.editor-toolbar
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  padding: $small 0
  flex-shrink: 0

  // The switch is the editor's (EditorModeSwitch.vue), set in the slot.
  :slotted(.mode-tabs)
    margin-left: auto

.toolbar-tags
  flex: 1 1 0
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $minor

// A group never parts: the row breaks between groups alone.
.tag-group
  display: inline-flex
  align-items: center
  gap: $minor

.toolbar-separator
  width: 1px
  height: 18px
  background-color: $border
  margin: 0 $tiny

.tag-btn
  // 6px sits between $minor and $small; a step of the scale moves the row by 4px per button.
  padding: $tiny 6px
  border: 1px solid
  border-color: $border
  // DM2's radius: the site's 8 marks a control of the page (RADIUS_REGISTER in roundedCorners.spec.ts).
  border-radius: 2px
  background: transparent
  cursor: pointer
  font-size: $tertiary-font-size
  font-family: inherit
  color: $text

  &:hover:not(:disabled)
    background-color: $bg-element-accent

  &:disabled
    opacity: $disabled-opacity
    cursor: default

  // PT Sans ships 400 and 700 alone.
  &.active
    background-color: $bg-element-accent
    font-weight: 700

.tag-btn-private,
.tag-btn-mod
  color: $accent-green

.tag-btn-nsfw
  color: $accent-red
</style>
