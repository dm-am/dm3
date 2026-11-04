<script setup lang="ts">
// The blog notepad: NotepadBoard, bound to the blog's endpoints. The server
// enforces the same gate (BlogNotepadService).
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { NotepadBoard, type NotepadAdapter } from "@/widgets/notepad";
import { LoginPrompt } from "@/features/auth";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { canUseNotepad, isOwner } = storeToRefs(blogStore);
const { user } = storeToRefs(useAuthStore());

// The endpoints take the public id, so the route parameter is enough.
const blogId = computed(() => route.params.id as string);

const adapter: NotepadAdapter = {
  list: () => blogApi.getNotepad(blogId.value),
  create: (input) => blogApi.createNote(blogId.value, input),
  update: (id, input) => blogApi.updateNote(blogId.value, id, input),
  remove: (id, options) => blogApi.deleteNote(blogId.value, id, options),
};
</script>

<template>
  <LoginPrompt v-if="!user" action="видеть заметки блога" />

  <NotepadBoard
    v-else
    :adapter="adapter"
    :accessible="canUseNotepad"
    :viewer-id="user.id"
    :can-delete-others="isOwner"
    title="Заметки блога"
    denied-text="Заметки доступны только автору блога и ассистентам"
    empty-text="Нет записей в заметках блога"
    empty-hint="Создайте первую запись для общих заметок автора блога и ассистентов"
    load-error-text="Не удалось загрузить заметки"
  />
</template>
