<script setup lang="ts">
// Blog notepad — shared notes for the blog owner, assistants and the assigned
// mentor, hidden from readers (dev doc 4.2.3.6.4 "Заметки блога"). The screen
// itself is NotepadBoard, shared with the personal and game notepads; this page
// binds it to the blog notepad endpoints and gates access. The backend enforces
// the same gate (BlogNotepadService).
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

// Notepad endpoints accept the public id (they are publicId-tolerant), so the
// raw route param is enough — no need to wait for the blog GUID to resolve.
const blogId = computed(() => route.params.id as string);

const adapter: NotepadAdapter = {
  list: () => blogApi.getNotepad(blogId.value),
  create: (input) => blogApi.createNote(blogId.value, input),
  update: (id, input) => blogApi.updateNote(blogId.value, id, input),
  remove: (id) => blogApi.deleteNote(blogId.value, id),
};
</script>

<template>
  <LoginPrompt v-if="!user" action="видеть заметки блога" />

  <NotepadBoard
    v-else
    :adapter="adapter"
    :accessible="canUseNotepad"
    :viewer-id="user?.id ?? null"
    :can-delete-others="isOwner"
    title="Заметки блога"
    denied-text="Заметки доступны только мастеру блога и ассистентам"
    empty-text="Заметок пока нет"
    empty-hint="Создайте первую запись для общих заметок мастера блога и ассистентов."
    load-error-text="Не удалось загрузить заметки"
  />
</template>
