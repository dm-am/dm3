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
import { useToast } from "@/shared/lib/composables/useToast";
import type { NotepadAdapter } from "./types";
import { notifyFailure } from "@/shared/lib/errors";

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
    /** Toast text when the list request fails. */
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

const toast = useToast();

const entries = ref<NotepadEntry[]>([]);
const loading = ref(false);
const saving = ref(false);
const deleting = ref(false);

// Editor state
const showEditor = ref(false);
const editingEntry = ref<NotepadEntry | null>(null);
const editorTitle = ref("");
const editorContent = ref("");

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
  const { data, error } = await props.adapter.list();
  loading.value = false;
  if (error) {
    notifyFailure(error, props.loadErrorText);
    return;
  }
  entries.value = data?.resources ?? [];
}

function openNewEntryEditor() {
  editingEntry.value = null;
  editorTitle.value = "";
  editorContent.value = "";
  showEditor.value = true;
}

function openEditEntryEditor(entry: NotepadEntry) {
  editingEntry.value = entry;
  editorTitle.value = entry.title;
  editorContent.value = entry.content;
  showEditor.value = true;
}

function closeEditor() {
  showEditor.value = false;
  editingEntry.value = null;
  editorTitle.value = "";
  editorContent.value = "";
}

async function saveEntry() {
  if (saving.value) return;
  if (!editorTitle.value.trim()) {
    toast.error("Введите название записи");
    return;
  }

  const input = { title: editorTitle.value, content: editorContent.value };
  const editing = editingEntry.value;

  saving.value = true;
  const { data, error } = editing
    ? await props.adapter.update(editing.id, input)
    : await props.adapter.create(input);
  saving.value = false;

  if (error || !data?.resource) {
    toast.error("Не удалось сохранить запись");
    return;
  }

  const saved = data.resource;
  if (editing) {
    const index = entries.value.findIndex((e) => e.id === editing.id);
    if (index !== -1) entries.value[index] = saved;
    if (selectedEntry.value?.id === saved.id) selectedEntry.value = saved;
    toast.success("Запись обновлена");
  } else {
    entries.value.push(saved);
    toast.success("Запись создана");
  }
  closeEditor();
}

async function confirmDelete() {
  const entry = pendingDelete.value;
  if (!entry || deleting.value) return;

  deleting.value = true;
  const { error } = await props.adapter.remove(entry.id);
  deleting.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить запись");
    return;
  }

  pendingDelete.value = null;
  entries.value = entries.value.filter((e) => e.id !== entry.id);
  if (selectedEntry.value?.id === entry.id) selectedEntry.value = null;
  toast.success("Запись удалена");
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
  let nextIndex: number | null = null;

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
        <button class="add-btn" @click="openNewEntryEditor">
          Новая запись
        </button>
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

            <div class="editor-form">
              <div class="form-field">
                <label for="entry-title">Название</label>
                <input
                  id="entry-title"
                  v-model="editorTitle"
                  type="text"
                  placeholder="Название записи"
                  maxlength="200"
                />
              </div>

              <div class="form-field">
                <label for="entry-content">Содержимое</label>
                <textarea
                  id="entry-content"
                  v-model="editorContent"
                  placeholder="Текст записи..."
                  rows="12"
                ></textarea>
              </div>

              <div class="editor-actions">
                <button
                  class="cancel-btn"
                  :disabled="saving"
                  @click="closeEditor"
                >
                  Отмена
                </button>
                <Button :loading="saving" @click="saveEntry">Сохранить</Button>
              </div>
            </div>
          </div>

          <!-- View mode -->
          <template v-else-if="selectedEntry">
            <div class="content-header">
              <h2>{{ selectedEntry.title }}</h2>
              <div
                v-if="canEditSelected || canDeleteSelected"
                class="content-actions"
              >
                <button
                  v-if="canEditSelected"
                  class="edit-btn"
                  @click="openEditEntryEditor(selectedEntry)"
                >
                  Редактировать
                </button>
                <button
                  v-if="canDeleteSelected"
                  class="delete-btn"
                  @click="pendingDelete = selectedEntry"
                >
                  Удалить
                </button>
              </div>
            </div>
            <div class="content-meta">
              <span
                >Создано: <human-date :date="selectedEntry.createdUtc"
              /></span>
              <span v-if="selectedEntry.modifiedUtc">
                | Изменено: <human-date :date="selectedEntry.modifiedUtc" />
              </span>
            </div>
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
      title="Удаление записи"
      :message="`Удалить запись &quot;${pendingDelete?.title ?? ''}&quot;?`"
      confirm-label="Удалить"
      danger
      :loading="deleting"
      @update:show="(v) => !v && (pendingDelete = null)"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Skeleton"

.notepad
  display: flex
  flex-direction: column
  min-height: $grid-step * 50

  // The personal notepad is the page, so it carries the page box itself.
  &.standalone
    padding: $medium
    height: 100%
    min-height: 0

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

.add-btn
  display: flex
  align-items: center
  gap: $minor
  +button

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

  @media (max-width: $bp-tablet)
    grid-template-columns: 1fr
    grid-template-rows: auto 1fr

.entry-list
  border: 1px solid $border
  border-radius: $border-radius
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
    border-left: 3px solid $accent-green

  // Keyboard focus ring for a listbox option. Inset, like the expandable
  // table rows in _Tables.sass: the row sits flush inside the list's scroll
  // container, where an outset ring would be clipped.
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: -2px

.entry-title
  font-weight: 500
  color: $text
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.entry-date
  font-size: $tertiary-font-size
  color: $text-muted
  margin-top: 2px

.entry-content
  border: 1px solid $border
  border-radius: $border-radius
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

.close-btn
  width: 32px
  height: 32px
  border: none
  border-radius: 50%
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

.form-field
  margin-bottom: $small

  label
    display: block
    margin-bottom: $minor
    font-weight: 500
    color: $text

  input, textarea
    width: 100%
    padding: $small
    border: 1px solid $border
    border-radius: $border-radius
    background: $input-bg
    color: $text
    font-family: inherit
    font-size: $font-size

    &:focus:not(:focus-visible)
      outline: none
      border-color: $accent-green

  textarea
    resize: vertical
    min-height: 200px
    flex: 1

.editor-actions
  display: flex
  justify-content: flex-end
  gap: $small
  margin-top: $medium

.cancel-btn
  +button

.content-header
  display: flex
  justify-content: space-between
  align-items: flex-start
  margin-bottom: $small

  h2
    margin: 0
    flex: 1

.content-actions
  display: flex
  gap: $minor

.edit-btn
  +button-outline($link)

.delete-btn
  +button-outline($accent-red)

.content-meta
  font-size: 0.8rem
  color: $text-muted
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
