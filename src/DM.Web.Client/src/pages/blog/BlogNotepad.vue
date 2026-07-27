<script setup lang="ts">
// Blog notepad — shared notes for the blog owner and assistants, hidden from
// readers (dev doc 4.2.3.6.4 "Заметки блога"). Mirrors GameNotepad's two-pane
// entry UI, backed by the blog notepad blogApi. Access is gated to
// owner + assistant; the backend enforces this too (BlogNotepadService).
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { BlogNotepadEntry } from "@/entities/blog";
import { symbols } from "@/shared/lib/utils/icons";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";

const route = useRoute();
const toast = useToast();
const blogStore = useBlogDetailsStore();
const { canUseNotepad } = storeToRefs(blogStore);

// Notepad endpoints accept the public id (they are publicId-tolerant), so the
// raw route param is enough — no need to wait for the blog GUID to resolve.
const blogId = computed(() => route.params.id as string);

// The owner, assistants, and the assigned mentor may see/manage the blog
// notepad (mirrors the backend BlogNotepadService gate).
const canAccess = computed(() => canUseNotepad.value);

const entries = ref<BlogNotepadEntry[]>([]);
const loading = ref(false);
const saving = ref(false);

// Editor state
const showEditor = ref(false);
const editingEntry = ref<BlogNotepadEntry | null>(null);
const editorTitle = ref("");
const editorContent = ref("");

// Selected entry for viewing
const selectedEntry = ref<BlogNotepadEntry | null>(null);

// Entry pending delete confirmation (ConfirmDialog idiom)
const pendingDelete = ref<BlogNotepadEntry | null>(null);

const sortedEntries = computed(() =>
  [...entries.value].sort((a, b) => a.sortOrder - b.sortOrder),
);

const fetchEntries = async () => {
  if (!canAccess.value) return;
  loading.value = true;
  try {
    const { data } = await blogApi.getNotepad(blogId.value);
    entries.value = data?.resources || [];
  } catch {
    toast.error("Не удалось загрузить заметки");
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

const openEditEntryEditor = (entry: BlogNotepadEntry) => {
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
      const { data } = await blogApi.updateNote(
        blogId.value,
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
      const { data } = await blogApi.createNote(blogId.value, {
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

const confirmDelete = async () => {
  const entry = pendingDelete.value;
  pendingDelete.value = null;
  if (!entry) return;

  try {
    await blogApi.deleteNote(blogId.value, entry.id);
    entries.value = entries.value.filter((e) => e.id !== entry.id);
    if (selectedEntry.value?.id === entry.id) selectedEntry.value = null;
    toast.success("Запись удалена");
  } catch {
    toast.error("Не удалось удалить запись");
  }
};

const selectEntry = (entry: BlogNotepadEntry) => {
  selectedEntry.value = entry;
};

onMounted(() => fetchEntries());
</script>

<template>
  <div class="notepad-page">
    <div v-if="!canAccess" class="notepad-denied">
      <secondary-text
        >Заметки доступны только мастеру блога и ассистентам</secondary-text
      >
    </div>

    <template v-else>
      <div class="page-header">
        <h2 class="page-title">Заметки блога</h2>
        <button class="add-btn" @click="openNewEntryEditor">
          Новая запись
        </button>
      </div>

      <secondary-text v-if="loading">Загрузка...</secondary-text>

      <template v-else-if="entries.length === 0 && !showEditor">
        <secondary-text>Заметок пока нет</secondary-text>
        <p class="hint">
          Создайте первую запись для общих заметок мастера блога и ассистентов.
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
                <button
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
      :show="!!pendingDelete"
      title="Удаление записи"
      :message="`Удалить запись &quot;${pendingDelete?.title ?? ''}&quot;?`"
      confirm-label="Удалить"
      danger
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

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
</style>
