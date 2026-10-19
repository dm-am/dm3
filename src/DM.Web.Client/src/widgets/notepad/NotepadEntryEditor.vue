<script setup lang="ts">
/** The editor of one notepad entry: it writes, the board places, a fresh one per opening. */
import { ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import type { NotepadEntry } from "@/shared/api/models/notepads";
import type { NotepadAdapter } from "./types";
import { refusalHere } from "@/shared/lib/errors";
import { FormField } from "@/shared/ui/Form";
import { Button } from "@/shared/ui/Button";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = defineProps<{
  adapter: NotepadAdapter;
  /** The entry being rewritten; null for a new one. */
  entry: NotepadEntry | null;
  /** A listener prop, not an emit: Vue drops the emit of an editor unmounted mid-save. */
  onSaved: (saved: NotepadEntry, edited: NotepadEntry | null) => void;
}>();

const emit = defineEmits<{ cancel: [] }>();

const saving = ref(false);
const editorTitle = ref(props.entry?.title ?? "");
const editorContent = ref(props.entry?.content ?? "");

/** Why the title is missing, under its field, as FormField prints it. */
const titleError = ref("");

/** Why the last save was refused, in the editor above its buttons. */
const saveRefusal = ref("");

/** The editor's own sentence, for a refusal the server did not word. */
const SAVE_REFUSED = "Не удалось сохранить запись";

/** The bound of the entry requests' Title, compared by ClientFieldLimitsShould. */
const TITLE_MAX_LENGTH = 200;

async function saveEntry() {
  if (saving.value) return;
  if (!editorTitle.value.trim()) {
    titleError.value = "Введите название записи";
    return;
  }
  titleError.value = "";
  saveRefusal.value = "";

  const input = { title: editorTitle.value, content: editorContent.value };
  const editing = props.entry;

  saving.value = true;
  const { data, error } = editing
    ? await props.adapter.update(editing.id, input)
    : await props.adapter.create(input);
  saving.value = false;

  if (error) {
    // Every refusal is said here, what was typed kept; the interceptor's plate is taken down.
    saveRefusal.value = refusalHere(error, SAVE_REFUSED) ?? "";
    return;
  }

  const saved = data?.resource;
  if (!saved) {
    // Answered without an entry: nothing the interceptor would have said.
    saveRefusal.value = SAVE_REFUSED;
    return;
  }

  props.onSaved(saved, editing);
}
</script>

<template>
  <div class="editor">
    <div class="editor-header">
      <h3>
        {{ entry ? "Редактирование записи" : "Новая запись" }}
      </h3>
      <button class="close-btn" aria-label="Закрыть" @click="emit('cancel')">
        {{ symbols.close }}
      </button>
    </div>

    <!-- A form of the page: the site's fields, the buttons under the last one -->
    <div class="editor-form">
      <FormField label="Название" :errors="titleError ? [titleError] : []">
        <input
          id="entry-title"
          v-model="editorTitle"
          type="text"
          placeholder="Название записи"
          :maxlength="TITLE_MAX_LENGTH"
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

      <!-- The refusal of the last save, above the buttons -->
      <ActionResult :text="saveRefusal" class="form-error" />

      <div class="editor-actions">
        <Button variant="primary" :loading="saving" @click="saveEntry">
          Сохранить
        </Button>
        <Button class="cancel-btn" :disabled="saving" @click="emit('cancel')">
          Отмена
        </Button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
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

// A control, rounded as the cross of a window is.
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

// The fields are the site's own; only the growth of the text area is this editor's.
.editor-form textarea
  resize: vertical
  min-height: 200px

// No margin of its own: the field above and the buttons below carry the spacing.
.form-error
  --action-result-margin: 0

// Under the last field, the main button first, as every form of a page has it.
.editor-actions
  display: flex
  gap: $small
  margin-top: $medium
</style>
