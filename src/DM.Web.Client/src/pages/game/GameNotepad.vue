<script setup lang="ts">
// The notepad of the game itself, for its master and assistants.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameNotepadApi } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { NotepadBoard, adapterOf } from "@/widgets/notepad";
import { LoginPrompt } from "@/features/auth";

const route = useRoute();
const gameStore = useGameDetailsStore();
const { isMaster, isAssistant } = storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());

// The notepad endpoints accept the public id.
const gameId = computed(() => route.params.id as string);

// The two HasEditAccess admits on the server.
const canAccess = computed(() => isMaster.value || isAssistant.value);

const adapter = adapterOf(() => gameNotepadApi.ofGame(gameId.value));
</script>

<template>
  <LoginPrompt v-if="!user" action="видеть заметки игры" />

  <NotepadBoard
    v-else
    :adapter="adapter"
    :accessible="canAccess"
    :viewer-id="user.id"
    :can-delete-others="isMaster"
    title="Заметки игры"
    denied-text="Заметки игры доступны только мастеру и ассистентам"
    empty-text="Нет записей в заметках игры"
    empty-hint="Создайте первую запись для общих заметок мастера и ассистентов"
    load-error-text="Не удалось загрузить заметки игры"
  />
</template>
