<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import type { Username, UserProfileNote } from "@/shared/api/models/community";
import { communityApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";

const props = defineProps<{
  username: Username;
}>();

const { user: currentUser } = storeToRefs(useUserStore());

const note = ref<UserProfileNote | null>(null);
const editText = ref("");
const isEditing = ref(false);
const isSaving = ref(false);
const loading = ref(true);

async function fetchNote() {
  if (!currentUser.value) {
    loading.value = false;
    return;
  }

  loading.value = true;
  const { data } = await communityApi.getUserProfileNote(props.username);
  note.value = data || null;
  editText.value = note.value?.text || "";
  loading.value = false;
}

onMounted(fetchNote);
watch(() => props.username, fetchNote);

function startEditing() {
  editText.value = note.value?.text || "";
  isEditing.value = true;
}

function cancelEditing() {
  editText.value = note.value?.text || "";
  isEditing.value = false;
}

async function saveNote() {
  if (!editText.value.trim()) {
    await deleteNote();
    return;
  }

  isSaving.value = true;
  const { data } = await communityApi.upsertUserProfileNote(
    props.username,
    editText.value,
  );
  if (data) {
    note.value = data;
  }
  isSaving.value = false;
  isEditing.value = false;
}

async function deleteNote() {
  isSaving.value = true;
  await communityApi.deleteUserProfileNote(props.username);
  note.value = null;
  editText.value = "";
  isSaving.value = false;
  isEditing.value = false;
}
</script>

<template>
  <section v-if="currentUser" class="profile-personal-note">
    <div class="section-header">
      <h3 class="section-title">Личная заметка</h3>
      <span class="section-hint">Видна только вам</span>
    </div>

    <div v-if="loading" class="loading">Загрузка...</div>

    <template v-else>
      <template v-if="isEditing">
        <textarea
          v-model="editText"
          class="note-textarea"
          placeholder="Напишите заметку об этом пользователе..."
          rows="4"
        />
        <div class="note-actions">
          <Button :disabled="isSaving" @click="saveNote">
            {{ isSaving ? "Сохранение..." : "Сохранить" }}
          </Button>
          <Button secondary @click="cancelEditing">Отмена</Button>
        </div>
      </template>

      <template v-else>
        <div v-if="note?.text" class="note-content">{{ note.text }}</div>
        <div v-else class="note-empty">Заметка не добавлена</div>
        <Button secondary @click="startEditing">
          {{ note?.text ? "Редактировать" : "Добавить заметку" }}
        </Button>
      </template>
    </template>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-personal-note
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium
  border: 1px dashed $border

.section-header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $small

.section-title
  color: $text
  margin: 0
  font-size: 1rem

.section-hint
  font-size: $secondary-font-size
  color: $text-meta
  font-style: italic

.loading
  color: $text-muted
  font-size: $secondary-font-size

.note-textarea
  width: 100%
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background: $bg-element-overlay
  color: $text
  font-family: inherit
  font-size: inherit
  resize: vertical
  min-height: $grid-step * 20
  margin-bottom: $small

  &:focus
    outline: none
    border-color: $link

.note-content
  color: $text
  line-height: 1.5
  margin-bottom: $small
  white-space: pre-wrap

.note-empty
  color: $text-meta
  font-style: italic
  margin-bottom: $small

.note-actions
  display: flex
  gap: $small
</style>
