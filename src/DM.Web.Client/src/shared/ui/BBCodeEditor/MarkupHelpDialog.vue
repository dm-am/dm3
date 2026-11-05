<script setup lang="ts">
// DM2's TagHelp.aspx: the typed half is text as the grammar reads it, the shown half the server's markup.
import { computed, ref, watch } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import { cleanupBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeCleanup";
import { initBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeInteractive";
import {
  HELP_ENTRIES,
  chordLabel,
  typedPieces,
  type HelpEntry,
} from "./lib/markupHelp";
import { DRAFT_KEPT_FOR } from "./lib/editorDraft";

const props = defineProps<{
  modelValue: boolean;
  /** The tags the editor offers on this surface, as its toolbar does. */
  tags: readonly string[];
  /** The editor keeps a draft, and its "load" button brings it back. */
  drafts: boolean;
  /** Ctrl+Enter sends what is written. */
  sends: boolean;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", open: boolean): void;
}>();

const entries = computed<HelpEntry[]>(() =>
  HELP_ENTRIES.filter((entry) => props.tags.includes(entry.tag)),
);

/** The note goes in the cell of the last example, so every row keeps its four cells. */
const notedAt = (entry: HelpEntry, index: number): boolean =>
  Boolean(entry.note) && index === entry.examples.length - 1;

/** Example links point at made-up addresses: a click does nothing. Spoiler and 18+ heads stay live. */
function holdExampleLinks(event: MouseEvent) {
  const anchor = (event.target as Element | null)?.closest?.("a[href]");
  if (anchor && !anchor.matches(".spoiler-head, .nsfw-head")) {
    event.preventDefault();
  }
}

const table = ref<HTMLTableElement | null>(null);

// Wired while the table is there: a spoiler head registers with "Развернуть все" and has to unregister.
watch(
  table,
  (element, previous) => {
    if (previous) {
      previous.removeEventListener("click", holdExampleLinks);
      cleanupBbcodeInteractive(previous);
    }
    if (!element) return;
    initBbcodeInteractive(element);
    element
      .querySelectorAll<HTMLAnchorElement>(
        ".help-shown a[href]:not(.spoiler-head):not(.nsfw-head)",
      )
      .forEach((anchor) => anchor.setAttribute("tabindex", "-1"));
    element.addEventListener("click", holdExampleLinks);
  },
  { flush: "post" },
);
</script>

<template>
  <!-- The editor's window (920): at 580 the widest example broke into scraps. -->
  <Dialog
    editor
    :model-value="modelValue"
    @update:model-value="(open: boolean) => emit('update:modelValue', open)"
  >
    <DialogTitle>Справка по тегам</DialogTitle>
    <p class="help-lead">
      Теги набираются в режиме [bbcode], а кнопки панели ставят их в обоих
      режимах. Тег с ошибкой печатается как набран, и в режиме [bbcode] редактор
      подсказывает, что исправить.
    </p>
    <table ref="table" class="help-table">
      <thead>
        <tr>
          <th scope="col">Тег</th>
          <th scope="col" class="help-half">Как написать</th>
          <th scope="col" class="help-half">Что получится</th>
          <th scope="col">Клавиши</th>
        </tr>
      </thead>
      <tbody
        v-for="entry in entries"
        :key="entry.tag"
        class="help-entry"
        :data-tag="entry.tag"
      >
        <tr v-for="(example, index) in entry.examples" :key="example.typed">
          <th
            v-if="index === 0"
            scope="rowgroup"
            class="help-tag"
            :rowspan="entry.examples.length"
          >
            {{ entry.tag }}
          </th>
          <td class="help-example">
            <div class="help-typed">
              <template
                v-for="(piece, at) in typedPieces(example.typed)"
                :key="at"
                ><wbr v-if="at > 0" />{{ piece }}</template
              >
            </div>
            <p v-if="notedAt(entry, index)" class="help-note">
              {{ entry.note }}
            </p>
          </td>
          <!-- The server's own reading markup, written in markupHelp.ts: a
               constant, never text from a reader. -->
          <td class="help-shown bbcode-content" v-html="example.shown"></td>
          <td
            v-if="index === 0"
            class="help-keys"
            :rowspan="entry.examples.length"
          >
            <kbd v-if="entry.keys">{{ chordLabel(entry.keys) }}</kbd>
          </td>
        </tr>
      </tbody>
      <tbody v-if="drafts" class="help-entry" data-tag="load">
        <tr>
          <th scope="rowgroup" class="help-tag">load</th>
          <td class="help-example">
            <p class="help-note help-note--alone">
              Кнопка на панели, а не тег. Возвращает в поле черновик, который
              редактор сам сохраняет в этом браузере и хранит
              {{ DRAFT_KEPT_FOR }}.
            </p>
          </td>
          <td class="help-shown"></td>
          <td class="help-keys"></td>
        </tr>
      </tbody>
    </table>
    <p v-if="sends" class="help-foot">
      <kbd>Ctrl+Enter</kbd> отправляет написанное
    </p>
  </Dialog>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

// A note stands in the cell of its example: laid across two columns, it broke the rule between them.
.help-lead
  margin: 0 0 $medium
  font-size: $secondary-font-size
  line-height: 1.5
  color: $text-muted

.help-table
  width: 100%
  border-collapse: collapse
  font-size: $secondary-font-size
  line-height: 1.5

  th,
  td
    padding: $table-cell-padding-v $table-cell-padding-h
    border: 1px solid $border
    background-color: $bg-element
    color: $text
    text-align: left
    vertical-align: top

  thead th
    background-color: $bg-element-accent
    font-weight: 700
    white-space: nowrap

// Sized by content, the half with the notes took most of the width.
.help-half
  width: 50%

// Set as the headings of the site are (PageTitle): uppercase, bold, 0.5px apart.
.help-tag
  font-weight: 700
  text-transform: uppercase
  letter-spacing: .5px
  white-space: nowrap

  .help-table &
    color: $heading

// The text's own face: Courier New set the Cyrillic thin and wide. Breaks anywhere rather than push the table out.
.help-typed
  white-space: pre-wrap
  overflow-wrap: anywhere

.help-note
  margin: $minor 0 0
  color: $text-muted

.help-note--alone
  margin: 0
  color: $text

.help-keys
  white-space: nowrap

kbd
  font: inherit

// A result's first block would keep the margin it has in a post and sink under its example.
.help-shown
  > :deep(:first-child)
    margin-top: 0

  > :deep(:last-child)
    margin-bottom: 0

.help-foot
  margin: $medium 0 0
  font-size: $secondary-font-size
  color: $text-muted

  kbd
    color: $text
</style>
