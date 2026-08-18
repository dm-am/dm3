<script setup lang="ts">
// "Заметки игры" — the notepad of the game itself, shared by its master and
// assistants and hidden from everyone else. It is one of three: a character
// carries "Заметки игрока" and "Заметки мастера" of its own, both on the
// character sheet. The screen here is NotepadBoard, shared with the personal
// and blog notepads; this page binds it to the game notepad endpoints and
// gates access. The backend enforces the same gate.
//
// The address stays /game/:id/notes: it was named for the notes, not for the
// caption above them, and renaming the caption is no reason to break links.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { NotepadBoard, type NotepadAdapter } from "@/widgets/notepad";
import { LoginPrompt } from "@/features/auth";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { isMaster, isAssistant } = storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

// Notepad endpoints accept the public id (they are publicId-tolerant), so the
// raw route param is enough — no need to wait for the game GUID to resolve.
const gameId = computed(() => route.params.id as string);

// Master and assistants, the two the server admits (NotepadIntentionResolver
// answers this notepad with HasEditAccess). The curating mentor used to be
// offered the page here and refused by the API on arrival.
const canAccess = computed(() => isMaster.value || isAssistant.value);

const adapter: NotepadAdapter = {
  list: () => gameApi.getNotepad(gameId.value),
  create: (input) => gameApi.createNote(gameId.value, input),
  update: (id, input) => gameApi.updateNote(gameId.value, id, input),
  remove: (id) => gameApi.deleteNote(gameId.value, id),
};
</script>

<template>
  <LoginPrompt v-if="!user" action="видеть заметки игры" />

  <NotepadBoard
    v-else
    :adapter="adapter"
    :accessible="canAccess"
    :viewer-id="user?.id ?? null"
    :can-delete-others="isMaster"
    title="Заметки игры"
    denied-text="Заметки игры доступны только мастеру и ассистентам"
    empty-text="Нет записей в заметках игры"
    empty-hint="Создайте первую запись для общих заметок мастера и ассистентов."
    load-error-text="Не удалось загрузить заметки игры"
  />
</template>
