<script setup lang="ts">
// Game master notepad — shared notes for the master, assistants and the game
// mentor, hidden from players and readers (MasterNotepad, see GLOSSARY).
// Reuses the personal notepad's two-pane entry UI, backed by the game notepad
// gameApi. Access is gated to master + assistant + mentor; the backend
// enforces this too.
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import type { NotepadEntry } from "@/entities/game";
import { symbols } from "@/shared/lib/utils/icons";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { useToast } from "@/shared/lib/composables/useToast";

const route = useRoute();
const toast = useToast();
const gameStore = useGameDetailsStore();
const { isMaster, isAssistant, isMentor } = storeToRefs(gameStore);

// Notepad endpoints accept the public id (they are publicId-tolerant), so the
// raw route param is enough — no need to wait for the game GUID to resolve.
const gameId = computed(() => route.params.id as string);

// Only the master, assistants and the game mentor may see/manage the notepad.
const canAccess = computed(
  () => isMaster.value || isAssistant.value || isMentor.value,
);

const entries = ref<NotepadEntry[]>([]);
const loading = ref(false);
const saving = ref(false);

// Editor state
const showEditor = ref(false);
const editingEntry = ref<NotepadEntry | null>(null);
const editorTitle = ref("");
const editorContent = ref("");

// Selected entry for viewing
const selectedEntry = ref<NotepadEntry | null>(null);

const sortedEntries = computed(() =>
  [...entries.value].sort((a, b) => a.sortOrder - b.sortOrder),
);

const fetchEntries = async () => {
  if (!canAccess.value) return;
  loading.value = true;
  try {
    const { data } = await gameApi.getNotepad(gameId.value);
    entries.value = data?.resources || [];
  } catch {
    toast.error("Не удалось загрузить блокнот");
  } finally {
    loading.value = false;
  }
};

const openNewEntryEditor = () => {
  editingEntry.value = null;
  editorTitle.value = "";
  editorContent.value = "";
  showEditor.value = true;
};

const openEditEntryEditor = (entry: NotepadEntry) => {
  editingEntry.value = entry;
  editorTitle.value = entry.title;
  editorContent.value = entry.content;
  showEditor.value = true;
};

const closeEditor = () => {
  showEditor.value = false;
  editingEntry.value = null;
  editorTitle.value = "";
  editorContent.value = "";
};

const saveEntry = async () => {
  if (!editorTitle.value.trim()) {
    toast.error("Введите название записи");
    return;
  }

  saving.value = true;
  try {
    if (editingEntry.value) {
      const { data } = await gameApi.updateNote(
        gameId.value,
        editingEntry.value.id,
        { title: editorTitle.value, content: editorContent.value },
      );
      const index = entries.value.findIndex(
        (e) => e.id === editingEntry.value!.id,
      );
      if (index !== -1 && data) {
        entries.value[index] = data;
        if (selectedEntry.value?.id === data.id) selectedEntry.value = data;
      }
      toast.success("Запись обновлена");
    } else {
      const { data } = await gameApi.createNote(gameId.value, {
        title: editorTitle.value,
        content: editorContent.value,
      });
      if (data?.resource) entries.value.push(data.resource);
      toast.success("Запись создана");
    }
    closeEditor();
  } catch {
    toast.error("Не удалось сохранить запись");
  } finally {
    saving.value = false;
  }
};

const deleteEntry = async (entry: NotepadEntry) => {
  if (!confirm(`Удалить запись "${entry.title}"?`)) return;

  try {
    await gameApi.deleteNote(gameId.value, entry.id);
    entries.value = entries.value.filter((e) => e.id !== entry.id);
    if (selectedEntry.value?.id === entry.id) selectedEntry.value = null;
    toast.success("Запись удалена");
  } catch {
    toast.error("Не удалось удалить запись");
  }
};

const selectEntry = (entry: NotepadEntry) => {
  selectedEntry.value = entry;
};

onMounted(() => fetchEntries());
</script>

<template>
  <div class="notepad-page">
    <div v-if="!canAccess" class="notepad-denied">
      <secondary-text
        >Блокнот доступен только мастеру, ассистентам и
        наставнику</secondary-text
      >
    </div>

    <template v-else>
      <div class="page-header">
        <h2 class="page-title">Блокнот мастера</h2>
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
        <secondary-text>Нет записей в блокноте</secondary-text>
        <p class="hint">
          Создайте первую запись для общих заметок мастера и ассистентов.
        </p>
      </template>

      <div v-else class="notepad-layout">
        <!-- Entry list -->
        <div class="entry-list">
          <div
            v-for="entry in sortedEntries"
            :key="entry.id"
            class="entry-item"
            :class="{ selected: selectedEntry?.id === entry.id }"
            @click="selectEntry(entry)"
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
              <button class="close-btn" @click="closeEditor">
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
                <button class="save-btn" :disabled="saving" @click="saveEntry">
                  {{ saving ? "Сохранение..." : "Сохранить" }}
                </button>
              </div>
            </div>
          </div>

          <!-- View mode -->
          <template v-else-if="selectedEntry">
            <div class="content-header">
              <h2>{{ selectedEntry.title }}</h2>
              <div class="content-actions">
                <button
                  class="edit-btn"
                  @click="openEditEntryEditor(selectedEntry)"
                >
                  Редактировать
                </button>
                <button class="delete-btn" @click="deleteEntry(selectedEntry)">
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
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"
@import "src/assets/styles/Skeleton"

.notepad-page
  display: flex
  flex-direction: column
  min-height: $grid-step * 50

.notepad-denied
  padding: $big
  text-align: center

.page-header
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $medium

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

  @media (max-width: 768px)
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

.entry-title
  font-weight: 500
  color: $text
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.entry-date
  font-size: 0.75rem
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
    font-size: 1rem

    &:focus
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

.save-btn
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
  padding: $minor $small
  border: 1px solid $link
  border-radius: $border-radius
  background: transparent
  color: $link
  cursor: pointer
  font-size: 0.85rem

  &:hover
    background: $link
    color: white

.delete-btn
  padding: $minor $small
  border: 1px solid $accent-red
  border-radius: $border-radius
  background: transparent
  color: $accent-red
  cursor: pointer
  font-size: 0.85rem

  &:hover
    background: $accent-red
    color: $text-on-red

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

// Twin of the centered "Выберите запись..." secondary-text line (14px).
.skeleton-content-hint
  width: 220px
  height: 14px
  +skeleton-shimmer
</style>
