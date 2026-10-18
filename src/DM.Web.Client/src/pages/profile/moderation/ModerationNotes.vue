<script setup lang="ts">
/**
 * ModerationNotes — moderator notes about a user (product doc 4.2.2.20), a
 * section of the profile's "Модерация" tab.
 *
 * Divergence from the doc, kept intentionally: the doc frames this as ONE
 * note per profile, but the backend models it as a LIST of authored notes
 * (each carries its own author, timestamps and per-note canEdit/canDelete —
 * see ModeratedProfileNote / GET moderatorNotes). The multi-note model is the
 * richer, correct one — several moderators can leave distinct, attributed
 * notes over time rather than overwriting a single shared field — so it is
 * preserved and flagged rather than collapsed to a single note.
 *
 * The notes read the way the viewer's own note on the same page reads: the
 * text, and a grey "Добавить" under it that opens the editor. The editor used
 * to stand open under the list at all times, with its toolbar and a button
 * across the column, and pushed everything after it down the tab.
 *
 * The note text is authored with the BBCode editor (doc: "BBCode editor"),
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
import {
  CaptionAction,
  CaptionLine,
  CaptionPart,
} from "@/shared/ui/CaptionLine";
import { BlockTitle } from "@/shared/ui/Layout";
import Button from "@/shared/ui/Button/Button.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = defineProps<{
  notes: ModNote[];
  canCreate: boolean;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

/** The editor of a new note is open. */
const adding = ref(false);
const newNoteText = ref("");
const creating = ref(false);

const editingNoteId = ref<string | null>(null);
const editText = ref("");

/** A refused note, under the button that added or saved it; text kept. */
const createResult = useActionResult();
const editResult = useActionResult();

function startAdding() {
  createResult.clear();
  adding.value = true;
}

function cancelAdding() {
  adding.value = false;
  newNoteText.value = "";
  createResult.clear();
}

async function createNote() {
  if (!newNoteText.value.trim()) return;
  creating.value = true;
  createResult.clear();
  const { error } = await moderationApi.createModNote(
    props.targetUsername as Username,
    newNoteText.value.trim(),
  );
  creating.value = false;
  if (error) {
    createResult.refuse(error, "Не удалось создать заметку");
    return;
  }
  newNoteText.value = "";
  adding.value = false;
  emit("updated");
}

function startEdit(note: ModNote) {
  editingNoteId.value = note.id;
  editText.value = note.text;
}

function cancelEdit() {
  editingNoteId.value = null;
  editText.value = "";
  editResult.clear();
}

async function saveEdit(noteId: string) {
  if (!editText.value.trim()) return;
  editResult.clear();
  const { error } = await moderationApi.updateModNote(
    noteId,
    editText.value.trim(),
  );
  if (error) {
    editResult.refuse(error, "Не удалось сохранить заметку");
    return;
  }
  cancelEdit();
  emit("updated");
}

// --- Delete note (ConfirmDialog-gated) ---
const deleteTarget = ref<ModNote | null>(null);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmDelete() {
  const target = deleteTarget.value;
  if (!target) return;
  const result = await moderationApi.deleteModNote(target.id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) emit("updated");
  return result;
}
</script>

<template>
  <section class="mod-section">
    <BlockTitle>Заметки модераторов</BlockTitle>

    <div v-for="note in notes" :key="note.id" class="mod-note">
      <!-- The name, then the caption line in the size of the secondary text
           around it: the date, the mark of an edit and what the viewer may do
           with the note. The actions go while the note is open in the editor,
           which has buttons of its own. -->
      <div class="mod-note_header">
        <strong class="mod-note_author">{{ note.authorUsername }}</strong
        >{{ " "
        }}<CaptionLine size="secondary"
          ><CaptionPart
            :bar="!note.modifiedUtc"
            :class="{ 'mod-note_date--joined': note.modifiedUtc }"
            >{{ formatDateFull(note.createdUtc) }}</CaptionPart
          ><span v-if="note.modifiedUtc"
            >(изменено {{ formatDateFull(note.modifiedUtc) }})</span
          ><CaptionAction
            v-if="note.canEdit && editingNoteId !== note.id"
            @click="startEdit(note)"
            >Редактировать</CaptionAction
          ><CaptionAction
            v-if="note.canDelete && editingNoteId !== note.id"
            @click="deleteTarget = note"
            >Удалить</CaptionAction
          ></CaptionLine
        >
      </div>

      <div v-if="editingNoteId !== note.id" class="mod-note_text">
        {{ note.text }}
      </div>

      <div v-else class="mod-note_edit">
        <BBCodeEditor
          v-model="editText"
          context="common"
          placeholder="Заметка модератора..."
          :min-height="80"
          :max-height="250"
          :is-moderator="true"
        />
        <div class="mod-note_actions">
          <Button
            variant="primary"
            :disabled="!editText.trim()"
            @click="saveEdit(note.id)"
          >
            Сохранить
          </Button>
          <Button @click="cancelEdit">Отмена</Button>
        </div>
        <ActionResult :result="editResult" />
      </div>
    </div>

    <p v-if="!notes.length && !adding" class="mod-note_empty">Заметок нет</p>

    <template v-if="canCreate">
      <!-- The personal note of the same page opens its editor the same way. -->
      <CaptionLine v-if="!adding" class="mod-note_add">
        <CaptionAction @click="startAdding">Добавить</CaptionAction>
      </CaptionLine>
      <div v-else class="mod-note_create">
        <BBCodeEditor
          v-model="newNoteText"
          context="common"
          placeholder="Заметка модератора..."
          :min-height="80"
          :max-height="250"
          :is-moderator="true"
          :disabled="creating"
        />
        <div class="mod-note_actions">
          <Button
            variant="primary"
            :disabled="!newNoteText.trim()"
            :loading="creating"
            @click="createNote"
          >
            Сохранить
          </Button>
          <Button :disabled="creating" @click="cancelAdding">Отмена</Button>
        </div>
        <ActionResult :result="createResult" class="mod-note_create-refusal" />
      </div>
    </template>

    <ConfirmDialog
      :show="deleteTarget !== null"
      title="Удалить заметку?"
      message="Заметка будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить заметку"
      :action="confirmDelete"
      @update:show="(v) => !v && (deleteTarget = null)"
    />
  </section>
</template>

<style scoped lang="sass">
.mod-note + .mod-note
  margin-top: $small

// Inline flow: the name and the caption line share a baseline, and a narrow
// column wraps the line under the name instead of beside it.
.mod-note_header
  margin-bottom: $minor

// The step the old flex gap made between the name and the date, and between
// the date and the mark of an edit: a space and $minor more.
.mod-note_author
  margin-right: $minor

.mod-note_date--joined
  margin-right: $minor

.mod-note_text
  white-space: pre-wrap
  word-break: break-word

.mod-note_edit
  margin-top: $minor

// The note of the page says it is empty the same way.
.mod-note_empty
  margin: 0
  color: $text-muted
  font-style: italic

.mod-note_add
  display: block
  margin-top: $minor

.mod-note_create
  display: flex
  flex-direction: column
  gap: $small
  margin-top: $small

.mod-note_actions
  display: flex
  gap: $small
  margin-top: $small

.mod-note_create .mod-note_actions
  margin-top: 0

// Spaced by the gap of the column.
.mod-note_create-refusal
  --action-result-margin: 0
</style>
