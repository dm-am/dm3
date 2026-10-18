<script setup lang="ts">
/**
 * NotepadBoard — the two-pane notepad (entry list + reader/editor) shared by
 * the personal, game-master and blog notepads.
 *
 * These were three copies of one screen and they had already drifted: only the
 * game copy had a loading skeleton, only the blog copy asked before deleting,
 * and each spelled its own toast text. The board is the single copy; a page
 * supplies the endpoints through `adapter` and the wording through props.
 *
 * `standalone` marks the personal notepad, which is a page in its own right
 * (own h1, own page padding). The game and blog notepads are tabs inside a
 * container page and carry an h2 with no outer padding.
 */
import { computed, onMounted, ref, watch } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import type { NotepadEntry } from "@/shared/api/models/notepads";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import type { NotepadAdapter } from "./types";
import { refusalHere } from "@/shared/lib/errors";
import { ErrorState } from "@/shared/ui/ErrorState";
import { FormField } from "@/shared/ui/Form";

const props = withDefaults(
  defineProps<{
    adapter: NotepadAdapter;
    /** Heading above the board. */
    title: string;
    /** Shown instead of the board when the viewer may not open this notepad. */
    accessible?: boolean;
    deniedText?: string;
    /** Empty state: the line and the sentence under it. */
    emptyText: string;
    emptyHint: string;
    /** The line in place of the list when the list request fails. */
    loadErrorText: string;
    /**
     * Who is looking. The per-entry controls repeat the server's rules for one
     * entry, which are decided by authorship: an entry is edited by the one who
     * wrote it and by nobody else, because rewritten by another hand it would
     * still stand under their name. Null for a viewer with no account, who
     * writes nowhere.
     */
    viewerId: string | null;
    /**
     * Whether the viewer leads the container and may therefore remove an entry
     * somebody else wrote — the game master, the blog owner. Deleting is the
     * one place where a lead outranks the author; editing is not.
     */
    canDeleteOthers?: boolean;
    /** Personal notepad: own page, h1 and page padding. */
    standalone?: boolean;
  }>(),
  {
    accessible: true,
    deniedText: "",
    canDeleteOthers: false,
    standalone: false,
  },
);

const entries = ref<NotepadEntry[]>([]);
const loading = ref(false);
/**
 * Why the list did not arrive, in the place of the list with "Повторить". It
 * used to be a plate over an empty notepad that read as one with no entries.
 */
const loadRefusal = ref("");
const saving = ref(false);

// Editor state
const showEditor = ref(false);
const editingEntry = ref<NotepadEntry | null>(null);
const editorTitle = ref("");
const editorContent = ref("");

/**
 * Why the title is missing, printed under the field.
 *
 * The check is the client's own and runs before any request, so nothing is on
 * the wire to talk about: the reader is standing in the editor with the cursor
 * two lines above the empty field. A toast said it in the far corner of the
 * screen, away from the field it was about, while the rest of the site answers
 * a failed field with a red line under it — the one the shared FormField
 * prints (shared/ui/Form/FormField.vue), and the one the nearest twin of this
 * editor already uses for the very same check
 * (features/attribute-schema-editor, "Введите название схемы").
 */
const titleError = ref("");

/**
 * Why the last save was refused, printed in the editor above its buttons.
 *
 * A refused save leaves the editor standing with the text still in it, so the
 * reader is looking at the editor and not at the corner a toast rises in. The
 * refusal line of a form (p.form-error, the shape features/edit-character and
 * features/attribute-schema-editor print) stands where the eye already is and
 * stays until the next attempt or until the editor closes, however long the
 * reader takes to read it. The toast that stood here said the same thing in
 * the far corner and, raised directly, said it beside the interceptor's own
 * plate on a 403, 429 or 5xx: one press, two red boxes. A refusal has one
 * carrier (UI_STANDARDS, "Всплывающие сообщения"); the adapter has no seam for
 * `ownsRefusal`, so this screen keeps quiet on the statuses the interceptor
 * announces and prints the rest in the server's words.
 */
const saveRefusal = ref("");

/** The board's own sentence, for a refusal the server did not word. */
const SAVE_REFUSED = "Не удалось сохранить запись";

// Selected entry for viewing
const selectedEntry = ref<NotepadEntry | null>(null);

// Entry awaiting delete confirmation (ConfirmDialog idiom).
const pendingDelete = ref<NotepadEntry | null>(null);

const sortedEntries = computed(() =>
  [...entries.value].sort((a, b) => a.sortOrder - b.sortOrder),
);

// The two controls over the entry on the right, each the client half of a
// server rule. Offered on every entry, they earned a 403 the moment a notepad
// held two authors — the notes of a game are shared by the master and the
// assistants, and so are the notes kept about a character.
const canEditSelected = computed(
  () =>
    !!selectedEntry.value &&
    !!props.viewerId &&
    selectedEntry.value.authorId === props.viewerId,
);

const canDeleteSelected = computed(
  () =>
    !!selectedEntry.value &&
    !!props.viewerId &&
    (selectedEntry.value.authorId === props.viewerId || props.canDeleteOthers),
);

async function fetchEntries() {
  if (!props.accessible) return;
  loading.value = true;
  loadRefusal.value = "";
  const { data, error } = await props.adapter.list();
  loading.value = false;
  if (error) {
    // An expired session and a dead connection keep their plate, and the
    // place of the list still says the list is not there.
    loadRefusal.value =
      refusalHere(error, props.loadErrorText) ?? props.loadErrorText;
    return;
  }
  entries.value = data?.resources ?? [];
}

function openNewEntryEditor() {
  editingEntry.value = null;
  editorTitle.value = "";
  editorContent.value = "";
  titleError.value = "";
  saveRefusal.value = "";
  showEditor.value = true;
}

function openEditEntryEditor(entry: NotepadEntry) {
  editingEntry.value = entry;
  editorTitle.value = entry.title;
  editorContent.value = entry.content;
  titleError.value = "";
  saveRefusal.value = "";
  showEditor.value = true;
}

function closeEditor() {
  showEditor.value = false;
  editingEntry.value = null;
  editorTitle.value = "";
  editorContent.value = "";
  titleError.value = "";
  saveRefusal.value = "";
}

async function saveEntry() {
  if (saving.value) return;
  if (!editorTitle.value.trim()) {
    titleError.value = "Введите название записи";
    return;
  }
  titleError.value = "";
  saveRefusal.value = "";

  const input = { title: editorTitle.value, content: editorContent.value };
  const editing = editingEntry.value;

  saving.value = true;
  const { data, error } = editing
    ? await props.adapter.update(editing.id, input)
    : await props.adapter.create(input);
  saving.value = false;

  if (error) {
    // The editor explains every refusal - a rejected field, an entry that is
    // gone, a refused action, a wait, a dead server - and stays open with what
    // was typed; the plate the interceptor held for it is taken down.
    saveRefusal.value = refusalHere(error, SAVE_REFUSED) ?? "";
    return;
  }

  const saved = data?.resource;
  if (!saved) {
    // Answered without an entry: nothing to put in the pane and nothing the
    // interceptor would have said, so the line speaks for itself.
    saveRefusal.value = SAVE_REFUSED;
    return;
  }

  if (editing) {
    const index = entries.value.findIndex((e) => e.id === editing.id);
    if (index !== -1) entries.value[index] = saved;
    if (selectedEntry.value?.id === saved.id) selectedEntry.value = saved;
  } else {
    entries.value.push(saved);
    // A created entry opens in the reader pane, the way a created game or blog
    // opens on its own page (features/create-game, features/create-blog): the
    // thing that was just written is what the screen shows next. Here the pane
    // already has the slot for it — `selectedEntry` is the only thing it reads,
    // and the edit branch above re-points it at the saved resource for the same
    // reason. The create endpoint answers with the whole entry
    // (Envelope<NotepadEntryResponse>, the same DTO the list serves), so there
    // is nothing left to fetch. Without this the pane fell back to "Выберите
    // запись для просмотра" — the screen "Отмена" leaves behind — and a toast
    // had to say what the screen refused to show.
    selectedEntry.value = saved;
  }
  closeEditor();
}

/**
 * The confirmation's action: it waits shut for the answer, prints a refusal
 * in itself and closes on success alone, which is also what lets go of
 * `pendingDelete` (the window's `update:show`).
 */
async function deleteEntry() {
  const entry = pendingDelete.value;
  if (!entry) return;

  const result = await props.adapter.remove(entry.id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) {
    entries.value = entries.value.filter((e) => e.id !== entry.id);
    if (selectedEntry.value?.id === entry.id) selectedEntry.value = null;
  }
  return result;
}

function selectEntry(entry: NotepadEntry) {
  selectedEntry.value = entry;
}

// The entry list is an ARIA listbox (UI_STANDARDS, "Autocomplete/Listbox
// Standards"). It used to be a bare div with @click — no role, no tabindex,
// no keydown — while the reader pane and the per-entry edit/delete buttons
// exist only under `selectedEntry`: a keyboard user could open nothing at
// all on any of the three notepad pages.
//
// Roving tabindex, same mechanics as shared/ui/Tabs: exactly one option sits
// in the Tab cycle — the selected entry, or the first one while nothing is
// selected, so the list is never skipped over. Arrow keys, Home and End move
// the focus and the selection follows it; Enter and Space take the option the
// keyboard entered the list on, which is how the first entry gets opened from
// a cold start.
const entryList = ref<HTMLElement | null>(null);

const tabbableEntryId = computed(
  () => selectedEntry.value?.id ?? sortedEntries.value[0]?.id ?? null,
);

function focusEntry(index: number) {
  const options =
    entryList.value?.querySelectorAll<HTMLElement>('[role="option"]');
  options?.[index]?.focus();
}

function onEntryKeydown(event: KeyboardEvent, index: number) {
  const list = sortedEntries.value;
  const last = list.length - 1;
  let nextIndex: number;

  switch (event.key) {
    case "ArrowDown":
      nextIndex = index === last ? 0 : index + 1;
      break;
    case "ArrowUp":
      nextIndex = index === 0 ? last : index - 1;
      break;
    case "Home":
      nextIndex = 0;
      break;
    case "End":
      nextIndex = last;
      break;
    case "Enter":
    case " ":
      nextIndex = index;
      break;
    default:
      return;
  }

  event.preventDefault();
  selectEntry(list[nextIndex]);
  focusEntry(nextIndex);
}

// Access is resolved from a container store, so it can flip from false to true
// after the container finishes loading — the first fetch would otherwise be
// skipped and the notepad would stay empty for everyone.
watch(() => props.accessible, fetchEntries);

onMounted(fetchEntries);
</script>

<template>
  <div class="notepad" :class="{ standalone }">
    <div v-if="!accessible" class="notepad-denied">
      <secondary-text>{{ deniedText }}</secondary-text>
    </div>

    <template v-else>
      <div class="page-header">
        <page-title v-if="standalone">{{ title }}</page-title>
        <h2 v-else class="page-title">{{ title }}</h2>
        <!-- Secondary: it opens a form and is no form's action. With the editor
             open, "Сохранить" is the one main button on screen. -->
        <Button type="button" class="add-btn" @click="openNewEntryEditor">
          Новая запись
        </Button>
      </div>

      <!-- Loading: geometric twin of the loaded two-pane layout
           (skeleton-parity). Reuses .notepad-layout / .entry-list /
           .entry-content so grid tracks, borders and paddings match by
           construction; the right pane mirrors the initial no-selection
           state (no phantom content). -->
      <div v-if="loading" class="notepad-layout" aria-hidden="true">
        <div class="entry-list">
          <div v-for="i in 3" :key="i" class="skeleton-entry">
            <div class="skeleton-entry-title" />
            <div class="skeleton-entry-date" />
          </div>
        </div>
        <div class="entry-content">
          <div class="no-selection">
            <div class="skeleton-content-hint" />
          </div>
        </div>
      </div>

      <ErrorState
        v-else-if="loadRefusal && entries.length === 0 && !showEditor"
        :message="loadRefusal"
        :retry="fetchEntries"
      />

      <template v-else-if="entries.length === 0 && !showEditor">
        <secondary-text>{{ emptyText }}</secondary-text>
        <p class="hint">{{ emptyHint }}</p>
      </template>

      <div v-else class="notepad-layout">
        <!-- Entry list -->
        <div
          ref="entryList"
          class="entry-list"
          role="listbox"
          :aria-label="title"
        >
          <div
            v-for="(entry, index) in sortedEntries"
            :key="entry.id"
            class="entry-item"
            :class="{ selected: selectedEntry?.id === entry.id }"
            role="option"
            :aria-selected="selectedEntry?.id === entry.id"
            :tabindex="entry.id === tabbableEntryId ? 0 : -1"
            @click="selectEntry(entry)"
            @keydown="onEntryKeydown($event, index)"
          >
            <div class="entry-title">{{ entry.title }}</div>
            <div class="entry-date">
              <human-date :date="entry.modifiedUtc || entry.createdUtc" />
            </div>
          </div>
        </div>

        <!-- Entry content / Editor -->
        <div class="entry-content">
          <!-- Editor mode -->
          <div v-if="showEditor" class="editor">
            <div class="editor-header">
              <h3>
                {{ editingEntry ? "Редактирование записи" : "Новая запись" }}
              </h3>
              <button
                class="close-btn"
                aria-label="Закрыть"
                @click="closeEditor"
              >
                {{ symbols.close }}
              </button>
            </div>

            <!-- A form of the page: the fields of the site with their captions
                 and the red line under a field that failed, the buttons under
                 the last field at the left, the main one first. -->
            <div class="editor-form">
              <FormField
                label="Название"
                :errors="titleError ? [titleError] : []"
              >
                <input
                  id="entry-title"
                  v-model="editorTitle"
                  type="text"
                  placeholder="Название записи"
                  maxlength="200"
                  @input="titleError = ''"
                />
              </FormField>

              <FormField label="Содержимое">
                <textarea
                  id="entry-content"
                  v-model="editorContent"
                  placeholder="Текст записи..."
                  rows="12"
                ></textarea>
              </FormField>

              <!--
                The refusal of the last save, in the form's own line above its
                buttons: the site's answer to a save the server would not take
                while the form stays on screen (features/edit-character puts
                it in the same place). Not a toast - see `saveRefusal`.
              -->
              <p v-if="saveRefusal" class="form-error" role="alert">
                {{ saveRefusal }}
              </p>

              <div class="editor-actions">
                <Button variant="primary" :loading="saving" @click="saveEntry">
                  Сохранить
                </Button>
                <Button
                  class="cancel-btn"
                  :disabled="saving"
                  @click="closeEditor"
                >
                  Отмена
                </Button>
              </div>
            </div>
          </div>

          <!-- View mode -->
          <template v-else-if="selectedEntry">
            <h2 class="content-title">{{ selectedEntry.title }}</h2>
            <!-- The entry's caption and what the viewer may do with it, one
                 line: the controls used to be buttons beside the title, where
                 a long title pushed "Удалить" out of the pane. -->
            <CaptionLine class="content-meta">
              <span
                >Создано: <human-date :date="selectedEntry.createdUtc"
              /></span>
              <span v-if="selectedEntry.modifiedUtc"
                >Изменено: <human-date :date="selectedEntry.modifiedUtc"
              /></span>
              <CaptionAction
                v-if="canEditSelected"
                @click="openEditEntryEditor(selectedEntry)"
                >Редактировать</CaptionAction
              >
              <CaptionAction
                v-if="canDeleteSelected"
                @click="pendingDelete = selectedEntry"
                >Удалить</CaptionAction
              >
            </CaptionLine>
            <div class="content-body">
              <pre>{{ selectedEntry.content }}</pre>
            </div>
          </template>

          <!-- No selection -->
          <div v-else class="no-selection">
            <secondary-text>Выберите запись для просмотра</secondary-text>
          </div>
        </div>
      </div>
    </template>

    <ConfirmDialog
      :show="pendingDelete !== null"
      title="Удалить запись?"
      :message="`Запись &quot;${pendingDelete?.title ?? ''}&quot; будет удалена без возможности восстановления`"
      confirm-label="Удалить"
      failure="Не удалось удалить запись"
      :action="deleteEntry"
      @update:show="(v) => !v && (pendingDelete = null)"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Skeleton" as *

.notepad
  display: flex
  flex-direction: column
  min-height: $grid-step * 50

  // The personal notepad is the page. No padding of its own: it pushed the
  // heading 16 to the right of every other page of the column, as the
  // notifications had it. The heading row keeps the top of a
  // page's heading ($medium, as PageTitle stands).
  &.standalone
    height: 100%
    min-height: 0

    .page-header
      margin: $medium 0

.notepad-denied
  padding: $big

.page-header
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $medium

  // PageTitle renders an h1 (standalone), the tab variant an h2. Both sit
  // flush in the header.
  h1
    margin: 0

.page-title
  margin: 0

.hint
  color: $text-muted
  font-size: 0.9rem
  margin-top: $small

.notepad-layout
  display: grid
  grid-template-columns: 300px 1fr
  gap: $medium
  flex: 1
  min-height: 0

.entry-list
  border: 1px solid $border
  overflow-y: auto
  background: $bg-element

.entry-item
  padding: $small
  border-bottom: 1px solid $border
  cursor: pointer
  transition: background-color 0.2s

  &:last-child
    border-bottom: none

  &:hover
    background: $hover-overlay

  &.selected
    background: $selected-overlay

  // Keyboard focus ring for a listbox option. Inset, like the expandable
  // table rows in _Tables.sass: the row sits flush inside the list's scroll
  // container, where an outset ring would be clipped.
  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

// A long title wraps, whole, rather than losing its end under "...".
.entry-title
  font-weight: 500
  color: $text

.entry-date
  font-size: $tertiary-font-size
  color: $text-muted
  margin-top: 2px

.entry-content
  border: 1px solid $border
  padding: $medium
  overflow-y: auto
  background: $bg-element

.no-selection
  display: flex
  align-items: center
  justify-content: center
  height: 100%

.editor
  height: 100%
  display: flex
  flex-direction: column

.editor-header
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $medium

  h3
    margin: 0

// A control, rounded as every control of the site is, the cross of a window
// included (_DialogSurface.sass).
.close-btn
  width: 32px
  height: 32px
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text-muted
  cursor: pointer
  font-size: 1.5rem
  line-height: 1
  display: flex
  align-items: center
  justify-content: center

  &:hover
    background: $hover-overlay
    color: $text

.editor-form
  flex: 1
  display: flex
  flex-direction: column

// The fields are the site's own (FormField, +input): the line at the page's
// control height, the caption, the frame, the ring, the red line under a
// field that failed. Only the growth of the text area is this editor's.
.editor-form textarea
  resize: vertical
  min-height: 200px

// The form's refusal line, in the colour and size features/edit-character
// prints its own: one sentence in the accent, no box of its own. No margin of
// its own either - the field above and the buttons below carry the spacing.
.form-error
  margin: 0
  color: $accent-red
  font-size: $secondary-font-size

// Under the last field, $medium away, the main button first: the row every
// form of a page has.
.editor-actions
  display: flex
  gap: $small
  margin-top: $medium

.content-title
  margin: 0 0 $small

// The caption line draws its own size and colour; here it only stands as a
// block of its own above the text.
.content-meta
  display: block
  margin-bottom: $medium

.content-body
  pre
    white-space: pre-wrap
    word-wrap: break-word
    font-family: inherit
    margin: 0
    color: $text

// --- Loading skeleton (twin of .entry-item rows, skeleton-parity) ---

// Twin of .entry-item: $small padding + 1px separator. Inner bars mirror
// the real line boxes: title 16px x 1.3 ~ 21px (16px bar + 2px margins),
// date 12px x 1.3 ~ 16px with its 2px top margin (12px bar + 4px margin).
.skeleton-entry
  padding: $small
  border-bottom: 1px solid $border

  &:last-child
    border-bottom: none

.skeleton-entry-title
  width: 70%
  height: 16px
  margin: 2px 0
  +skeleton-shimmer

.skeleton-entry-date
  width: 90px
  height: 12px
  margin-top: 4px
  +skeleton-shimmer

// Twin of the centered "Выберите запись…" secondary-text line (14px).
.skeleton-content-hint
  width: 220px
  height: 14px
  +skeleton-shimmer
</style>
