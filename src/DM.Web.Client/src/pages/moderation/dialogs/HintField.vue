<script setup lang="ts">
/**
 * The field of a tag description: the text as typed, two buttons over it that
 * write its one tag, and the markup check under it.
 *
 * A description is the hint surface of the server (BbSurface.Hint), whose one
 * tag is [tip]: a word that shows a text, a picture, or a picture with its
 * text when a reader points at it or taps it. The buttons are named after the
 * tag, as the buttons of the editor are ("tip text", "tip img"): the moderator
 * selects a word, presses one, and the editor's panel of an insertion opens
 * under the button with the word in it already. The check reads the text as
 * the server does (validateBBCode, the hint context) and names what the page
 * will print as typed, in the words and the look of the editor's check.
 *
 * No visual mode. The text is a sentence or two with one kind of tag in it,
 * and what the moderator has to see is not formatted text but the tooltip a
 * reader gets, which the preview under the field draws (HintPreview).
 *
 * The component is the row of the field's own children: the textarea stands
 * straight in the row of the FormField around it, whose rules for a window
 * reach a textarea only there.
 */
import { computed, nextTick, ref } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import InsertPanel, {
  type InputField,
} from "@/shared/ui/BBCodeEditor/InsertPanel.vue";
import {
  isTipImageAddress,
  isWritableValue,
  validateBBCode,
} from "@/shared/lib/utils/bbcode";
import { symbols } from "@/shared/lib/utils/icons";

const text = defineModel<string>({ required: true });

const props = withDefaults(
  defineProps<{
    /** Id of the textarea, which the label of the field points at. */
    id: string;
    rows?: number;
  }>(),
  { rows: 3 },
);

type Kind = "text" | "img";

const textarea = ref<HTMLTextAreaElement | null>(null);
const buttons = ref<Record<Kind, HTMLElement | null>>({
  text: null,
  img: null,
});

/** The open panel, the button it hangs under, and the part of the text it replaces. */
const insertion = ref<{
  kind: Kind;
  anchor: HTMLElement;
  start: number;
  end: number;
  fields: InputField[];
} | null>(null);

/** Why a value cannot be written in the tag, or null. */
function valueRefusal(value: string): string | null {
  if (isWritableValue(value)) return null;
  if (value.includes("[")) return "Нельзя использовать символ [";
  if (/[\r\n]/.test(value)) return "Пишется в одну строку";
  if (/" *\]/.test(value)) return "Нельзя ставить кавычку перед ]";
  return "Нельзя ставить кавычку перед словом со знаком =";
}

/** The word stands between the two halves of the tag: one line, no bracket. */
function wordRefusal(value: string): string | null {
  if (!value.trim()) return null;
  if (value.includes("[")) return "Нельзя использовать символ [";
  if (/[\r\n]/.test(value)) return "Слово пишется в одну строку";
  return null;
}

/** The address of the picture, by the rule the page shows it by. */
function addressRefusal(value: string): string | null {
  if (!value.trim()) return null;
  if (isTipImageAddress(value)) return valueRefusal(value.trim());
  return /^(https?:\/\/|\/)/i.test(value.trim())
    ? "Адрес ведет в локальную сеть или записан с ошибкой"
    : "Адрес начинается с http://, https:// или /";
}

function fieldsOf(kind: Kind, word: string): InputField[] {
  const wordField: InputField = {
    name: "word",
    label: "Слово",
    type: "text",
    required: true,
    validator: wordRefusal,
    value: word,
  };
  return kind === "text"
    ? [
        wordField,
        {
          name: "text",
          label: "Текст подсказки",
          type: "text",
          required: true,
          validator: valueRefusal,
        },
      ]
    : [
        wordField,
        {
          name: "img",
          label: "URL изображения",
          type: "text",
          placeholder: "https://example.com/image.jpg или /images/a.gif",
          required: true,
          validator: addressRefusal,
        },
        {
          name: "text",
          label: "Подпись под изображением",
          type: "text",
          required: false,
          validator: valueRefusal,
        },
      ];
}

// The name of each button and of its panel: what it puts on the word. Not
// "on hover": a phone has none, and the word opens on a tap there.
const PANEL_LABEL: Record<Kind, string> = {
  text: "Текст к слову",
  img: "Картинка к слову",
};

/** Open the panel of one kind under its button, or close it when it is open. */
function toggle(kind: Kind) {
  if (insertion.value?.kind === kind) {
    insertion.value = null;
    return;
  }
  const field = textarea.value;
  const anchor = buttons.value[kind];
  if (!field || !anchor) return;
  const start = field.selectionStart ?? text.value.length;
  const end = field.selectionEnd ?? start;
  insertion.value = {
    kind,
    anchor,
    start,
    end,
    fields: fieldsOf(kind, text.value.slice(start, end)),
  };
}

/** The tag, the picture first and its text after it, as the design writes it. */
function write(values: Record<string, string>): string {
  const img = values.img?.trim();
  const caption = values.text ?? "";
  let opening = "[tip";
  if (img) opening += ` img="${img}"`;
  if (caption.trim()) opening += ` text="${caption}"`;
  return `${opening}]${values.word}[/tip]`;
}

async function submit(values: Record<string, string>) {
  const open = insertion.value;
  if (!open) return;
  insertion.value = null;
  const tag = write(values);
  text.value =
    text.value.slice(0, open.start) + tag + text.value.slice(open.end);
  await nextTick();
  const caret = open.start + tag.length;
  textarea.value?.focus();
  textarea.value?.setSelectionRange(caret, caret);
}

function closePanel() {
  insertion.value = null;
}

/** The check, as the page reads the text: one finding to a line. */
const issues = computed(() => validateBBCode(text.value, "hint"));
</script>

<template>
  <div class="hint-toolbar" role="toolbar" aria-label="Разметка описания">
    <Tooltip :text="PANEL_LABEL.text">
      <button
        :ref="(el) => (buttons.text = el as HTMLElement | null)"
        type="button"
        class="tag-btn"
        :aria-label="PANEL_LABEL.text"
        :aria-expanded="insertion?.kind === 'text'"
        @click="toggle('text')"
      >
        tip text
      </button>
    </Tooltip>
    <Tooltip :text="PANEL_LABEL.img">
      <button
        :ref="(el) => (buttons.img = el as HTMLElement | null)"
        type="button"
        class="tag-btn"
        :aria-label="PANEL_LABEL.img"
        :aria-expanded="insertion?.kind === 'img'"
        @click="toggle('img')"
      >
        tip img
      </button>
    </Tooltip>
  </div>
  <InsertPanel
    v-if="insertion"
    :key="insertion.kind"
    :label="PANEL_LABEL[insertion.kind]"
    :fields="insertion.fields"
    :anchor="insertion.anchor"
    @submit="submit"
    @close="closePanel"
  />
  <textarea
    :id="props.id"
    ref="textarea"
    v-model="text"
    :rows="props.rows"
  ></textarea>
  <div
    v-if="issues.length"
    class="hint-check"
    role="alert"
    aria-live="polite"
    aria-atomic="true"
  >
    <div
      v-for="(issue, index) in issues"
      :key="`${index}:${issue.message}`"
      class="hint-check-line"
    >
      <span class="hint-check-icon" aria-hidden="true">{{
        symbols.warning
      }}</span>
      <span>{{ issue.message }}</span>
    </div>
  </div>
</template>

<style scoped lang="sass">
// The row of the buttons over the field: the buttons of the editor's toolbar,
// named after their tag, as on DM2 (BBCodeEditor.vue, .tag-btn).
.hint-toolbar
  display: flex
  gap: $minor
  margin-bottom: $small

.tag-btn
  padding: $tiny 6px
  border: 1px solid $border
  border-radius: 2px
  background: transparent
  cursor: pointer
  font-size: 12px
  font-family: inherit
  color: $text

  &:hover
    background-color: $bg-element-accent

  &[aria-expanded="true"]
    background-color: $bg-element-accent

// The markup check under the field, in the band of the editor's own check: one
// finding to a line, each after the warning sign in the colour of a refusal.
.hint-check
  margin-top: $small
  padding: $small $medium
  background-color: $control-bg-overlay

.hint-check-line
  display: flex
  align-items: center
  gap: $small
  padding: 2px 0
  font-size: $secondary-font-size
  color: $accent-red

.hint-check-icon
  font-size: 12px
</style>
