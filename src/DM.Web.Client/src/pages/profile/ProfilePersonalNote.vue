<script lang="ts">
/**
 * "Личная заметка": what the viewer keeps about the owner of a profile. useProfileNote
 * is the page's: the section is redrawn on every switch to "О себе", and the note outlives it.
 */
import { ref, type Ref } from "vue";
import { userApi, type Username } from "@/entities/user";
import type { PersonalNote } from "@/shared/api/models/common";
import type { User } from "@/shared/api/models/community";
import {
  useActionResult,
  type ActionResult as NoteResult,
} from "@/shared/lib/composables/useActionResult";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

/** What the note holds while the profile is open. */
interface NoteState {
  note: Ref<PersonalNote | null>;
  noteEditText: Ref<string>;
  isEditingNote: Ref<boolean>;
  isNoteSaving: Ref<boolean>;
  confirmingNoteDelete: Ref<boolean>;
  /** A refused note, under the buttons of the editor. */
  noteResult: NoteResult;
}

/** The two requests of the note: the save and the delete it may turn into. */
function noteRequests(
  state: NoteState,
  username: () => Username,
  cancelEditNote: () => void,
) {
  const { note, noteEditText, isEditingNote, isNoteSaving, noteResult } = state;

  async function saveNote() {
    if (!noteEditText.value.trim()) {
      // Nothing stored, nothing to lose and nothing to ask.
      if (note.value?.text) state.confirmingNoteDelete.value = true;
      else cancelEditNote();
      return;
    }
    isNoteSaving.value = true;
    noteResult.clear();
    const { data, error } = await userApi.upsertUserProfileNote(
      username(),
      noteEditText.value,
    );
    isNoteSaving.value = false;
    // A refusal keeps the editor open with the text, and says why under its buttons.
    if (error) {
      noteResult.refuse(error, "Не удалось сохранить заметку");
      return;
    }
    if (data) note.value = data;
    isEditingNote.value = false;
  }

  // The window waits for the request, says a refusal in itself and closes on success alone.
  async function deleteNote() {
    const result = await userApi.deleteUserProfileNote(
      username(),
      OWNS_REFUSAL,
    );
    if (result.error) return result;
    note.value = null;
    noteEditText.value = "";
    isEditingNote.value = false;
    return result;
  }

  return { saveNote, deleteNote };
}

/**
 * The note the viewer keeps about the owner of a profile: what is stored, the
 * text being edited, and the requests that save and delete it.
 */
export function useProfileNote(
  username: () => Username,
  user: Ref<User | null>,
) {
  const state: NoteState = {
    note: ref<PersonalNote | null>(null),
    noteEditText: ref(""),
    isEditingNote: ref(false),
    isNoteSaving: ref(false),
    // A deletion is always asked about, and saving the note empty is a deletion.
    confirmingNoteDelete: ref(false),
    noteResult: useActionResult(),
  };
  const { note, noteEditText, isEditingNote, noteResult } = state;

  // The note comes with the profile, read for whoever holds the session.
  function seedNote() {
    note.value = user.value?.personalNote ?? null;
    noteEditText.value = note.value?.text ?? "";
  }

  function startEditNote() {
    // From the stored note, so a cancelled edit does not leak in.
    noteEditText.value = note.value?.text ?? "";
    isEditingNote.value = true;
  }

  function cancelEditNote() {
    noteEditText.value = note.value?.text ?? "";
    isEditingNote.value = false;
    noteResult.clear();
  }

  return {
    ...state,
    seedNote,
    startEditNote,
    cancelEditNote,
    ...noteRequests(state, username, cancelEditNote),
  };
}

type ProfileNote = ReturnType<typeof useProfileNote>;
</script>

<script setup lang="ts">
import Button from "@/shared/ui/Button/Button.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { BlockTitle } from "@/shared/ui/Layout";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";

const props = defineProps<{
  /** The page's: the note outlives this section, redrawn on each tab switch. */
  model: ProfileNote;
}>();

const {
  note,
  noteEditText,
  isEditingNote,
  isNoteSaving,
  confirmingNoteDelete,
  noteResult,
  startEditNote,
  cancelEditNote,
  saveNote,
  deleteNote,
} = props.model;
</script>

<template>
  <section class="profile-note">
    <BlockTitle>Личная заметка</BlockTitle>
    <template v-if="!isEditingNote">
      <p v-if="note?.text" class="note-text">{{ note.text }}</p>
      <p v-else class="note-empty">Заметки об этом пользователе нет</p>
      <!-- Actions alone: the note is the viewer's own and signs nothing. -->
      <CaptionLine class="note-caption">
        <CaptionAction @click="startEditNote">{{
          note?.text ? "Редактировать" : "Добавить"
        }}</CaptionAction>
        <CaptionAction v-if="note?.text" @click="confirmingNoteDelete = true"
          >Удалить</CaptionAction
        >
      </CaptionLine>
    </template>
    <div v-else class="note-expanded">
      <BBCodeEditor
        v-model="noteEditText"
        context="common"
        placeholder="Напишите заметку об этом пользователе..."
        :min-height="100"
        :max-height="300"
      />
      <div class="note-actions">
        <Button variant="primary" :loading="isNoteSaving" @click="saveNote">
          Сохранить
        </Button>
        <Button :disabled="isNoteSaving" @click="cancelEditNote">
          Отмена
        </Button>
      </div>
      <ActionResult :result="noteResult" class="note-refusal" />
    </div>

    <!-- Teleported to body, so it takes no room in the section. -->
    <ConfirmDialog
      v-model:show="confirmingNoteDelete"
      title="Удалить заметку?"
      message="Заметка будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить заметку"
      :action="deleteNote"
    />
  </section>
</template>

<style scoped lang="sass">
.profile-note > :deep(h2:first-child)
  margin-top: 0

.note-text
  margin: 0 0 $small
  color: $text
  overflow-wrap: anywhere
  white-space: pre-wrap

.note-empty
  margin: 0 0 $small
  color: $text-muted
  font-style: italic

.note-caption
  display: block
  margin-top: $minor

.note-expanded
  display: flex
  flex-direction: column
  gap: $small
  margin-top: $small

.note-actions
  display: flex
  gap: $small

// Spaced by the gap of the column, as the form of a new name above.
.note-refusal
  --action-result-margin: 0
</style>
