<script setup lang="ts">
/**
 * ModerationNotes — moderator notes about a user (product doc 4.2.2.20).
 *
 * Divergence from the doc, kept intentionally: the doc frames this as ONE
 * note per profile, but the backend models it as a LIST of authored notes
 * (each carries its own author, timestamps and per-note canEdit/canDelete —
 * see ModeratedProfileNote / GET moderatorNotes). The multi-note model is the
 * richer, correct one — several moderators can leave distinct, attributed
 * notes over time rather than overwriting a single shared field — so it is
 * preserved and flagged rather than collapsed to a single note.
 *
 * The note text is now authored with the BBCode editor (doc: "BBCode editor"),
 * matching the warning/ban reason inputs. Display stays plain text: the
 * moderation domain stores the raw text and does not server-render it (the
 * warning reason on ModerationWarnings.vue is shown the same way). Rendering
 * BBCode here would need a server-rendered HTML field on the note DTO.
 */
import { ref } from "vue";
import type { ModNote } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import { moderationApi } from "@/entities/moderation";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { formatDateFull } from "@/shared/lib/utils/datetime";

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
  editingNoteId.value = note.id;
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

// --- Delete note (ConfirmDialog-gated) ---
const deleteTarget = ref<ModNote | null>(null);
const deleting = ref(false);

async function confirmDelete() {
  if (!deleteTarget.value || deleting.value) return;
  deleting.value = true;
  const { error } = await moderationApi.deleteModNote(deleteTarget.value.id);
  deleting.value = false;
  if (error) {
    toast.error("Не удалось удалить заметку");
    return;
  }
  deleteTarget.value = null;
  emit("updated");
}
</script>

<template>
  <div class="mod-section">
    <h4 class="mod-section_title">Заметки модератора ({{ notes.length }})</h4>

    <div v-for="note in notes" :key="note.id" class="mod-note">
      <div class="mod-note_header">
        <strong>{{ note.authorUsername }}</strong>
        <secondary-text>{{ formatDateFull(note.createdUtc) }}</secondary-text>
        <secondary-text v-if="note.modifiedUtc">
          (изменено {{ formatDateFull(note.modifiedUtc) }})
        </secondary-text>
      </div>

      <template v-if="editingNoteId !== note.id">
        <div class="mod-note_text">{{ note.text }}</div>
        <div v-if="note.canEdit || note.canDelete" class="mod-note_actions">
          <button
            v-if="note.canEdit"
            type="button"
            class="mod-action"
            @click="startEdit(note)"
          >
            Изменить
          </button>
          <button
            v-if="note.canDelete"
            type="button"
            class="mod-action mod-action-danger"
            @click="deleteTarget = note"
          >
            Удалить
          </button>
        </div>
      </template>

      <template v-else>
        <div class="mod-note_edit">
          <BBCodeEditor
            v-model="editText"
            context="common"
            placeholder="Заметка модератора..."
            :min-height="80"
            :max-height="250"
            :is-moderator="true"
          />
          <div class="mod-note_edit-actions">
            <Button :disabled="!editText.trim()" @click="saveEdit(note.id)">
              Сохранить
            </Button>
            <Button @click="cancelEdit">Отмена</Button>
          </div>
        </div>
      </template>
    </div>

    <div v-if="canCreate" class="mod-note_create">
      <BBCodeEditor
        v-model="newNoteText"
        context="common"
        placeholder="Заметка модератора..."
        :min-height="80"
        :max-height="250"
        :is-moderator="true"
        :disabled="creating"
      />
      <Button
        :disabled="!newNoteText.trim()"
        :loading="creating"
        @click="createNote"
      >
        Добавить заметку
      </Button>
    </div>

    <ConfirmDialog
      :show="deleteTarget !== null"
      title="Удаление заметки"
      message="Удалить заметку?"
      confirm-label="Удалить"
      danger
      :loading="deleting"
      @update:show="(v) => !v && (deleteTarget = null)"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped lang="sass">
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
  padding: 0
  border: none
  background: none
  cursor: pointer
  font-family: inherit
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
