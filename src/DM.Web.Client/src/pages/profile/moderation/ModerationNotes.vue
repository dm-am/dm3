<script setup lang="ts">
import { ref } from "vue";
import type { ModNote } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import { moderationApi } from "@/shared/api";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import TheButton from "@/shared/ui/Button/TheButton.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import dayjs from "dayjs";

const props = defineProps<{
  notes: ModNote[];
  canCreate: boolean;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const toast = useToast();

const newNoteText = ref("");
const creating = ref(false);

const editingNoteId = ref<string | null>(null);
const editText = ref("");

function formatDateTime(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY HH:mm");
}

async function createNote() {
  if (!newNoteText.value.trim()) return;
  creating.value = true;
  const { error } = await moderationApi.createModNote(
    props.targetUsername as Username,
    newNoteText.value.trim(),
  );
  creating.value = false;
  if (error) {
    toast.error("Не удалось создать заметку");
    return;
  }
  newNoteText.value = "";
  emit("updated");
}

function startEdit(note: ModNote) {
  editingNoteId.value = note.noteId;
  editText.value = note.text;
}

function cancelEdit() {
  editingNoteId.value = null;
  editText.value = "";
}

async function saveEdit(noteId: string) {
  if (!editText.value.trim()) return;
  const { error } = await moderationApi.updateModNote(
    noteId,
    editText.value.trim(),
  );
  if (error) {
    toast.error("Не удалось сохранить заметку");
    return;
  }
  cancelEdit();
  emit("updated");
}

async function deleteNote(noteId: string) {
  if (!confirm("Удалить заметку?")) return;
  const { error } = await moderationApi.deleteModNote(noteId);
  if (error) {
    toast.error("Не удалось удалить заметку");
    return;
  }
  emit("updated");
}
</script>

<template>
  <div class="mod-section">
    <h4 class="mod-section_title">
      Заметки модератора ({{ notes.length }})
    </h4>

    <div v-for="note in notes" :key="note.noteId" class="mod-note">
      <div class="mod-note_header">
        <strong>{{ note.authorUsername }}</strong>
        <secondary-text>{{ formatDateTime(note.createdUtc) }}</secondary-text>
        <secondary-text v-if="note.updatedUtc">
          (изм. {{ formatDateTime(note.updatedUtc) }})
        </secondary-text>
      </div>

      <template v-if="editingNoteId !== note.noteId">
        <div class="mod-note_text">{{ note.text }}</div>
        <div v-if="note.canEdit || note.canDelete" class="mod-note_actions">
          <a v-if="note.canEdit" class="mod-action" @click="startEdit(note)">
            Изменить
          </a>
          <a
            v-if="note.canDelete"
            class="mod-action mod-action-danger"
            @click="deleteNote(note.noteId)"
          >
            Удалить
          </a>
        </div>
      </template>

      <template v-else>
        <div class="mod-note_edit">
          <textarea v-model="editText" rows="3" class="mod-textarea" />
          <div class="mod-note_edit-actions">
            <the-button
              :disabled="!editText.trim()"
              @click="saveEdit(note.noteId)"
            >
              Сохранить
            </the-button>
            <the-button secondary @click="cancelEdit">Отмена</the-button>
          </div>
        </div>
      </template>
    </div>

    <div v-if="canCreate" class="mod-note_create">
      <textarea
        v-model="newNoteText"
        rows="3"
        placeholder="Заметка модератора..."
        class="mod-textarea"
      />
      <the-button
        :disabled="!newNoteText.trim()"
        :loading="creating"
        @click="createNote"
      >
        Добавить заметку
      </the-button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-note
  padding: $small
  border-bottom: 1px solid $bg-element-accent
  &:last-of-type
    border-bottom: none

.mod-note_header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $minor

.mod-note_text
  white-space: pre-wrap
  word-break: break-word

.mod-note_actions
  display: flex
  gap: $small
  margin-top: $minor

.mod-action
  cursor: pointer
  font-size: $secondary-font-size
  color: $link
  &:hover
    color: $link-hover
    text-decoration: underline

.mod-action-danger
  color: $accent-red
  &:hover
    color: $accent-red-hover

.mod-note_edit
  margin-top: $minor

.mod-textarea
  width: 100%
  resize: vertical
  box-sizing: border-box
  +input()

.mod-note_edit-actions
  display: flex
  gap: $small
  margin-top: $small

.mod-note_create
  margin-top: $medium
  display: flex
  flex-direction: column
  gap: $small
</style>
