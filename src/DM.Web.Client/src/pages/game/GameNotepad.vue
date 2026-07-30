<script setup lang="ts">
// Game master notepad — shared notes for the master, assistants and the game
// mentor, hidden from players and readers (MasterNotepad, see GLOSSARY). The
// screen itself is NotepadBoard, shared with the personal and blog notepads;
// this page binds it to the game notepad endpoints and gates access. The
// backend enforces the same gate.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { NotepadBoard, type NotepadAdapter } from "@/widgets/notepad";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { isMaster, isAssistant, isMentor } = storeToRefs(gameStore);

// Notepad endpoints accept the public id (they are publicId-tolerant), so the
// raw route param is enough — no need to wait for the game GUID to resolve.
const gameId = computed(() => route.params.id as string);

// Only the master, assistants and the game mentor may see/manage the notepad.
const canAccess = computed(
  () => isMaster.value || isAssistant.value || isMentor.value,
);

const adapter: NotepadAdapter = {
  list: () => gameApi.getNotepad(gameId.value),
  create: (input) => gameApi.createNote(gameId.value, input),
  update: (id, input) => gameApi.updateNote(gameId.value, id, input),
  remove: (id) => gameApi.deleteNote(gameId.value, id),
};
</script>

<template>
  <NotepadBoard
    :adapter="adapter"
    :accessible="canAccess"
    title="Блокнот мастера"
    denied-text="Блокнот доступен только мастеру, ассистентам и наставнику"
    empty-text="Нет записей в блокноте"
    empty-hint="Создайте первую запись для общих заметок мастера и ассистентов."
    load-error-text="Не удалось загрузить блокнот"
  />
</template>
