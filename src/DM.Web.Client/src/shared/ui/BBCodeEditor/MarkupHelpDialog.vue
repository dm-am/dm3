<script setup lang="ts">
/**
 * The help of the markup editor, opened by its "help" button: a window of the
 * site with the table DM2 kept in TagHelp.aspx. A row to a tag, in the order of
 * the toolbar: the tag, what the author types and what a reader gets for it,
 * and the keys. The typed half is text as the grammar reads it; the other half
 * is the page's own markup, drawn and opened the way a post is (markupHelp.ts).
 *
 * The rows are the tags of the surface the editor writes for: [private] in a
 * game post only, [mod] for a moderator where the surface reads it. The "load"
 * row and the line about Ctrl+Enter are there only where the editor keeps a
 * draft and where a parent sends on that chord.
 */
import { ref, watch } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import {
  cleanupBbcodeInteractive,
  initBbcodeInteractive,
} from "@/shared/lib/utils/bbcodeInteractive";
import {
  HELP_ENTRIES,
  chordLabel,
  typedPieces,
  type HelpEntry,
} from "./lib/markupHelp";

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

const entries = (): HelpEntry[] =>
  HELP_ENTRIES.filter((entry) => props.tags.includes(entry.tag));

/**
 * Whether the note of an entry goes under this example: under the last one,
 * in its own cell, so every row keeps the four cells of the table.
 */
const notedAt = (entry: HelpEntry, index: number): boolean =>
  Boolean(entry.note) && index === entry.examples.length - 1;

/**
 * An example is not a destination. Its links point at made-up addresses and a
 * made-up profile, and one followed from here would take the reader off the
 * text being written; so a click on one does nothing and Tab passes it by. The
 * heads of a spoiler and of 18+ content stay live: opening them is the point.
 */
function holdExampleLinks(event: MouseEvent) {
  const anchor = (event.target as Element | null)?.closest?.("a[href]");
  if (anchor && !anchor.matches(".spoiler-head, .nsfw-head")) {
    event.preventDefault();
  }
}

const table = ref<HTMLTableElement | null>(null);

// The table is in the window's content, which exists only while the window
// is open. It is wired when it appears and let go when it leaves: a spoiler
// head registers with the page's "Развернуть все" and has to unregister.
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
  <!-- The measure of the editor's window (920): the typed markup and the
       result stand side by side, and at 580 either the widest example or its
       result had to break into scraps. -->
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
        v-for="entry in entries()"
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
              редактор сам сохраняет в этом браузере и хранит неделю.
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

// The table of DM2's help, in the site's own: the rule of a table round every
// cell, the cells in the fill of an element, the heading row one step darker.
// A tag is named in the colour and the capitals of a heading, as DM2 named it.
// Every row has its four cells, each closed by its own rules: an example is a
// row, the typed markup beside what a reader gets for it, and a note stands in
// the cell of the example it follows. A note laid across two columns ended the
// rule between them halfway, and its text ran over the line of the cells above.

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

// The typed and the shown halves share the width the tag and the keys leave
// them. Sized by their content, the half that carries the notes took most of
// it, and a result as short as the line of a hidden block's readers broke into
// three.
.help-half
  width: 50%

.help-tag
  font-weight: 700
  text-transform: uppercase
  letter-spacing: .5px
  white-space: nowrap

  .help-table &
    color: $heading

// The markup as the author types it and sees it in the [bbcode] mode, in the
// text's own face, line breaks kept. Courier New set the Cyrillic of every
// example thin and wide, and the column read worse than the one beside it. It
// breaks between two tags first; a piece wider than a narrow window's column
// breaks where it has to rather than push the table out of the window.
.help-typed
  white-space: pre-wrap
  overflow-wrap: anywhere

// What the examples do not show by themselves, under the last of them, in the
// muted colour of a hint. The draft button has no example, and its word about
// it is the whole cell, in the colour of the text.
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

// The spacing of a result is its own: the first block of a spoiler or a quote
// starts with the margin it has in a post, and here it would push the result
// under the line of its example.
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

// A window narrower than a tablet's has no room for four columns: at 700px
// the halves were 180px wide and an address broke inside its closing tag. An
// entry is then a box of lines, its tag and keys on the first, then each
// example, typed over its result, and the note under the last result. The
// heading row goes: the typed and the shown halves say what they are by their
// faces.
@media (max-width: $bp-tablet)
  .help-table
    &,
    tbody,
    tr,
    th,
    td
      display: block

    thead
      display: none

    tbody
      padding: $small $table-cell-padding-h
      border: 1px solid $border
      background-color: $bg-element

      & + tbody
        border-top: 0

    tr
      display: flex
      flex-wrap: wrap
      align-items: baseline
      column-gap: $small

      & + tr
        margin-top: $small

    th,
    td
      padding: 0
      border: 0

    // The cell of an example gives its typed markup and its note to the line
    // of the entry, so the result can stand between them.
    .help-example
      display: contents

    .help-tag
      flex: 1 1 auto

    .help-keys
      order: 1

    .help-typed,
    .help-note--alone
      order: 2
      flex: 0 0 100%

    .help-shown
      order: 3
      flex: 0 0 100%
      margin-top: $tiny

      &:empty
        display: none

    .help-note
      order: 4
      flex: 0 0 100%

    .help-note--alone
      order: 2
</style>
